using System;
using System.Collections.Generic;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    public class SFPoolsConfig : SFConfig, ISFConfigsGenerator
    {
        public override ISFConfigNode[] Nodes => Groups;
        public SFPrefabsGroupNode[] Groups = Array.Empty<SFPrefabsGroupNode>();

        public void GetGenerationData(out SFGenerationData[] generationData)
        {
            var pools = new HashSet<string>();
            var prefabs = new HashSet<string>();

            foreach (var layer0 in Groups)
            {
                pools.Add($"{Name}/{layer0.Name}");
                foreach (var layer1 in layer0.Prefabs)
                {
                    prefabs.Add($"{Name}/{layer0.Name}/{layer1.Name}");
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