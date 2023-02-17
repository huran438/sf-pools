using System;
using System.Collections.Generic;
using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    public class SFPoolsRepository : SFRepository, ISFRepositoryGenerator
    {
        public override ISFNode[] Nodes => Groups;
        public SFPrefabsGroupContainer[] Groups = Array.Empty<SFPrefabsGroupContainer>();

        public void GetGenerationData(out SFGenerationData[] generationData)
        {
            var pools = new HashSet<string>();
            var prefabs = new HashSet<string>();

            foreach (var layer0 in Groups)
            {
                pools.Add($"{_Name}/{layer0._Name}");
                foreach (var layer1 in layer0.Prefabs)
                {
                    prefabs.Add($"{_Name}/{layer0._Name}/{layer1._Name}");
                }
            }

            generationData = new[]
            {
                new SFGenerationData
                {
                    FileName = "SFPrefabs",
                    Properties = prefabs
                },
                new SFGenerationData
                {
                    FileName = "SFPools",
                    Properties = pools
                }
            };
        }
    }
}