using System;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace SFramework.Pools.Runtime
{
    [Serializable]
    public class SFPrefabNode : SFConfigNode
    {
        public string Path;
        public int PreloadAmount;
        public override ISFConfigNode[] Children => null;
    }
}