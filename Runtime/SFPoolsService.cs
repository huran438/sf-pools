using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace SFramework.Pools.Runtime
{
    public partial class SFPoolsService : ISFPoolsService
    {
        private sealed class PoolEntry
        {
            public string Prefab;
            public AsyncOperationHandle<GameObject> Handle;
            public Action<AsyncOperationHandle<GameObject>> OnCompleted;
            public readonly UniTaskCompletionSource Ready = new();
            public ObjectPool<GameObject> Pool;
        }

        private readonly Dictionary<string, PoolEntry> _entries = new();
        private readonly Dictionary<GameObject, PoolEntry> _borrowed = new();
        private readonly Dictionary<string, SFPrefabNode> _prefabNodeByName = new();

        private readonly ISFConfigsService _configsService;
        private Func<string, AsyncOperationHandle<GameObject>> _loadAsset = key => Addressables.LoadAssetAsync<GameObject>(key);
        private bool _disposed;

        SFPoolsService(ISFConfigsService provider)
        {
            _configsService = provider;
        }

        public UniTask Init(CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SFPoolsService));
            cancellationToken.ThrowIfCancellationRequested();

            _prefabNodeByName.Clear();
            foreach (var config in _configsService.Configs.OfType<SFPoolsConfig>())
            {
                if (config.Groups == null) continue;
                foreach (var group in config.Groups)
                {
                    if (group?.Prefabs == null) continue;
                    foreach (var prefab in group.Prefabs)
                    {
                        if (prefab != null) _prefabNodeByName[prefab.FullId] = prefab;
                    }
                }
            }

            return UniTask.CompletedTask;
        }

        public bool CanSpawnPrefab(string prefab) =>
            prefab != null && _entries.TryGetValue(prefab, out var entry) && entry.Pool != null;

        public async UniTask Load(string prefab, IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SFPoolsService));
            cancellationToken.ThrowIfCancellationRequested();

            if (!_entries.TryGetValue(prefab, out var entry))
            {
                var node = _prefabNodeByName[prefab];
                entry = new PoolEntry { Prefab = prefab, Handle = _loadAsset(node.Path) };
                _entries.Add(prefab, entry);
                ObserveLoad(prefab, entry);
            }

            var ready = entry.Ready.Task;
            if (progress != null && ready.Status == UniTaskStatus.Pending)
            {
                await UniTask.WaitUntil(() =>
                {
                    if (ready.Status != UniTaskStatus.Pending) return true;
                    progress.Report(entry.Handle.PercentComplete);
                    return ready.Status != UniTaskStatus.Pending;
                }, cancellationToken: cancellationToken);
            }

            await ready.AttachExternalCancellation(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(prefab, entry)) throw new OperationCanceledException("The pool was unloaded.");
            progress?.Report(1f);
        }

        private void ObserveLoad(string prefab, PoolEntry entry)
        {
            if (!entry.Handle.IsValid())
            {
                _entries.Remove(prefab);
                entry.Ready.TrySetException(new InvalidOperationException($"No Addressables operation for prefab '{prefab}'."));
                return;
            }

            if (entry.Handle.IsDone)
            {
                CompleteLoad(prefab, entry, entry.Handle);
                return;
            }

            entry.OnCompleted = handle => CompleteLoad(prefab, entry, handle);
            entry.Handle.Completed += entry.OnCompleted;
        }

        private void CompleteLoad(string prefab, PoolEntry entry, AsyncOperationHandle<GameObject> handle)
        {
            if (!IsCurrent(prefab, entry)) return;
            entry.OnCompleted = null;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                var error = new InvalidOperationException($"Could not load Addressable prefab '{prefab}'.", handle.OperationException);
                _entries.Remove(prefab);
                entry.Handle.Release();
                entry.Ready.TrySetException(error);
                return;
            }

            var asset = handle.Result;
            entry.Pool = new ObjectPool<GameObject>(() => CreateInstance(asset), actionOnDestroy: DestroyInstance);
            entry.Ready.TrySetResult();
        }

        private bool IsCurrent(string prefab, PoolEntry entry) =>
            _entries.TryGetValue(prefab, out var current) && current == entry;

        private static GameObject CreateInstance(GameObject prefab)
        {
            var instance = Object.Instantiate(prefab);
            try
            {
                // SFView owns its Awake injection; traverse plain injectable components once on creation.
                SFContainer.Inject(instance, includeInactive: true, skipViews: true);
                return instance;
            }
            catch
            {
                DestroyInstance(instance);
                throw;
            }
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }

        public void Unload(string prefab)
        {
            if (prefab == null || !_entries.TryGetValue(prefab, out var entry)) return;
            _entries.Remove(prefab);

            if (entry.OnCompleted != null && entry.Handle.IsValid())
                entry.Handle.Completed -= entry.OnCompleted;

            if (entry.Pool == null)
            {
                if (entry.Handle.IsValid()) entry.Handle.Release();
                entry.Ready.TrySetCanceled();
                return;
            }

            var instances = _borrowed.Where(pair => pair.Value == entry).Select(pair => pair.Key).ToArray();
            foreach (var instance in instances)
            {
                _borrowed.Remove(instance);
                DestroyInstance(instance);
            }
            entry.Pool.Clear();

            // In Play Mode Destroy is deferred; keep dependencies alive through OnDestroy.
            if (Application.isPlaying) ReleaseAfterDestroy(entry.Handle).Forget();
            else if (entry.Handle.IsValid()) entry.Handle.Release();
        }

        private static async UniTask ReleaseAfterDestroy(AsyncOperationHandle<GameObject> handle)
        {
            await UniTask.NextFrame();
            if (handle.IsValid()) handle.Release();
        }

        public bool Spawn(string prefab, out GameObject gameObject, Vector3 position, Quaternion rotation)
        {
            gameObject = null;
            if (!TryTake(prefab, out var entry, out var instance) ||
                !Place(prefab, entry, instance, position, rotation)) return false;

            gameObject = instance;
            return true;
        }

        public bool Spawn<T>(string prefab, out T component, Vector3 position, Quaternion rotation) where T : Component
        {
            component = null;
            if (!TryTake(prefab, out var entry, out var instance)) return false;

            var found = instance.GetComponent<T>();
            if (found == null)
            {
                Despawn(instance);
                return false;
            }

            if (!Place(prefab, entry, instance, position, rotation)) return false;

            component = found;
            return true;
        }

        private bool TryTake(string prefab, out PoolEntry entry, out GameObject instance)
        {
            entry = null;
            instance = null;
            if (prefab == null || !_entries.TryGetValue(prefab, out entry) || entry.Pool == null) return false;

            // Skip stale inactive instances, but attempt to instantiate at most once.
            var remaining = entry.Pool.CountInactive + 1;
            do instance = entry.Pool.Get();
            while (instance == null && --remaining > 0 && IsCurrent(prefab, entry));

            if (!IsCurrent(prefab, entry))
            {
                DestroyInstance(instance);
                return false;
            }

            if (instance == null) return false;
            _borrowed.Add(instance, entry);
            return true;
        }

        private bool Place(string prefab, PoolEntry entry, GameObject instance, Vector3 position, Quaternion rotation)
        {
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            // Unity lifecycle callbacks may despawn or unload the instance.
            return IsCurrent(prefab, entry) && instance != null && _borrowed.ContainsKey(instance);
        }

        public bool Despawn(GameObject gameObject)
        {
            if (ReferenceEquals(gameObject, null) || !_borrowed.TryGetValue(gameObject, out var entry)) return false;
            _borrowed.Remove(gameObject);
            if (gameObject == null) return false;

            gameObject.SetActive(false);
            if (gameObject != null && IsCurrent(entry.Prefab, entry))
                entry.Pool.Release(gameObject);
            else
                DestroyInstance(gameObject);
            return true;
        }

        public void DespawnAll()
        {
            foreach (var instance in _borrowed.Keys.ToArray())
                Despawn(instance);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var prefab in _entries.Keys.ToArray()) Unload(prefab);
            _prefabNodeByName.Clear();
        }
    }
}
