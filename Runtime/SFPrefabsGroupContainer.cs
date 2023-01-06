using System;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    [Serializable]
    public class SFPrefabsGroupContainer : ISFDatabaseNode
    {
        public string Name => _name;
        public ISFDatabaseNode[] Children => _prefabContainers;
        
        [SerializeField]
        private string _name;
        
        [SerializeField]
        private SFPrefabContainer[] _prefabContainers = Array.Empty<SFPrefabContainer>();
    }
}