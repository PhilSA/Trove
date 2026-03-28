using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Trove.Audio.FMOD
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    public partial class FMODBeginSystemGroup : ComponentSystemGroup
    { }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial class FMODUpdateSystemGroup : ComponentSystemGroup
    { }
}