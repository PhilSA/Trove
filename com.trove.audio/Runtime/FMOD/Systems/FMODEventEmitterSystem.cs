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
using Unity.Mathematics;
using Unity.Physics;

namespace Trove.Audio.FMOD
{
    /*
     * TODO:
     * - Handle entity enable/disable
     */

    [UpdateInGroup(typeof(FMODBeginSystemGroup))]
    public partial struct FMODEventEmitterInitializationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FMODSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity singletonEntity = SystemAPI.GetSingletonEntity<FMODSingleton>();
            FMODSingleton singleton = SystemAPI.GetSingleton<FMODSingleton>();
            if (!singleton.StudioSystem.isValid())
                return;

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            state.Dependency = new EventEmittersStartJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                SingletonEntity = singletonEntity,
                SingletonLookup = SystemAPI.GetComponentLookup<FMODSingleton>(false),
            }.Schedule(state.Dependency);

            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Can't be bursted because of FMOD APIs   
            // Load event and parameter descriptions.
            foreach (var (emitter, emitterCleanup, parameters, entity) in
                     SystemAPI
                         .Query<RefRW<FMODEventEmitter>, RefRW<FMODEventEmitterState>,
                             DynamicBuffer<FMODEventParameter>>()
                         .WithAll<LoadEventDescriptionRequest>()
                         .WithEntityAccess())
            {
                DynamicBuffer<FMODEventParameter> parametersBuffer = parameters;
                emitterCleanup.ValueRW._eventDescription =
                    FMODDotsUtility.LoadEventFromGUID(ref singleton, in emitter.ValueRO.EventGUID,
                        ref parametersBuffer);
                emitterCleanup.ValueRW.EventDescription.loadSampleData();
            }
        }

        [BurstCompile]
        [WithNone(typeof(FMODEventEmitterState))]
        public partial struct EventEmittersStartJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public EntityCommandBuffer ECB;
            public Entity SingletonEntity;
            public ComponentLookup<FMODSingleton> SingletonLookup;

            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;

            public void Execute(
                Entity entity,
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest,
                EnabledRefRW<FMODEmitterPlayStateControl> playRequest)
            {
                FMODEventEmitterState state = new FMODEventEmitterState();
                state.UpdateFrom(eventEmitter, in ltw);

                if (eventEmitter.Preload)
                {
                    loadEventDescriptionRequest.ValueRW = true;
                }

                if (eventEmitter.PlayOnCreated)
                {
                    playRequest.ValueRW = true;
                }

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
    }

    [UpdateInGroup(typeof(FMODUpdateSystemGroup))]
    public partial struct FMODEventEmitterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FMODSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity singletonEntity = SystemAPI.GetSingletonEntity<FMODSingleton>();
            FMODSingleton singleton = SystemAPI.GetSingleton<FMODSingleton>();
            if (!singleton.StudioSystem.isValid())
                return;

            state.Dependency = new EventEmittersDestroyJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginPresentationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(state.Dependency);
            
            state.Dependency = new CalculateEventEmittersVelocityJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency); 

            state.Dependency = new CalculateEventEmittersVelocityPhysicsJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new PlayEventEmittersJob
            {
                SingletonEntity = singletonEntity,
                SingletonLookup = SystemAPI.GetComponentLookup<FMODSingleton>(false),
            }.Schedule(state.Dependency);

            state.Dependency = new UpdateEventEmittersJob
            {
            }.ScheduleParallel(state.Dependency); 
        }

        [BurstCompile]
        [WithNone(typeof(FMODEventEmitter))]
        public partial struct EventEmittersDestroyJob : IJobEntity
        {
            public EntityCommandBuffer ECB;

            public void Execute(Entity entity,
                ref FMODEventEmitterState eventEmitterState,
                ref FMODEmitterPlayStateControl emitterPlayStateControl,
                EnabledRefRW<FMODEmitterPlayStateControl> emitterControlEnabled)
            {
                if (eventEmitterState.EventInstance.isValid())
                {
                    eventEmitterState.EventDescription.isOneshot(out eventEmitterState.IsOneShot);
                    if (eventEmitterState.EventDescription.isValid() && eventEmitterState.IsOneShot)
                    {
                        eventEmitterState.EventInstance.release();
                        eventEmitterState.EventInstance.clearHandle();
                    }
                }

                if (eventEmitterState.Preload)
                {
                    eventEmitterState.EventDescription.unloadSampleData();
                }

                if (eventEmitterState.StopOnDestroyed)
                {
                    FMODDotsUtility.Stop(ref emitterPlayStateControl, emitterControlEnabled);
                }

                ECB.RemoveComponent<FMODEventEmitterState>(entity);
            }
        }

        [BurstCompile]
        [WithAll(typeof(FMODEventEmitterState))]
        [WithAll(typeof(IsActiveEmitter))]
#if UNITY_PHYSICS_PRESENT
        [WithNone(typeof(PhysicsVelocity))]
