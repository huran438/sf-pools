using System;
using System.Collections.Generic;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Pools.Runtime
{
    public class SFPoolsConfig : SFConfig, ISFConfigsGenerator
    {
        public override ISFConfigNode[] Children => Groups;
        public SFPrefabsGroupNode[] Groups = Array.Empty<SFPrefabsGroupNode>();

        public void GetGenerationData(out SFGenerationData[] generationData)
        {
            var pools = new HashSet<string>();
            var prefabs = new HashSet<string>();

            foreach (var layer0 in Groups)
            {
                pools.Add($"{Id}/{layer0.Id}");
                foreach (var layer1 in layer0.Prefabs)
                {
                    prefabs.Add($"{Id}/{layer0.Id}/{layer1.Id}");
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