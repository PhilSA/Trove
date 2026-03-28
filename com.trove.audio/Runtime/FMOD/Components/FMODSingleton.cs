using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Trove.Audio.FMOD
{
    public struct FMODSingleton : IComponentData
    {
        [NativeDisableUnsafePtrRestriction]
        public global::FMOD.Studio.System StudioSystem;
    }
}