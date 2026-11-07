using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    public interface ISFPoolsService : ISFService
    {
        bool CanSpawnPrefab(string prefab);

        /// <summary>Loads or joins the pool's shared load. Cancellation stops only this caller's wait.</summary>
        UniTask Load(string prefab, IProgress<float> progress = null, CancellationToken cancellationToken = default);

        /// <summary>Invalidates pending loads, destroys all clones, and releases the pool's prefab handle.</summary>
        void Unload(string prefab);

        bool Spawn(string prefab, out GameObject gameObject, Vector3 position, Quaternion rotation);
        bool Spawn<T>(string prefab, out T component, Vector3 position, Quaternion rotation) where T : Component;
        bool Despawn(GameObject gameObject);
        void DespawnAll();
    }
}
