using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    public interface ISFPoolsService : ISFService
    {
        float LoadingProgress { get; }
        bool CanSpawnPrefab(string prefab);
        bool Spawn(string prefab, out GameObject gameObject, Vector3 position, Quaternion rotation);
        bool Spawn<T>(string prefab, out T component, Vector3 position, Quaternion rotation) where T : Component;
        bool Despawn(GameObject gameObject);
        void DespawnAll();
    }
}