using System;
using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    [Serializable]
    public class SFPrefabsGroupContainer : SFNode
    {
        public override ISFNode[] Nodes => Prefabs;
        
        public SFPrefabNode[] Prefabs = Array.Empty<SFPrefabNode>();
    }
}