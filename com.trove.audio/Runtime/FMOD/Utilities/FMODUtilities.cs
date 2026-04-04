using System;
using System.Runtime.CompilerServices;
using FMOD;
using FMODUnity;
using FMOD.Studio;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Trove.Audio.FMOD
{
    /// <summary>
    /// Utility methods for converting ECS transform data to FMOD 3D attributes.
    /// </summary>
    public static class FMODUtilities
    {
        public static bool IsPlaying(EventInstance eventInstance)
        {
            if (eventInstance.isValid())
            {
                global::FMOD.Studio.PLAYBACK_STATE playbackState;
                eventInstance.getPlaybackState(out playbackState);
                return (playbackState != global::FMOD.Studio.PLAYBACK_STATE.STOPPED);
            }
            return false;
        }
        
        public static void Play(Entity entity, EntityManager entityManager)
        {
            if (entityManager.HasComponent<FMODEmitterPlayStateUpdate>(entity) &&
                entityManager.HasComponent<FMODEventEmitterState>(entity))
            {
                FMODEventEmitterState emitterState = entityManager.GetComponentData<FMODEventEmitterState>(entity);
                emitterState.PlayStateEventType = EmitterControlEventType.Play;
                entityManager.SetComponentData(entity, emitterState);
                
                entityManager.SetComponentEnabled<FMODEmitterPlayStateUpdate>(entity, true);
            }
        }
        
        public static void Play(Entity entity, 
            ref ComponentLookup<FMODEventEmitterState> emitterStateLookup, 
            ref ComponentLookup<FMODEmitterPlayStateUpdate> playStateUpdateLookup)
        {
            if (playStateUpdateLookup.HasComponent(entity) &&
                emitterStateLookup.TryGetRefRW(entity, out RefRW<FMODEventEmitterState> emitterState))
            {
                emitterState.ValueRW.PlayStateEventType = EmitterControlEventType.Play;
                playStateUpdateLookup.SetComponentEnabled(entity, true);   
            }
        }
        
        public static void Play(ref FMODEventEmitterState emitterState, EnabledRefRW<FMODEmitterPlayStateUpdate> playStateUpdate)
        {
            emitterState.PlayStateEventType = EmitterControlEventType.Play;
            playStateUpdate.ValueRW = true;
        }
        
        public static void Stop(Entity entity, EntityManager entityManager)
        {
            if (entityManager.HasComponent<FMODEmitterPlayStateUpdate>(entity) &&
                entityManager.HasComponent<FMODEventEmitterState>(entity))
            {
                FMODEventEmitterState emitterState = entityManager.GetComponentData<FMODEventEmitterState>(entity);
                emitterState.PlayStateEventType = EmitterControlEventType.Stop;
                entityManager.SetComponentData(entity, emitterState);
                
                entityManager.SetComponentEnabled<FMODEmitterPlayStateUpdate>(entity, true);
            }
        }
        
        public static void Stop(Entity entity, 
            ref ComponentLookup<FMODEventEmitterState> emitterStateLookup, 
            ref ComponentLookup<FMODEmitterPlayStateUpdate> playStateUpdateLookup)
        {
            if (playStateUpdateLookup.HasComponent(entity) &&
                emitterStateLookup.TryGetRefRW(entity, out RefRW<FMODEventEmitterState> emitterState))
            {
                emitterState.ValueRW.PlayStateEventType = EmitterControlEventType.Stop;
                playStateUpdateLookup.SetComponentEnabled(entity, true);   
            }
        }
        
        public static void Stop(ref FMODEventEmitterState emitterState, EnabledRefRW<FMODEmitterPlayStateUpdate> playStateUpdate)
        {
            emitterState.PlayStateEventType = EmitterControlEventType.Stop;
            if(playStateUpdate.IsValid)
            {
                playStateUpdate.ValueRW = true;
            }
        }

        public static bool GetParameter(
            FixedString128Bytes name, 
            ref DynamicBuffer<FMODEventParameter> parameters,
            out FMODEventParameter parameter,
            out int parameterIndex)
        {
            for (int i = 0; i < parameters.Length; ++i)
            {
                parameter = parameters[i];
                if (parameter.Name == name)
                {
                    parameterIndex = i;
                    return true;
                }
            }

            parameter = default;
            parameterIndex = -1;
            return false;
        }

        public static bool GetParameter(
            Entity emitterEntity,
            FixedString128Bytes name, 
            ref BufferLookup<FMODEventParameter> parametersBufferLookup,
            out FMODEventParameter parameter,
            out int parameterIndex)
        {
            if (parametersBufferLookup.TryGetBuffer(emitterEntity,
                    out DynamicBuffer<FMODEventParameter> parametersBuffer))
            {
                for (int i = 0; i < parametersBuffer.Length; ++i)
                {
                    parameter = parametersBuffer[i];
                    if (parameter.Name == name)
                    {
                        parameterIndex = i;
                        return true;
                    }
                }
            }

            parameter = default;
            parameterIndex = -1;
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void SetParameter(
            int parameterIndex,
            float value,
            bool ignoreSeekSpeed,
            EventInstance eventInstance, 
            ref DynamicBuffer<FMODEventParameter> parametersBuffer)
        {
            if (parameterIndex >= 0 && parametersBuffer.Length > parameterIndex)
            {
                FMODEventParameter parameter = parametersBuffer[parameterIndex];
                parameter.Value = value;
                parametersBuffer[parameterIndex] = parameter;
            
                FMODExternalMethods.FMOD_Studio_EventInstance_SetParametersByIDs(
                    eventInstance.handle,
                    (IntPtr)(&parameter.ID), 
                    (IntPtr)(&parameter.Value), 
                    1, 
                    ignoreSeekSpeed);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void SetParameter(
            FixedString128Bytes name, 
            float value,
            bool ignoreSeekSpeed,
            EventInstance eventInstance, 
            ref DynamicBuffer<FMODEventParameter> parametersBuffer)
        {
            if (GetParameter(name, ref parametersBuffer, out FMODEventParameter parameter, out int parameterIndex))
            {
                parameter.Value = value;
                parametersBuffer[parameterIndex] = parameter;
            
                FMODExternalMethods.FMOD_Studio_EventInstance_SetParametersByIDs(
                    eventInstance.handle,
                    (IntPtr)(&parameter.ID), 
                    (IntPtr)(&parameter.Value), 
                    1, 
                    ignoreSeekSpeed);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void SetParameter(
            int parameterIndex,
            float value,
            bool ignoreSeekSpeed,
            Entity emitterEntity, 
            in ComponentLookup<FMODEventEmitterState> emitterStateLookup,
            ref BufferLookup<FMODEventParameter> parametersBufferLookup)
        {
            if (emitterStateLookup.TryGetComponent(emitterEntity, out FMODEventEmitterState emitterState) &&
                parametersBufferLookup.TryGetBuffer(emitterEntity, out DynamicBuffer<FMODEventParameter> parametersBuffer))
            {
                if (parameterIndex >= 0 && parametersBuffer.Length > parameterIndex)
                {
                    FMODEventParameter parameter = parametersBuffer[parameterIndex];
                    parameter.Value = value;
                    parametersBuffer[parameterIndex] = parameter;

                    FMODExternalMethods.FMOD_Studio_EventInstance_SetParametersByIDs(
                        emitterState.EventInstance.handle,
                        (IntPtr)(&parameter.ID),
                        (IntPtr)(&parameter.Value),
                        1,
                        ignoreSeekSpeed);
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void SetParameter(
            FixedString128Bytes name, 
            float value,
            bool ignoreSeekSpeed,
            Entity emitterEntity, 
            in ComponentLookup<FMODEventEmitterState> emitterStateLookup,
            ref BufferLookup<FMODEventParameter> parametersBufferLookup)
        {
            if (emitterStateLookup.TryGetComponent(emitterEntity, out FMODEventEmitterState emitterState) &&
                parametersBufferLookup.TryGetBuffer(emitterEntity, out DynamicBuffer<FMODEventParameter> parametersBuffer))
            {
                if (GetParameter(name, ref parametersBuffer, out FMODEventParameter parameter, out int parameterIndex))
                {
                    parameter.Value = value;
                    parametersBuffer[parameterIndex] = parameter;

                    FMODExternalMethods.FMOD_Studio_EventInstance_SetParametersByIDs(
                        emitterState.EventInstance.handle,
                        (IntPtr)(&parameter.ID),
                        (IntPtr)(&parameter.Value),
                        1,
                        ignoreSeekSpeed);
                }
            }
        }

        internal static void HandlePlay(
            Entity entity, 
            ref FMODSingleton singleton,
            in FMODEventEmitter emitter,
            ref FMODEventEmitterState eventEmitterState,
            ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
            in LocalToWorld ltw,
            ref ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> isActiveEmitterToStopOutsideOfMaxDistanceLookup)
        {
            if (eventEmitterState.TriggerOnce && eventEmitterState.HasTriggered)
            {
                return;
            }

            if (!eventEmitterState.EventDescription.isValid())
            {
                eventEmitterState._eventDescription =
                    FMODUtilities.LoadEventFromGUID(ref singleton, in emitter.EventGUID, ref eventEmitterParameters);
                eventEmitterState.EventDescription.loadSampleData();
            }

            eventEmitterState.EventDescription.isSnapshot(out bool isSnapshot);

            if (!isSnapshot)
            {
                eventEmitterState.EventDescription.isOneshot(out eventEmitterState.IsOneShot);
            }

            eventEmitterState.EventDescription.is3D(out bool is3D);

            if (is3D && singleton.StopEventsOutsideMaxDistance)
            {
                if (!eventEmitterState.IsOneShot)
                {
                    isActiveEmitterToStopOutsideOfMaxDistanceLookup.SetComponentEnabled(entity, true);
                }
                
                FMODUtilities.UpdatePlayingStatus(
                    entity,
                    ref isActiveEmitterToStopOutsideOfMaxDistanceLookup,
                    ref singleton,
                    in emitter.EventGUID,
                    ref eventEmitterState, 
                    ref eventEmitterParameters,
                    in ltw,
                    true);
            }
            else
            {
                FMODUtilities.PlayInstance(
                    ref eventEmitterState,
                    ref eventEmitterParameters,
                    in ltw);
            }
        }

        internal static void HandleStop(
            Entity entity, 
            ref FMODEventEmitterState eventEmitterState,
            ref ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> isActiveEmitterLookupToStopOutsideOfMaxDistance)
        {
            if (isActiveEmitterLookupToStopOutsideOfMaxDistance.HasComponent(entity))
            {
                isActiveEmitterLookupToStopOutsideOfMaxDistance.SetComponentEnabled(entity, false);
            }

            FMODUtilities.StopInstance(entity, in eventEmitterState, ref isActiveEmitterLookupToStopOutsideOfMaxDistance);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VECTOR ToFMODVector(float3 v)
        {
            return new VECTOR { x = v.x, y = v.y, z = v.z };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ATTRIBUTES_3D To3DAttributes(in LocalToWorld ltw, float3 velocity)
        {
            return new ATTRIBUTES_3D
            {
                position = ToFMODVector(ltw.Position),
                velocity = ToFMODVector(velocity),
                forward = ToFMODVector(ltw.Forward),
                up = ToFMODVector(ltw.Up)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ATTRIBUTES_3D To3DAttributes(in LocalTransform transform, float3 velocity)
        {
            return new ATTRIBUTES_3D
            {
                position = ToFMODVector(transform.Position),
                velocity = ToFMODVector(velocity),
                forward = ToFMODVector(math.mul(transform.Rotation, math.forward())),
                up = ToFMODVector(math.mul(transform.Rotation, math.up()))
            };
        }

        internal unsafe static global::FMOD.Studio.EventDescription LoadEventFromGUID(
            ref FMODSingleton singleton,
            in global::FMOD.GUID eventGUID,
            ref DynamicBuffer<FMODEventParameter> parameters)
        {
            global::FMOD.Studio.EventDescription eventDescription = 
                FMODUtilities.GetOrCreateEventDescription(ref singleton, in eventGUID);

            if (eventDescription.isValid())
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    FMODEventParameter parameter = parameters[i];
                    FMODExternalMethods.FMOD_Studio_EventDescription_GetParameterDescriptionByName(
                        eventDescription.handle, 
                        (IntPtr)parameter.Name.GetUnsafePtr(),
                        out global::FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription);
                    parameter.ID = parameterDescription.id;
                    parameters[i] = parameter;
                }
            }
            
            return eventDescription;
        }

        internal unsafe static global::FMOD.Studio.EventDescription GetOrCreateEventDescription(ref FMODSingleton singleton, in global::FMOD.GUID eventGUID)
        {
            global::FMOD.Studio.EventDescription eventDescription;
            if (singleton.CachedEventDescriptions->TryGetValue(eventGUID, out eventDescription) && eventDescription.isValid())
            {
            }
            else
            {
                RESULT result = singleton.StudioSystem.getEventByID(eventGUID, out eventDescription);

                if (result == RESULT.OK)
                {
                    if (eventDescription.isValid())
                    {
                        singleton.CachedEventDescriptions->TryAdd(eventGUID, eventDescription);
                    }
                }
            }
            return eventDescription;
        }

        internal static float GetMaxDistance(
            ref FMODSingleton singleton,
            in global::FMOD.GUID eventGUID,
            ref FMODEventEmitterState eventEmitterState,
            ref DynamicBuffer<FMODEventParameter> parameters)
        {
            if (eventEmitterState.OverrideAttenuation)
            {
                return eventEmitterState.OverrideMaxDistance;
            }

            if (!eventEmitterState.EventDescription.isValid())
            {
                eventEmitterState._eventDescription =
                    FMODUtilities.LoadEventFromGUID(ref singleton, in eventGUID, ref parameters);
                eventEmitterState.EventDescription.loadSampleData();
            }

            float minDistance, maxDistance;
            eventEmitterState.EventDescription.getMinMaxDistance(out minDistance, out maxDistance);
            return maxDistance;
        }

        internal static void UpdatePlayingStatus(
            Entity entity,
            ref ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> isActiveEmitterToStopOutsideOfMaxDistanceLookup,
            ref FMODSingleton singleton,
            in global::FMOD.GUID eventGUID,
            ref FMODEventEmitterState eventEmitterState, 
            ref DynamicBuffer<FMODEventParameter> parameters, 
            in LocalToWorld ltw, 
            bool force = false)
        {
            // If at least one listener is within the max distance, ensure an event instance is playing
            float maxDistance = GetMaxDistance(ref singleton, in eventGUID, ref eventEmitterState, ref parameters);
            bool playInstance = DistanceSquaredToNearestListener(in singleton, in ltw) <= (maxDistance * maxDistance);
            
            if (force || playInstance != IsPlaying(eventEmitterState._eventInstance))
            {
                if (playInstance)
                {
                    PlayInstance(ref eventEmitterState,
                        ref parameters,
                        in ltw);
                }
                else
                {
                    StopInstance(entity, in eventEmitterState, ref isActiveEmitterToStopOutsideOfMaxDistanceLookup);
                }
            }
        }

        internal unsafe static float DistanceSquaredToNearestListener(
            in FMODSingleton singleton,
            in LocalToWorld ltw)
        {
            float result = float.MaxValue;
            UnsafeList<FMODSingleton.ListenerData> listenerDatas = *singleton.ActiveListenerDatas;
            for (int i = 0; i < singleton.ActiveListenerDatas->Length; i++)
            {
                if (listenerDatas[i].AttenuationEntity == Entity.Null)
                {
                    result = math.min(result, math.lengthsq(ltw.Position - listenerDatas[i].Position));
                }
                else
                {
                    result = math.min(result, math.lengthsq(ltw.Position - listenerDatas[i].AttenuationPosition));
                }
            }
            return result;
        }

        internal unsafe static void PlayInstance(
            ref FMODEventEmitterState eventEmitterState,
            ref DynamicBuffer<FMODEventParameter> parameters, 
            in LocalToWorld ltw)
        {
            if (!eventEmitterState._eventInstance.isValid())
            {
                eventEmitterState._eventInstance.clearHandle();
            }

            // Let previous oneshot instances play out
            if (eventEmitterState.IsOneShot && eventEmitterState._eventInstance.isValid())
            {
                eventEmitterState._eventInstance.release();
                eventEmitterState._eventInstance.clearHandle();
            }

            eventEmitterState.EventDescription.is3D(out bool is3D);

            if (!eventEmitterState._eventInstance.isValid())
            {
                eventEmitterState.EventDescription.createInstance(out eventEmitterState._eventInstance);

                if (is3D)
                {
                    eventEmitterState._eventInstance.set3DAttributes(FMODUtilities.To3DAttributes(ltw, default));
                }
            }

            // Set parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                FMODEventParameter param = parameters[i];
                FMODExternalMethods.FMOD_Studio_EventInstance_SetParametersByIDs(
                    eventEmitterState._eventInstance.handle,
                    (IntPtr)(&param.ID), 
                    (IntPtr)(&param.Value), 
                    1, 
                    false);
            }

            if (is3D && eventEmitterState.OverrideAttenuation)
            {
                eventEmitterState._eventInstance.setProperty(global::FMOD.Studio.EVENT_PROPERTY.MINIMUM_DISTANCE, eventEmitterState.OverrideMinDistance);
                eventEmitterState._eventInstance.setProperty(global::FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, eventEmitterState.OverrideMaxDistance);
            }

            eventEmitterState._eventInstance.start();

            eventEmitterState.HasTriggered = true;
        }

        internal static void StopInstance(
            Entity entity,
            in FMODEventEmitterState eventEmitterState,
            ref ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> isActiveEmitterToStopOutsideOfMaxDistanceLookup)
        {
            if (eventEmitterState.TriggerOnce && eventEmitterState.HasTriggered)
            {
                isActiveEmitterToStopOutsideOfMaxDistanceLookup.SetComponentEnabled(entity, false);
            }

            if (eventEmitterState._eventInstance.isValid())
            {
                eventEmitterState._eventInstance.stop(eventEmitterState.AllowFadeout
                    ? global::FMOD.Studio.STOP_MODE.ALLOWFADEOUT
                    : global::FMOD.Studio.STOP_MODE.IMMEDIATE);
                eventEmitterState._eventInstance.release();
                if (!eventEmitterState.AllowFadeout)
                {
                    eventEmitterState._eventInstance.clearHandle();
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ClampToMaxLength(float3 vector, float maxLength)
        {
            float sqrmag = math.lengthsq(vector);
            if (sqrmag > maxLength * maxLength)
            {
                float mag = math.sqrt(sqrmag);
                float normalized_x = vector.x / mag;
                float normalized_y = vector.y / mag;
                float normalized_z = vector.z / mag;
                return new float3(normalized_x * maxLength,
                    normalized_y * maxLength,
                    normalized_z * maxLength);
            }

            return vector;
        }
    }
}
