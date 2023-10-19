using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;

namespace SFramework.Pools.Runtime
{
    public class SFPrefabAttribute : SFIdAttribute
    {
        public SFPrefabAttribute() : base(typeof(SFPoolsConfig) ,-1)
        {
        }
    }
}