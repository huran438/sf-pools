using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;

namespace SFramework.Pools.Runtime
{
    public class SFPrefabAttribute : SFIdAttribute
    {
        public SFPrefabAttribute() : base(typeof(SFPoolsRepository) ,-1)
        {
        }
    }
}