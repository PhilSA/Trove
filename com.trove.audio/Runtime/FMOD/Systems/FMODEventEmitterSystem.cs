using System;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Trove.Audio.FMOD
{
    [UpdateInGroup(typeof(FMODUpdateSystemGroup))]
    [UpdateAfter(typeof(FMODListenerSystem))]
    public partial struct FMODEventEmitterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FMODSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityQuery singletonQuery = SystemAPI.QueryBuilder().WithAll<FMODSingleton>().Build();
            singletonQuery.CompleteDependency();
            
            FMODSingleton singleton = SystemAPI.GetSingletonRW<FMODSingleton>().ValueRW;
            if (!singleton.StudioSystem.isValid())
                return;
            
            Entity singletonEntity = SystemAPI.GetSingletonEntity<FMODSingleton>();
            ComponentLookup<FMODSingleton> singletonLookup = SystemAPI.GetComponentLookup<FMODSingleton>(false);
            ComponentLookup<FMODEmitterPlayStateUpdate> playStateUpdateLookup = SystemAPI.GetComponentLookup<FMODEmitterPlayStateUpdate>(false);
            ComponentLookup<IsEnabledEmitter> isEnabledEmitterLookup = SystemAPI.GetComponentLookup<IsEnabledEmitter>(false);
            ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> isActiveEnitterToStopOutsideOfMaxDistanceLookup = SystemAPI.GetComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance>(false);
            ComponentLookup<FMODEmitterPlayProperties> emitterPlayPropertiesLookup = SystemAPI.GetComponentLookup<FMODEmitterPlayProperties>(false);

            state.Dependency = new EventEmittersStartJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
                PlayStateUpdateLookup = playStateUpdateLookup,
                EmitterPlayPropertiesLookup = emitterPlayPropertiesLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new EventEmittersDestroyJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginPresentationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                IsActiveEmitterToStopOutsideOfMaxDistanceLookup = isActiveEnitterToStopOutsideOfMaxDistanceLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new EventEmittersEnableJob
            {
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
                IsActiveEmitterToStopOutsideOfMaxDistanceLookup = isActiveEnitterToStopOutsideOfMaxDistanceLookup,
                IsEnabledEmitterLookup = isEnabledEmitterLookup,
                EmitterPlayPropertiesLookup = emitterPlayPropertiesLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new EventEmittersDisableJob
            {
                IsActiveEmitterToStopOutsideOfMaxDistanceLookup = isActiveEnitterToStopOutsideOfMaxDistanceLookup,
                EmitterPlayPropertiesLookup = emitterPlayPropertiesLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new PlayEventEmittersJob
            {
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
                PlayStateUpdateLookup = playStateUpdateLookup,
                IsActiveEmitterToStopOutsideOfMaxDistanceLookup = isActiveEnitterToStopOutsideOfMaxDistanceLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new UpdateActiveEventEmittersToStopOutsideOfMaxDistanceJob
            {
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
                IsActiveEmitterToStopOutsideOfMaxDistanceLookup = isActiveEnitterToStopOutsideOfMaxDistanceLookup,
            }.ScheduleParallel(state.Dependency); 
            
            state.Dependency = new UpdateEventEmitters3DAttributesJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency); 

#if UNITY_PHYSICS_PRESENT
            state.Dependency = new UpdatePhysicsEventEmitters3DAttributesJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
#endif
        }

        [BurstCompile]
        [WithNone(typeof(FMODEventEmitterState))]
        [WithPresent(typeof(FMODEmitterPlayStateUpdate))]
        public partial struct EventEmittersStartJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public EntityCommandBuffer ECB;
            public Entity SingletonEntity;
            public ComponentLookup<FMODSingleton> SingletonLookup;
            public ComponentLookup<FMODEmitterPlayStateUpdate> PlayStateUpdateLookup;
            public ComponentLookup<FMODEmitterPlayProperties> EmitterPlayPropertiesLookup;

            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;

            public void Execute(
                Entity entity,
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw)
            {
                FMODEventEmitterState state = new FMODEventEmitterState();

                if (eventEmitter.Preload)
                {
                    state._eventDescription =
                        FMODUtilities.LoadEventFromGUID(ref _singleton, in eventEmitter.EventGUID, ref eventEmitterParameters);
                    state.EventDescription.loadSampleData();
                }

                if(EmitterPlayPropertiesLookup.TryGetComponent(entity, out FMODEmitterPlayProperties playProperties) &&
                   playProperties.PlayOnCreated)
                {
                    state.PlayStateEventType = EmitterControlEventType.Play;
                    PlayStateUpdateLookup.SetComponentEnabled(entity, true);
                }
                
                state.UpdateFrom(eventEmitter, in playProperties, in ltw);

                ECB.AddComponent(entity, state);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!_singleton.StudioSystem.isValid())
                {
                    SingletonLookup.TryGetComponent(SingletonEntity, out _singleton);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            {
            }
        }

        [BurstCompile]
        [WithNone(typeof(FMODEventEmitter))]
        public partial struct EventEmittersDestroyJob : IJobEntity
        {
            public EntityCommandBuffer ECB;
            public ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> IsActiveEmitterToStopOutsideOfMaxDistanceLookup;

            public void Execute(Entity entity,
                ref FMODEventEmitterState eventEmitterState)
            {
                if (eventEmitterState._eventInstance.isValid())
                {
                    eventEmitterState.EventDescription.isOneshot(out eventEmitterState.IsOneShot);
                    if (eventEmitterState.EventDescription.isValid() && eventEmitterState.IsOneShot)
                    {
                        eventEmitterState._eventInstance.release();
                        eventEmitterState._eventInstance.clearHandle();
                    }
                }

                if (eventEmitterState.Preload)
                {
                    eventEmitterState.EventDescription.unloadSampleData();
                }

                if(eventEmitterState.StopOnDestroyed)
                {
                    FMODUtilities.HandleStop(entity, ref eventEmitterState, ref IsActiveEmitterToStopOutsideOfMaxDistanceLookup);
                }

                ECB.RemoveComponent<FMODEventEmitterState>(entity);
            }
        }

        [BurstCompile]
        [WithNone(typeof(Disabled))]
        [WithDisabled(typeof(IsEnabledEmitter))]
        [WithPresent(typeof(FMODEmitterPlayStateUpdate))]
        internal partial struct EventEmittersEnableJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public Entity SingletonEntity;
            public ComponentLookup<FMODSingleton> SingletonLookup;
            public ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> IsActiveEmitterToStopOutsideOfMaxDistanceLookup;
            public ComponentLookup<IsEnabledEmitter> IsEnabledEmitterLookup;
            public ComponentLookup<FMODEmitterPlayProperties> EmitterPlayPropertiesLookup;

            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;
            
            internal void Execute(
                Entity entity,
                in FMODEventEmitter emitter,
                ref FMODEventEmitterState eventEmitterState,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw)
            {
                IsEnabledEmitterLookup.SetComponentEnabled(entity, true);

                if(EmitterPlayPropertiesLookup.TryGetComponent(entity, out FMODEmitterPlayProperties playProperties) &&
                   playProperties.PlayOnEnabled)
                {
                    FMODUtilities.HandlePlay(entity, ref _singleton, in emitter, ref eventEmitterState,
                        ref eventEmitterParameters, in ltw, ref IsActiveEmitterToStopOutsideOfMaxDistanceLookup);
                }
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!_singleton.StudioSystem.isValid())
                {
                    SingletonLookup.TryGetComponent(SingletonEntity, out _singleton);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            {
            }
        }

        [BurstCompile]
        [WithAll(typeof(Disabled))]
        [WithAll(typeof(IsEnabledEmitter))]
        [WithPresent(typeof(FMODEmitterPlayStateUpdate))]
        internal partial struct EventEmittersDisableJob : IJobEntity
        {
            public ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> IsActiveEmitterToStopOutsideOfMaxDistanceLookup;
            public ComponentLookup<FMODEmitterPlayProperties> EmitterPlayPropertiesLookup;
            
            internal void Execute(
                Entity entity,
                in FMODEventEmitter emitter,
                ref FMODEventEmitterState eventEmitterState,
                EnabledRefRW<IsEnabledEmitter> emitterIsEnabled)
            {
                emitterIsEnabled.ValueRW = false;

                if(EmitterPlayPropertiesLookup.TryGetComponent(entity, out FMODEmitterPlayProperties playProperties) &&
                   playProperties.StopOnDisabled)
                {
                    FMODUtilities.HandleStop(entity, ref eventEmitterState, ref IsActiveEmitterToStopOutsideOfMaxDistanceLookup);
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(FMODEmitterPlayStateUpdate))]
        public partial struct PlayEventEmittersJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public Entity SingletonEntity;
            public ComponentLookup<FMODSingleton> SingletonLookup;
            public ComponentLookup<FMODEmitterPlayStateUpdate> PlayStateUpdateLookup;
            public ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> IsActiveEmitterToStopOutsideOfMaxDistanceLookup;

            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;

            public void Execute(
                Entity entity,
                in FMODEventEmitter emitter,
                ref FMODEventEmitterState eventEmitterState,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw)
            {
                PlayStateUpdateLookup.SetComponentEnabled(entity, false);
                
                switch (eventEmitterState.PlayStateEventType)
                {
                    case EmitterControlEventType.Play:
                        FMODUtilities.HandlePlay(entity, ref _singleton, in emitter, ref eventEmitterState,
                            ref eventEmitterParameters, in ltw, ref IsActiveEmitterToStopOutsideOfMaxDistanceLookup);
                        break;
                    case EmitterControlEventType.Stop:
                        FMODUtilities.HandleStop(entity, ref eventEmitterState, ref IsActiveEmitterToStopOutsideOfMaxDistanceLookup);
                        break;
                    case EmitterControlEventType.Pause:
                        eventEmitterState._eventInstance.setPaused(true);
                        break;
                    case EmitterControlEventType.Resume:
                        eventEmitterState._eventInstance.setPaused(false);
                        break;
                }
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!_singleton.StudioSystem.isValid())
                {
                    SingletonLookup.TryGetComponent(SingletonEntity, out _singleton);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            {
            }
        }

        [BurstCompile]
        [WithAll(typeof(IsActiveEmitterToStopOutsideOfMaxDistance))]
        public partial struct UpdateActiveEventEmittersToStopOutsideOfMaxDistanceJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public Entity SingletonEntity;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<FMODSingleton> SingletonLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<IsActiveEmitterToStopOutsideOfMaxDistance> IsActiveEmitterToStopOutsideOfMaxDistanceLookup;

            [NativeDisableParallelForRestriction]
            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;
            
            public void Execute(
                Entity entity,
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                ref FMODEventEmitterState eventEmitterState)
            {
                UnityEngine.Debug.Log($"UpdateActiveEventEmittersToStopOutsideOfMaxDistanceJob {entity}");
                FMODUtilities.UpdatePlayingStatus(
                    entity,
                    ref IsActiveEmitterToStopOutsideOfMaxDistanceLookup,
                    ref _singleton,
                    in eventEmitter.EventGUID,
                    ref eventEmitterState, 
                    ref eventEmitterParameters,
                    in ltw,
                    false);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!_singleton.StudioSystem.isValid())
                {
                    SingletonLookup.TryGetComponent(SingletonEntity, out _singleton);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            {
            }
        }

        [BurstCompile]
#if UNITY_PHYSICS_PRESENT
        [WithNone(typeof(PhysicsVelocity))]
#endif
        public partial struct UpdateEventEmitters3DAttributesJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                ref FMODEventEmitterState eventEmitterState)
            {
                float3 velocity = float3.zero;
                if (DeltaTime != 0f)
                {
                    velocity = (ltw.Position - eventEmitterState.PreviousPosition) / DeltaTime;
                    velocity = FMODUtilities.ClampToMaxLength(velocity, 20f);
                }
                
                if (!ltw.Position.Equals(eventEmitterState.PreviousPosition))
                {
                    eventEmitterState._eventInstance.set3DAttributes(FMODUtilities.To3DAttributes(ltw, velocity));
                }
                
                eventEmitterState.PreviousPosition = ltw.Position;
            }
        }

#if UNITY_PHYSICS_PRESENT
        [BurstCompile]
        public partial struct UpdatePhysicsEventEmitters3DAttributesJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                in PhysicsVelocity physicsVelocity,
                ref FMODEventEmitterState eventEmitterState)
            {
                float3 velocity = float3.zero;
                if (eventEmitter.NonRigidbodyVelocity)
                {
                    if (DeltaTime != 0f)
                    {
                        velocity = (ltw.Position - eventEmitterState.PreviousPosition) / DeltaTime;
                        velocity = FMODUtilities.ClampToMaxLength(velocity, 20f);
                    }
                }
                else
                {
                    velocity = physicsVelocity.Linear;
                }
                
                if (!ltw.Position.Equals(eventEmitterState.PreviousPosition))
                {
                    eventEmitterState._eventInstance.set3DAttributes(FMODUtilities.To3DAttributes(ltw, velocity));
                }
                
                eventEmitterState.PreviousPosition = ltw.Position;
            }
        }
#endif
    }
}
