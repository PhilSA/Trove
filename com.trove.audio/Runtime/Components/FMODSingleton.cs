using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTSFMOD
{
    public struct FMODSingleton : IComponentData
    {
        [NativeDisableUnsafePtrRestriction]
        public FMOD.Studio.System StudioSystem;
    }
}