#endif
        public partial struct CalculateEventEmittersVelocityJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest,
                ref FMODEventEmitterState eventEmitterState)
            {
                eventEmitterState.Velocity = float3.zero;
                if (DeltaTime != 0f)
                {
                    eventEmitterState.Velocity = (ltw.Position - eventEmitterState.PreviousPosition) / DeltaTime;
                    eventEmitterState.Velocity = FMODDotsUtility.ClampToMaxLength(eventEmitterState.Velocity, 20f);
                }
            }
        }

#if UNITY_PHYSICS_PRESENT
        [BurstCompile]
        [WithAll(typeof(FMODEventEmitterState))]
        [WithAll(typeof(IsActiveEmitter))]
        public partial struct CalculateEventEmittersVelocityPhysicsJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                in PhysicsVelocity physicsVelocity,
                EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest,
                ref FMODEventEmitterState eventEmitterState)
            {
                eventEmitterState.Velocity = float3.zero;
                if (eventEmitter.NonRigidbodyVelocity)
                {
                    if (DeltaTime != 0f)
                    {
                        eventEmitterState.Velocity = (ltw.Position - eventEmitterState.PreviousPosition) / DeltaTime;
                        eventEmitterState.Velocity = FMODDotsUtility.ClampToMaxLength(eventEmitterState.Velocity, 20f);
                    }
                }
                else
                {
                    eventEmitterState.Velocity = physicsVelocity.Linear;
                }
            }
        }
#endif

        [BurstCompile]
        [WithAll(typeof(FMODEmitterPlayStateControl))]
        public partial struct PlayEventEmittersJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public Entity SingletonEntity;
            public ComponentLookup<FMODSingleton> SingletonLookup;

            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;

            public void Execute(
                in FMODEventEmitter emitter,
                ref FMODEventEmitterState eventEmitterState,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                in FMODEmitterPlayStateControl playStateControl,
                EnabledRefRW<IsActiveEmitter> isActiveEmitter,
                EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest,
                EnabledRefRW<FMODEmitterPlayStateControl> playStateControlEnabled)
            {
                playStateControlEnabled.ValueRW = false;

                switch (playStateControl.EventType)
                {
                    case EmitterControlEventType.Play:
                        HandlePlay(ref eventEmitterState, ref eventEmitterParameters, in ltw, isActiveEmitter, loadEventDescriptionRequest);
                        break;
                    case EmitterControlEventType.Stop:
                        HandleStop(ref eventEmitterState, isActiveEmitter);
                        break;
                    case EmitterControlEventType.Pause:
                        eventEmitterState.EventInstance.setPaused(true);
                        break;
                    case EmitterControlEventType.Resume:
                        eventEmitterState.EventInstance.setPaused(false);
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

            private void HandlePlay(
                ref FMODEventEmitterState eventEmitterState,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                EnabledRefRW<IsActiveEmitter> isActiveEmitter,
                EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest)
            {
                if (eventEmitterState.TriggerOnce && eventEmitterState.HasTriggered)
                    return;

                // TODO: cachedParams.Clear(); ??

                if (!eventEmitterState.EventDescription.isValid())
                {
                    loadEventDescriptionRequest.ValueRW = true;
                    return;
                }

                eventEmitterState.EventDescription.isSnapshot(out bool isSnapshot);

                if (!isSnapshot)
                {
                    eventEmitterState.EventDescription.isOneshot(out eventEmitterState.IsOneShot);
                }

                eventEmitterState.EventDescription.is3D(out bool is3D);

                isActiveEmitter.ValueRW = true;

                if (is3D && _singleton.StopEventsOutsideMaxDistance)
                {
                    FMODDotsUtility.UpdatePlayingStatus(
                        ref eventEmitterState, 
                        loadEventDescriptionRequest, 
                        ref eventEmitterParameters,
                        in ltw,
                        eventEmitterState.Velocity,
                        true);
                }
                else
                {
                    FMODDotsUtility.PlayInstance(
                        ref eventEmitterState,
                        ref eventEmitterParameters,
                        in ltw,
                        eventEmitterState.Velocity);
                }
            }

            private void HandleStop(ref FMODEventEmitterState eventEmitterState,
                EnabledRefRW<IsActiveEmitter> isActiveEmitter)
            {
                isActiveEmitter.ValueRW = false;

                if (eventEmitterState.TriggerOnce && eventEmitterState.HasTriggered)
                {
                    isActiveEmitter.ValueRW = false;
                }

                FMODDotsUtility.StopInstance(in eventEmitterState);
            }
        }

        [BurstCompile]
        [WithAll(typeof(FMODEventEmitterState))]
        [WithAll(typeof(IsActiveEmitter))]
        public partial struct UpdateEventEmittersJob : IJobEntity
        {
            public void Execute(
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest,
                ref FMODEventEmitterState eventEmitterState)
            {
                eventEmitterState.EventInstance.set3DAttributes(FMODDotsUtility.To3DAttributes(ltw, eventEmitterState.Velocity));
                
                FMODDotsUtility.UpdatePlayingStatus(
                    ref eventEmitterState, 
                    loadEventDescriptionRequest, 
                    ref eventEmitterParameters,
                    in ltw,
                    eventEmitterState.Velocity,
                    false);
                
                eventEmitterState.PreviousPosition = ltw.Position;
            }
        }
    }
}
