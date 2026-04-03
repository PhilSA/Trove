using System.Runtime.CompilerServices;
using FMOD;
using FMODUnity;
using FMOD.Studio;
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
    public static class FMODDotsUtility
    {
        public static bool IsPlaying(in FMODEventEmitterState eventEmitterState)
        {
            if (eventEmitterState.EventInstance.isValid())
            {
                global::FMOD.Studio.PLAYBACK_STATE playbackState;
                eventEmitterState.EventInstance.getPlaybackState(out playbackState);
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
            playStateUpdate.ValueRW = true;
        }

        public static bool GetParameterIndex(
            global::FMOD.Studio.PARAMETER_ID id, 
            ref DynamicBuffer<FMODEventParameter> parameters,
            out int parameterIndex)
        {
            for (int i = 0; i < parameters.Length; ++i)
            {
                FMODEventParameter parameter = parameters[i];
                if (parameter.ID.data1 == id.data1 && parameter.ID.data2 == id.data2)
                {
                    parameterIndex = i;
                    return true;
                }
            }

            parameterIndex = -1;
            return false;
        }

        public static void SetParameter(
            global::FMOD.Studio.PARAMETER_ID id, 
            float value,
            in FMODEventEmitterState eventEmitterState, 
            ref DynamicBuffer<FMODEventParameter> parameters,
            bool ignoreSeekSpeed = false)
        {
            for (int i = 0; i < parameters.Length; ++i)
            {
                FMODEventParameter parameter = parameters[i];
                if (parameter.ID.data1 == id.data1 && parameter.ID.data2 == id.data2)
                {
                    parameter.Value = value;
                    parameters[i] = parameter;

                    if (eventEmitterState.EventInstance.isValid())
                    {
                        eventEmitterState.EventInstance.setParameterByID(parameter.ID, value, ignoreSeekSpeed);
                    }

                    return;
                }
            }
        }

        public static void SetParameter(
            FixedString128Bytes name, 
            float value,
            in FMODEventEmitterState eventEmitterState, 
            ref DynamicBuffer<FMODEventParameter> parameters,
            bool ignoreSeekSpeed = false)
        {
            for (int i = 0; i < parameters.Length; ++i)
            {
                FMODEventParameter parameter = parameters[i];
                if (parameter.Name == name)
                {
                    parameter.Value = value;
                    parameters[i] = parameter;

                    if (eventEmitterState.EventInstance.isValid())
                    {
                        eventEmitterState.EventInstance.setParameterByID(parameter.ID, value, ignoreSeekSpeed);
                    }

                    return;
                }
            }
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

        internal static global::FMOD.Studio.EventDescription LoadEventFromGUID(
            ref FMODSingleton singleton,
            in global::FMOD.GUID eventGUID,
            ref DynamicBuffer<FMODEventParameter> parameters)
        {
            global::FMOD.Studio.EventDescription eventDescription = 
                FMODDotsUtility.GetOrCreateEventDescription(ref singleton, in eventGUID);

            if (eventDescription.isValid())
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    FMODEventParameter paremeter = parameters[i];
                    eventDescription.getParameterDescriptionByName(paremeter.Name.ConvertToString(), out global::FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription);
                    paremeter.ID = parameterDescription.id;
                    parameters[i] = paremeter;
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

                if (result != RESULT.OK)
                {
                    throw new EventNotFoundException(eventGUID);
                }

                if (eventDescription.isValid())
                {
                    singleton.CachedEventDescriptions->TryAdd(eventGUID, eventDescription);
                }
            }
            return eventDescription;
        }

        internal static float GetMaxDistance(
            in FMODEventEmitterState eventEmitterState, 
            EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest)
        {
            if (eventEmitterState.OverrideAttenuation)
            {
                return eventEmitterState.OverrideMaxDistance;
            }

            if (!eventEmitterState.EventDescription.isValid())
            {
                loadEventDescriptionRequest.ValueRW = true;
            }

            float minDistance, maxDistance;
            eventEmitterState.EventDescription.getMinMaxDistance(out minDistance, out maxDistance);
            return maxDistance;
        }

        internal static void UpdatePlayingStatus(
            in FMODSingleton singleton,
            ref FMODEventEmitterState eventEmitterState, 
            EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest,
            ref DynamicBuffer<FMODEventParameter> parameters, 
            in LocalToWorld ltw, 
            float3 velocity,
            bool force = false)
        {
            // If at least one listener is within the max distance, ensure an event instance is playing
            float maxDistance = GetMaxDistance(in eventEmitterState, loadEventDescriptionRequest);
            bool playInstance = DistanceSquaredToNearestListener(in singleton, in ltw) <= (maxDistance * maxDistance);
            
            
            if (force || playInstance != IsPlaying(in eventEmitterState))
            {
                if (playInstance)
                {
                    PlayInstance(ref eventEmitterState,
                        ref parameters,
                        in ltw,
                        velocity);
                }
                else
                {
                    StopInstance(in eventEmitterState);
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

        internal static void PlayInstance(
            ref FMODEventEmitterState eventEmitterState,
            ref DynamicBuffer<FMODEventParameter> parameters, 
            in LocalToWorld ltw, 
            float3 velocity)
        {
            if (!eventEmitterState.EventInstance.isValid())
            {
                eventEmitterState.EventInstance.clearHandle();
            }

            // Let previous oneshot instances play out
            if (eventEmitterState.IsOneShot && eventEmitterState.EventInstance.isValid())
            {
                eventEmitterState.EventInstance.release();
                eventEmitterState.EventInstance.clearHandle();
            }

            eventEmitterState.EventDescription.is3D(out bool is3D);

            if (!eventEmitterState.EventInstance.isValid())
            {
                eventEmitterState.EventDescription.createInstance(out eventEmitterState._eventInstance);

                // Only want to update if we need to set 3D attributes
                if (is3D)
                {
                    eventEmitterState.EventInstance.set3DAttributes(FMODDotsUtility.To3DAttributes(ltw, velocity));
                    
                }
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                FMODEventParameter param = parameters[i];
                eventEmitterState.EventInstance.setParameterByID(param.ID, param.Value);
            }

            if (is3D && eventEmitterState.OverrideAttenuation)
            {
                eventEmitterState.EventInstance.setProperty(global::FMOD.Studio.EVENT_PROPERTY.MINIMUM_DISTANCE, eventEmitterState.OverrideMinDistance);
                eventEmitterState.EventInstance.setProperty(global::FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, eventEmitterState.OverrideMaxDistance);
            }

            eventEmitterState.EventInstance.start();

            eventEmitterState.HasTriggered = true;
        }

        internal static void StopInstance(in FMODEventEmitterState eventEmitterState)
        {
            if (eventEmitterState.EventInstance.isValid())
            {
                eventEmitterState.EventInstance.stop(eventEmitterState.AllowFadeout
                    ? global::FMOD.Studio.STOP_MODE.ALLOWFADEOUT
                    : global::FMOD.Studio.STOP_MODE.IMMEDIATE);
                eventEmitterState.EventInstance.release();
                if (!eventEmitterState.AllowFadeout)
                {
                    eventEmitterState.EventInstance.clearHandle();
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
