using System.Collections.Generic;
using System.Linq;
using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace SFramework.Pools.Runtime
{
    public class SFPoolsService : ISFPoolsService
    {
        [SFInject]
        private readonly ISFContainer _container;
        
        private readonly Dictionary<string, GameObject> _prefabObjectBySFPrefab = new();
        private readonly Dictionary<string, IObjectPool<GameObject>> _poolBySFPrefab = new();
        private readonly Dictionary<GameObject, string> _sfPrefabByInstance = new();


        [SFInject]
        public async void Init(ISFRepositoryProvider provider)
        {
            var _repository = provider.GetRepositories<SFPoolsRepository>().FirstOrDefault();

            foreach (SFPrefabsGroupContainer prefabsGroupContainer in _repository.Nodes)
            {
                foreach (SFPrefabNode prefabContainer in prefabsGroupContainer.Nodes)
                {
                    var loadAssetAsync = Addressables.LoadAssetAsync<GameObject>(prefabContainer.Path);
                    await loadAssetAsync.Task;
                    var gameObject = loadAssetAsync.Result;
                    var sfPrefab = $"{prefabsGroupContainer.Name}/{prefabContainer.Name}";
                    _prefabObjectBySFPrefab[sfPrefab] = gameObject;
                    ObjectPool<GameObject> objectPool = null;
                    objectPool = new ObjectPool<GameObject>(() =>
                        {
                            var _gameObject = Object.Instantiate(_prefabObjectBySFPrefab[sfPrefab]);
                            _container.Inject(_gameObject);
                            _sfPrefabByInstance[_gameObject] = sfPrefab;
                            return _gameObject;
                        },
                        go => { go.SetActive(true); },
                        go => { go.SetActive(false); },
                        go =>
                        {
                            Object.Destroy(go);
                            _sfPrefabByInstance.Remove(go);
                        });

                    _poolBySFPrefab[sfPrefab] = objectPool;
                }
            }
        }

        public bool LoadingCompleted { get; private set; }
        public float LoadingProgress => 0f;

        public bool CanSpawnPrefab(string prefab)
        {
            return _prefabObjectBySFPrefab.ContainsKey(prefab);
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