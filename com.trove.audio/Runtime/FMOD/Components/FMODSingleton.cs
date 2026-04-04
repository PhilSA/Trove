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
        public struct ListenerData
        {
            public Entity Entity;
            public float3 Position;
            
            public Entity AttenuationEntity;
            public float3 AttenuationPosition;
        }
        
        [NativeDisableUnsafePtrRestriction]
        public global::FMOD.Studio.System StudioSystem;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeHashMap<global::FMOD.GUID, global::FMOD.Studio.EventDescription>* CachedEventDescriptions;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList<ListenerData>* ActiveListenerDatas;

        // Settings
        public bool StopEventsOutsideMaxDistance;
    }
}