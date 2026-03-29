using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using FMOD;
using FMODUnity;
using FMOD.Studio;
using Unity.Collections;

namespace Trove.Audio.FMOD
{
    public unsafe struct FMODSingleton : IComponentData
    {
        [NativeDisableUnsafePtrRestriction]
        public global::FMOD.Studio.System StudioSystem;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeHashMap<global::FMOD.GUID, global::FMOD.Studio.EventDescription>* CachedEventDescriptions;

        // Settings
        public bool StopEventsOutsideMaxDistance;
    }
}