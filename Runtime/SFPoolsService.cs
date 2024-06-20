using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace SFramework.Pools.Runtime
{
    public class SFPoolsService : ISFPoolsService
    {
        private readonly Dictionary<string, GameObject> _prefabObjectBySFPrefab = new();
        private readonly Dictionary<string, IObjectPool<GameObject>> _poolBySFPrefab = new();
        private readonly Dictionary<GameObject, string> _sfPrefabByInstance = new();
        private readonly Dictionary<string, SFPrefabNode> _prefabNodeByName = new();

        private readonly ISFConfigsService _configsService;

        SFPoolsService(ISFConfigsService provider)
        {
            _configsService = provider;
        }
        
        public UniTask Init(CancellationToken cancellationToken)
        {
            foreach (var repository in _configsService.GetConfigs<SFPoolsConfig>())
            {
                foreach (SFPrefabsGroupNode prefabsGroupContainer in repository.Children)
                {
                    foreach (SFPrefabNode prefabContainer in prefabsGroupContainer.Children)
                    {
                        _prefabNodeByName[prefabContainer.FullId] = prefabContainer;
                    }
                }
            }
            
            return UniTask.CompletedTask;
        }

        public bool CanSpawnPrefab(string prefab)
        {
            return _prefabObjectBySFPrefab.ContainsKey(prefab);
        }

        public async UniTask Load(string prefab, IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            var prefabNode = _prefabNodeByName[prefab];
            var loadAssetAsync = Addressables.LoadAssetAsync<GameObject>(prefabNode.Path);
            await loadAssetAsync.Task;
            var gameObject = loadAssetAsync.Result;
            _prefabObjectBySFPrefab[prefab] = gameObject;

            var objectPool = new ObjectPool<GameObject>(() =>
                {
                    var _gameObject = Object.Instantiate(_prefabObjectBySFPrefab[prefab]);
                    _gameObject.Inject();
                    _sfPrefabByInstance[_gameObject] = prefab;
                    return _gameObject;
                },
                go => { go.SetActive(true); },
                go => { go.SetActive(false); },
                go =>
                {
                    Object.Destroy(go);
                    _sfPrefabByInstance.Remove(go);
                });

            _poolBySFPrefab[prefab] = objectPool;
            Addressables.Release(loadAssetAsync);
        }

        public void Unload(string prefab)
        {
            throw new NotImplementedException();
        }

        public bool Spawn(string prefab, out GameObject gameObject, Vector3 position, Quaternion rotation)
        {
            if (!_poolBySFPrefab.ContainsKey(prefab))
            {
                gameObject = null;
                return false;
            }

            var pool = _poolBySFPrefab[prefab];

            gameObject = pool.Get();
            gameObject.transform.position = position;
            gameObject.transform.rotation = rotation;

            return true;
        }

        public bool Spawn<T>(string prefab, out T component, Vector3 position, Quaternion rotation) where T : Component
        {
            if (Spawn(prefab, out var gameObject, position, rotation))
            {
                component = gameObject.GetComponent<T>();
                return true;
            }

            component = null;
            return false;
        }

        public bool Despawn(GameObject gameObject)
        {
            if (!gameObject.activeSelf) return false;
            if (!_sfPrefabByInstance.ContainsKey(gameObject)) return false;
            var sfPrefab = _sfPrefabByInstance[gameObject];
            if (!_poolBySFPrefab.ContainsKey(sfPrefab)) return false;
            var pool = _poolBySFPrefab[sfPrefab];
            pool.Release(gameObject);
            return true;
        }

        public void DespawnAll()
        {
            foreach (var (instance, prefab) in _sfPrefabByInstance)
            {
                Despawn(instance);
            }
        }

        public void Dispose()
        {
        }
   
    }
}