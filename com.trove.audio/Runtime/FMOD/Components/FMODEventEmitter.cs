using FMOD;
using FMODUnity;
using FMOD.Studio;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace Trove.Audio.FMOD
{
    public enum EmitterControlEventType
    {
        Play,
        Stop,
        Pause,
        Resume,
    }
    
    public struct FMODEventEmitter : IComponentData
    {
        public global::FMOD.GUID EventGUID;
        
        public bool OverrideAttenuation;
        public float OverrideMinDistance;
        public float OverrideMaxDistance;
        
        internal bool Preload;
        internal bool AllowFadeout;
        internal bool TriggerOnce;
        internal bool NonRigidbodyVelocity;
    }

    public struct FMODEmitterPlayProperties : IComponentData
    {
        public bool PlayOnCreated;
        public bool StopOnDestroyed;
        public bool PlayOnEnabled;
        public bool StopOnDisabled;
    }

    internal struct IsEnabledEmitter : IComponentData, IEnableableComponent
    {
    }

    public struct IsActiveEmitterToStopOutsideOfMaxDistance : IComponentData, IEnableableComponent
    {
    }

    public struct FMODEmitterPlayStateUpdate : IComponentData, IEnableableComponent
    { }

    public struct FMODEventParameter : IBufferElementData
    {
        public FixedString128Bytes Name;
        public PARAMETER_ID ID;
        public float Value;
    }

    public struct FMODEventEmitterState : ICleanupComponentData
    { 
        internal EmitterControlEventType PlayStateEventType;
        
        internal bool Preload;
        internal bool TriggerOnce;
        internal bool AllowFadeout;
        
        internal bool OverrideAttenuation;
        internal float OverrideMinDistance;
        internal float OverrideMaxDistance;
        
        internal bool IsOneShot;
        internal bool HasTriggered;
        internal float3 PreviousPosition;
        
        internal bool StopOnDestroyed;
        
        internal EventInstance _eventInstance;
        internal EventDescription _eventDescription;

        public EventInstance EventInstance => _eventInstance;
        public EventDescription EventDescription => _eventDescription;

        internal void UpdateFrom(in FMODEventEmitter emitter, in FMODEmitterPlayProperties playProperties, in LocalToWorld ltw)
        {
            Preload = emitter.Preload;
            AllowFadeout = emitter.AllowFadeout;
            TriggerOnce = emitter.TriggerOnce;
            PreviousPosition = ltw.Position;

            StopOnDestroyed = playProperties.StopOnDestroyed;
            
            OverrideAttenuation = emitter.OverrideAttenuation;
            OverrideMinDistance = emitter.OverrideMinDistance;
            OverrideMaxDistance = emitter.OverrideMaxDistance;
        }
    }
}
