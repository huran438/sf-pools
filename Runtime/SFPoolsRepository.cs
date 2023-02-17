using System;
using System.Collections.Generic;
using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    public class SFPoolsRepository : SFRepository
    {
        public override ISFNode[] Nodes => Groups;
        public SFPrefabsGroupContainer[] Groups = Array.Empty<SFPrefabsGroupContainer>();
    }
}