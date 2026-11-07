using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using SFramework.Pools.Runtime;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace SFramework.Pools.Editor.Tests
{
    public sealed class SFPoolsTests
    {
        private SFPoolsService _service;
        private readonly List<GameObject> _objects = new();
        private readonly List<ControlledLoad> _loads = new();
        private readonly TestConfigs _configs = new();
        private ResourceManager _resources;
        private Action<AsyncOperationHandle, Exception> _originalExceptionHandler;

        [SetUp]
        public void SetUp()
        {
            _originalExceptionHandler = ResourceManager.ExceptionHandler;
            ResourceManager.ExceptionHandler = null;
            _resources = new ResourceManager();
            typeof(ResourceManager).GetField("CallbackHooksEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_resources, false);
            _service = (SFPoolsService)Activator.CreateInstance(typeof(SFPoolsService),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { _configs }, null);
            typeof(SFPoolsService).GetField("_loadAsset", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_service, (Func<string, AsyncOperationHandle<GameObject>>)(address =>
                {
                    var load = new ControlledLoad();
                    _loads.Add(load);
                    return _resources.StartOperation(load, default);
                }));
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            foreach (var load in _loads)
                if (!load.Finished && !load.Destroyed) load.Complete(null, true, (string)null);
            PumpResources();
            foreach (var gameObject in _objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            _objects.Clear();
            _configs.Items.Clear();
            _loads.Clear();
            _resources.Dispose();
            ResourceManager.ExceptionHandler = _originalExceptionHandler;
        }

        [Test]
        public void FailedNewInstanceInjectionDoesNotLeaveAnUntrackedClone()
        {
            var prefab = AddLoadedPool("pools/props/failing");
            prefab.AddComponent<SFPoolInjectionProbe>().Throw = true;

            Assert.Throws<InvalidOperationException>(() =>
                _service.Spawn("pools/props/failing", out GameObject _, Vector3.zero, Quaternion.identity));
            var remaining = Object.FindObjectsByType<SFPoolInjectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var probe in remaining) if (probe.gameObject != prefab) _objects.Add(probe.gameObject);
            Assert.That(remaining, Has.Length.EqualTo(1), "Only the source prefab should remain after injection fails.");
        }

        [Test]
        public void NewInstanceInjectsInactivePlainComponentsOnceAndReuseDoesNotReinject()
        {
            var prefab = AddLoadedPool("pools/props/injected");
            var child = new GameObject("Inactive injectable child");
            child.transform.SetParent(prefab.transform);
            child.AddComponent<SFPoolInjectionProbe>();
            child.SetActive(false);

            _service.Spawn("pools/props/injected", out GameObject instance, Vector3.zero, Quaternion.identity);
            var probe = instance.GetComponentInChildren<SFPoolInjectionProbe>(true);
            Assert.That(probe.Calls, Is.EqualTo(1));
            _service.Despawn(instance);
            _service.Spawn("pools/props/injected", out GameObject reused, Vector3.zero, Quaternion.identity);
            Assert.That(reused, Is.SameAs(instance));
            Assert.That(probe.Calls, Is.EqualTo(1));
        }

        [Test]
        public void PoolConfiguration_BuildsDistinctQualifiedIdsForPrefabsInDifferentGroups()
        {
            var first = new SFPrefabNode { Id = "shared", Path = "first" };
            var second = new SFPrefabNode { Id = "shared", Path = "second" };
            var config = new SFPoolsConfig
            {
                Id = "pools",
                Groups = new[]
                {
                    new SFPrefabsGroupNode { Id = "enemies", Prefabs = new[] { first } },
                    new SFPrefabsGroupNode { Id = "props", Prefabs = new[] { second } }
                }
            };

            config.BuildTree();

            Assert.That(first.FullId, Is.EqualTo("pools/enemies/shared"));
            Assert.That(second.FullId, Is.EqualTo("pools/props/shared"));
            Assert.That(first.Parent, Is.SameAs(config.Groups[0]));
            Assert.That(second.Parent, Is.SameAs(config.Groups[1]));
        }

        [Test]
        public void Spawn_UnknownPool_ReturnsFalseAndClearsOutput()
        {
            var spawned = _service.Spawn("missing", out GameObject instance, Vector3.one, Quaternion.identity);

            Assert.That(spawned, Is.False);
            Assert.That(instance, Is.Null);
            Assert.That(_service.CanSpawnPrefab("missing"), Is.False);
        }

        [Test]
        public void SpawnComponent_UnknownPool_ReturnsFalseAndClearsOutput()
        {
            var spawned = _service.Spawn("missing", out Transform component, Vector3.zero, Quaternion.identity);

            Assert.That(spawned, Is.False);
            Assert.That(component, Is.Null);
        }

        [Test]
        public void Spawn_AvailablePool_PlacesInstanceAtRequestedPose()
        {
            AddLoadedPool("pools/props/crate");
            var position = new Vector3(2f, -3f, 4f);
            var rotation = Quaternion.Euler(20f, 40f, 60f);

            Assert.That(_service.Spawn("pools/props/crate", out GameObject instance, position, rotation), Is.True);

            Assert.That(_service.CanSpawnPrefab("pools/props/crate"), Is.True);
            Assert.That(instance.activeSelf, Is.True);
            Assert.That(instance.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(instance.transform.rotation, rotation), Is.LessThan(0.001f));
        }

        [Test]
        public void SpawnComponent_ReturnsComponentOnTheSpawnedInstance()
        {
            var prefab = AddLoadedPool("pools/props/crate");
            prefab.AddComponent<BoxCollider>();

            Assert.That(_service.Spawn("pools/props/crate", out BoxCollider collider, Vector3.one, Quaternion.identity), Is.True);

            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.transform.position, Is.EqualTo(Vector3.one));
            Assert.That(_service.Despawn(collider.gameObject), Is.True);
            Assert.That(collider.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DespawnThenSpawn_ReusesInstanceAndUpdatesPose()
        {
            AddLoadedPool("pools/props/crate");
            _service.Spawn("pools/props/crate", out GameObject first, Vector3.zero, Quaternion.identity);

            Assert.That(_service.Despawn(first), Is.True);
            Assert.That(first.activeSelf, Is.False);
            Assert.That(_service.Spawn("pools/props/crate", out GameObject reused, Vector3.one, Quaternion.Euler(0f, 90f, 0f)), Is.True);

            Assert.That(reused, Is.SameAs(first));
            Assert.That(reused.activeSelf, Is.True);
            Assert.That(reused.transform.position, Is.EqualTo(Vector3.one));
            Assert.That(Quaternion.Angle(reused.transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.001f));
        }

        [Test]
        public void Despawn_AlreadyReleasedInstance_DoesNotReleaseTwice()
        {
            AddLoadedPool("pools/props/crate");
            _service.Spawn("pools/props/crate", out GameObject instance, Vector3.zero, Quaternion.identity);

            Assert.That(_service.Despawn(instance), Is.True);
            Assert.That(_service.Despawn(instance), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Despawn_UnownedObject_ReturnsFalseWithoutChangingActivation(bool active)
        {
            var instance = new GameObject("Unowned pool test object");
            _objects.Add(instance);
            instance.SetActive(active);

            Assert.That(_service.Despawn(instance), Is.False);
            Assert.That(instance.activeSelf, Is.EqualTo(active));
        }

        [Test]
        public void DespawnAll_ReleasesActiveInstancesAcrossPoolsAndCanBeRepeated()
        {
            AddLoadedPool("pools/props/crate");
            AddLoadedPool("pools/enemies/guard");
            _service.Spawn("pools/props/crate", out GameObject first, Vector3.zero, Quaternion.identity);
            _service.Spawn("pools/props/crate", out GameObject second, Vector3.zero, Quaternion.identity);
            _service.Spawn("pools/enemies/guard", out GameObject third, Vector3.zero, Quaternion.identity);
            _service.Despawn(first);

            _service.DespawnAll();
            _service.DespawnAll();

            Assert.That(first.activeSelf, Is.False);
            Assert.That(second.activeSelf, Is.False);
            Assert.That(third.activeSelf, Is.False);
        }

        [Test]
        public void TypedSpawnWithMissingComponentReturnsFalseAndDoesNotLeaveAnActiveClone()
        {
            const string id = "pools/props/crate";
            var prefab = AddLoadedPool(id);
            prefab.AddComponent<SFPoolInjectionProbe>();

            Assert.That(_service.Spawn(id, out BoxCollider component, Vector3.zero, Quaternion.identity), Is.False);
            Assert.That(component, Is.Null);
            var probes = Object.FindObjectsByType<SFPoolInjectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.That(probes, Is.EqualTo(new[] { prefab.GetComponent<SFPoolInjectionProbe>() }));
        }

        [Test]
        public void UnloadDestroysBorrowedAndReturnedInstancesAndReleasesTheHandle()
        {
            const string id = "pools/props/crate";
            AddLoadedPool(id);
            _service.Spawn(id, out GameObject active, Vector3.zero, Quaternion.identity);
            _service.Spawn(id, out GameObject inactive, Vector3.zero, Quaternion.identity);
            _service.Despawn(inactive);
            Assert.That(_loads[0].Destroyed, Is.False, "The pool must retain the asset while clones exist.");

            _service.Unload(id);
            _service.Unload(id);

            Assert.That(active == null, Is.True);
            Assert.That(inactive == null, Is.True);
            Assert.That(_loads[0].Destroyed, Is.True);
            Assert.That(_service.CanSpawnPrefab(id), Is.False);
        }

        [Test]
        public void ConcurrentLoadSharesHandleAndCancellationOnlyAffectsOneCaller()
        {
            const string id = "pools/props/crate";
            var prefab = ConfigurePool(id);
            using var cancellation = new CancellationTokenSource();
            var canceled = _service.Load(id, cancellationToken: cancellation.Token);
            var waiting = _service.Load(id).AsTask();
            Assert.That(_loads, Has.Count.EqualTo(1));

            cancellation.Cancel();
            Assert.That(canceled.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Catch<OperationCanceledException>(() => canceled.GetAwaiter().GetResult());
            Assert.That(waiting.IsCompleted, Is.False);
            _loads[0].Complete(prefab, true, (string)null);

            Assert.That(waiting.IsCompletedSuccessfully, Is.True);
            Assert.That(_service.CanSpawnPrefab(id), Is.True);
            Assert.That(_loads[0].Destroyed, Is.False);
            var progress = new RecordingProgress();
            _service.Load(id, progress).GetAwaiter().GetResult();
            Assert.That(progress.Last, Is.EqualTo(1f));
            Assert.That(_loads, Has.Count.EqualTo(1));
        }

        [Test]
        public void FailedLoadReleasesHandleAndCanBeRetried()
        {
            const string id = "pools/props/crate";
            var prefab = ConfigurePool(id);
            var failed = _service.Load(id).AsTask();
            _loads[0].Complete(null, false, "Expected address failure");
            PumpResources();

            Assert.That(failed.IsFaulted, Is.True);
            Assert.That(failed.Exception.InnerException.InnerException.Message, Does.Contain("Expected address failure"));
            Assert.That(_service.CanSpawnPrefab(id), Is.False);
            Assert.That(_loads[0].Destroyed, Is.True);
            var retry = _service.Load(id).AsTask();
            _loads[1].Complete(prefab, true, (string)null);
            Assert.That(retry.IsCompletedSuccessfully, Is.True);
            Assert.That(_service.CanSpawnPrefab(id), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingLoadCannotPublishAPoolAfterTeardown(bool dispose)
        {
            const string id = "pools/props/crate";
            var prefab = ConfigurePool(id);
            var waiting = _service.Load(id);

            if (dispose) _service.Dispose();
            else _service.Unload(id);
            Assert.That(waiting.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Catch<OperationCanceledException>(() => waiting.GetAwaiter().GetResult());

            _loads[0].Complete(prefab, true, (string)null);
            Assert.That(_loads[0].Destroyed, Is.True);
            Assert.That(_service.CanSpawnPrefab(id), Is.False);
        }

        [Test]
        public void ManuallyDisabledBorrowedInstanceCanStillBeReturned()
        {
            const string id = "pools/props/crate";
            AddLoadedPool(id);
            _service.Spawn(id, out GameObject instance, Vector3.zero, Quaternion.identity);
            instance.SetActive(false);

            Assert.That(_service.Despawn(instance), Is.True);
            Assert.That(_service.Despawn(instance), Is.False);
            _service.Spawn(id, out GameObject reused, Vector3.zero, Quaternion.identity);
            Assert.That(reused, Is.SameAs(instance));
        }

        private GameObject AddLoadedPool(string id)
        {
            var prefab = ConfigurePool(id);
            var load = _service.Load(id);
            _loads[^1].Complete(prefab, true, (string)null);
            load.GetAwaiter().GetResult();
            return prefab;
        }

        private GameObject ConfigurePool(string id)
        {
            var parts = id.Split('/');
            var prefab = new GameObject("Pool test prefab");
            _objects.Add(prefab);
            var config = new SFPoolsConfig
            {
                Id = parts[0],
                Groups = new[] { new SFPrefabsGroupNode
                {
                    Id = parts[1],
                    Prefabs = new[] { new SFPrefabNode { Id = parts[2], Path = id } }
                } }
            };
            config.BuildTree();
            // Mixed config sets must not require a casting lookup.
            _configs.Items.Add(new OtherConfig());
            _configs.Items.Add(config);
            _service.Init(default).GetAwaiter().GetResult();
            return prefab;
        }

        private sealed class ControlledLoad : AsyncOperationBase<GameObject>
        {
            public bool Destroyed { get; private set; }
            public bool Finished { get; private set; }

            public new void Complete(GameObject result, bool success, string error)
            {
                Finished = true;
                base.Complete(result, success, error);
            }

            protected override void Execute() { }
            protected override void Destroy() => Destroyed = true;
        }

        private void PumpResources() =>
            typeof(ResourceManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_resources, new object[] { 0f });

        private sealed class RecordingProgress : IProgress<float>
        {
            public float Last { get; private set; }
            public void Report(float value) => Last = value;
        }

        private struct OtherConfig : ISFConfig
        {
            public string Type { get; set; }
            public long Version { get; set; }
        }

        private sealed class TestConfigs : ISFConfigsService
        {
            public readonly List<ISFConfig> Items = new();
            public IEnumerable<ISFConfig> Configs => Items;
            public UniTask Init(CancellationToken cancellationToken) => UniTask.CompletedTask;
            public void Dispose() { }
            public IEnumerable<T> GetConfigs<T>() where T : ISFConfig => throw new NotSupportedException();
            public bool TryGetConfigs<T>(out T[] configs) where T : ISFConfig => throw new NotSupportedException();
            public bool TryGetGlobalConfig<T>(out T config) where T : ISFGlobalConfig => throw new NotSupportedException();
        }
    }

    public sealed class SFPoolInjectionProbe : MonoBehaviour, ISFInjectable, ISFInjectionCallback
    {
        public bool Throw;
        public int Calls;
        public void OnDependenciesInjected()
        {
            Calls++;
            if (Throw) throw new InvalidOperationException("injection failed");
        }
    }
}

