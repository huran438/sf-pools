using System;
using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace SFramework.Pools.Runtime
{
    [Serializable]
    public class SFPrefabNode : SFNode
    {
        public string Path;
        public int PreloadAmount;
        public override ISFNode[] Nodes => null;
    }
}