using System;
using System.Collections.Generic;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    public class SFPoolsConfig : SFNodesConfig
    {
        public override ISFConfigNode[] Children => Groups;
        public SFPrefabsGroupNode[] Groups = Array.Empty<SFPrefabsGroupNode>();
    }
}