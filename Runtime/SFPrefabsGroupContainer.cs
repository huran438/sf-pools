﻿using System;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    [Serializable]
    public class SFPrefabsGroupNode : SFConfigNode
    {
        public override ISFConfigNode[] Children => Prefabs;
        
        public SFPrefabNode[] Prefabs = Array.Empty<SFPrefabNode>();
    }
}