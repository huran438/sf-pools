using System;
using SFramework.Core.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace SFramework.Pools.Runtime
{
    [Serializable]
    public class SFPrefabContainer : ISFDatabaseNode
    {
        public AssetReferenceGameObject PrefabAssetReference => _prefabAssetReference;
        public int PreloadAmount => _preloadAmount;

        [SerializeField]
        private string _name;
        
        [Min(0)]
        [SerializeField]
        private int _preloadAmount;
        
        [SerializeField]
        private AssetReferenceGameObject _prefabAssetReference;

        public string Name => _name;
        public ISFDatabaseNode[] Children => null;
    }
}