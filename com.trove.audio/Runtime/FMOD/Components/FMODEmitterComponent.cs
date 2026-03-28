using FMOD;
using FMOD.Studio;
using Unity.Entities;
using Unity.Collections;

namespace Trove.Audio.FMOD
{
    /// <summary>
    /// Core emitter component that defines which FMOD event to play and its configuration.
    /// Replaces StudioEventEmitter's serialized fields.
    /// </summary>
    public struct FMODEmitterComponent : IComponentData
    {
        public GUID EventGuid;
        
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

    /// <summary>
    /// Runtime state for an active FMOD emitter. Tracks the native event instance and playback state.
    /// </summary>
    public struct FMODEmitterState : IComponentData
    {
        public ulong InstanceHandle;
        public bool HasTriggered;
        public bool IsOneshot;
        public bool IsActive;
        public bool DescriptionCached;
        public float MaxDistance;
    }

    /// <summary>
    /// Enable/disable tag to request an emitter to start playing.
    /// Add or enable this component to trigger playback.
    /// </summary>
    public struct FMODPlayRequest : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Enable/disable tag to request an emitter to stop playing.
    /// Add or enable this component to trigger stop.
    /// </summary>
    public struct FMODStopRequest : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Buffer element storing a parameter name/value pair to apply to an emitter's event instance.
    /// </summary>
    public struct FMODEmitterParameterElement : IBufferElementData
    {
        public FixedString128Bytes Name;
        public float Value;
        public PARAMETER_ID ID;
        public bool IDCached;
    }
}
