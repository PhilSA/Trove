using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Trove.Audio.FMOD
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor, WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class FMODUpdateSystemGroup : ComponentSystemGroup
    { }
}