using System;
using System.Collections.Generic;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    [CreateAssetMenu(menuName = "SFramework/Pools Database", fileName = "db_pools")]
    public class SFPoolsDatabase : SFDatabase
    {
        public override string Title => "Pools";
        public override ISFDatabaseNode[] Nodes => _prefabsGroupContainers;
        
        [SerializeField]
        private SFPrefabsGroupContainer[] _prefabsGroupContainers = Array.Empty<SFPrefabsGroupContainer>();
    }
}