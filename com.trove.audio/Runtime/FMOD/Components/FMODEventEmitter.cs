using FMOD;
using FMODUnity;
using FMOD.Studio;
using Unity.Entities;
using Unity.Collections;

namespace Trove.Audio.FMOD
{
    public struct FMODEventEmitter : IComponentData
    {
        public global::FMOD.GUID EventGUID;
        
        public bool PlayOnCreated;
        public bool PlayOnEnabled;
        public bool StopOnDestroyed;
        public bool StopOnDisabled;
        
        public bool OverrideAttenuation;
        public float OverrideMinDistance;
        public float OverrideMaxDistance;
        
        public bool Preload;
        public bool AllowFadeout;
        public bool TriggerOnce;
    }

    public struct FMODEmitterState : IComponentData
    {
        public ulong InstanceHandle;
        public bool HasTriggered;
        public bool IsOneshot;
        public bool IsActive;
        public bool DescriptionCached;
        public float MaxDistance;
    }

    public struct LoadEventDescriptionRequest : IComponentData, IEnableableComponent
    {
    }

    public struct FMODPlayRequest : IComponentData, IEnableableComponent
    {
    }

    public struct FMODStopRequest : IComponentData, IEnableableComponent
    {
    }

    public struct FMODEmitterParameterElement : IBufferElementData
    {
        public FixedString128Bytes Name;
        public float Value;
        
        public bool IDCached;
        public PARAMETER_ID CachedID;
    }

    public struct FMODEventEmitterCleanup : ICleanupComponentData
    { 
        public bool StopOnDestroyed;
        public bool AllowFadeout;
        
        public EventInstance EventInstance;
        public EventDescription EventDescription;
    }
}
