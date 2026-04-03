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
            ComponentLookup<IsActiveEmitter> isActiveEmitterLookup = SystemAPI.GetComponentLookup<IsActiveEmitter>(false);

            state.Dependency = new EventEmittersStartJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
                PlayStateUpdateLookup = SystemAPI.GetComponentLookup<FMODEmitterPlayStateUpdate>(false),
            }.Schedule(state.Dependency);

            state.Dependency = new EventEmittersDestroyJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginPresentationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                PlayStateUpdateLookup = playStateUpdateLookup,
            }.Schedule(state.Dependency);
            
            state.Dependency = new CalculateEventEmittersVelocityJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency); 

#if UNITY_PHYSICS_PRESENT
            state.Dependency = new CalculateEventEmittersVelocityPhysicsJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
#endif

            state.Dependency = new PlayEventEmittersJob
            {
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
                PlayStateUpdateLookup = playStateUpdateLookup,
                IsActiveLookup = isActiveEmitterLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new UpdateEventEmittersJob
            {
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
            }.ScheduleParallel(state.Dependency); 
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

            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;

            public void Execute(
                Entity entity,
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw)
            {
                FMODEventEmitterState state = new FMODEventEmitterState();
                state.UpdateFrom(eventEmitter, in ltw);

                if (eventEmitter.Preload)
                {
                    state._eventDescription =
                        FMODDotsUtility.LoadEventFromGUID(ref _singleton, in eventEmitter.EventGUID, ref eventEmitterParameters);
                    state.EventDescription.loadSampleData();
                }

                if (eventEmitter.PlayOnCreated)
                {
                    state.PlayStateEventType = EmitterControlEventType.Play;
                    PlayStateUpdateLookup.SetComponentEnabled(entity, true);
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

        [BurstCompile]
        [WithNone(typeof(FMODEventEmitter))]
        [WithPresent(typeof(FMODEmitterPlayStateUpdate))]
        public partial struct EventEmittersDestroyJob : IJobEntity
        {
            public EntityCommandBuffer ECB;
            public ComponentLookup<FMODEmitterPlayStateUpdate> PlayStateUpdateLookup;

            public void Execute(Entity entity,
                ref FMODEventEmitterState eventEmitterState)
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
                    EnabledRefRW<FMODEmitterPlayStateUpdate> playStateUpdate = PlayStateUpdateLookup.GetEnabledRefRW<FMODEmitterPlayStateUpdate>(entity);
                    FMODDotsUtility.Stop(ref eventEmitterState, playStateUpdate);
                }

                ECB.RemoveComponent<FMODEventEmitterState>(entity);
            }
        }

        [BurstCompile]
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
        [WithAll(typeof(IsActiveEmitter))]
        public partial struct CalculateEventEmittersVelocityPhysicsJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                in PhysicsVelocity physicsVelocity,
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
        [WithAll(typeof(FMODEmitterPlayStateUpdate))]
        [WithPresent(typeof(IsActiveEmitter))]
        public partial struct PlayEventEmittersJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public Entity SingletonEntity;
            public ComponentLookup<FMODSingleton> SingletonLookup;
            public ComponentLookup<FMODEmitterPlayStateUpdate> PlayStateUpdateLookup;
            public ComponentLookup<IsActiveEmitter> IsActiveLookup;

            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;

            public void Execute(
                Entity entity,
                in FMODEventEmitter emitter,
                ref FMODEventEmitterState eventEmitterState,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw)
            {
                switch (eventEmitterState.PlayStateEventType)
                {
                    case EmitterControlEventType.Play:
                        HandlePlay(entity, in emitter, ref eventEmitterState, ref eventEmitterParameters, in ltw);
                        break;
                    case EmitterControlEventType.Stop:
                        HandleStop(entity, ref eventEmitterState);
                        break;
                    case EmitterControlEventType.Pause:
                        eventEmitterState.EventInstance.setPaused(true);
                        PlayStateUpdateLookup.SetComponentEnabled(entity, false);
                        break;
                    case EmitterControlEventType.Resume:
                        eventEmitterState.EventInstance.setPaused(false);
                        PlayStateUpdateLookup.SetComponentEnabled(entity, false);
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
                Entity entity, 
                in FMODEventEmitter emitter,
                ref FMODEventEmitterState eventEmitterState,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw)
            {
                if (eventEmitterState.TriggerOnce && eventEmitterState.HasTriggered)
                    return;

                if (!eventEmitterState.EventDescription.isValid())
                {
                    eventEmitterState._eventDescription =
                        FMODDotsUtility.LoadEventFromGUID(ref _singleton, in emitter.EventGUID, ref eventEmitterParameters);
                    eventEmitterState.EventDescription.loadSampleData();
                    return;
                }

                PlayStateUpdateLookup.SetComponentEnabled(entity, false);

                eventEmitterState.EventDescription.isSnapshot(out bool isSnapshot);

                if (!isSnapshot)
                {
                    eventEmitterState.EventDescription.isOneshot(out eventEmitterState.IsOneShot);
                }

                eventEmitterState.EventDescription.is3D(out bool is3D);

                IsActiveLookup.SetComponentEnabled(entity, true);

                if (is3D && _singleton.StopEventsOutsideMaxDistance)
                {
                    FMODDotsUtility.UpdatePlayingStatus(
                        ref _singleton,
                        in emitter.EventGUID,
                        ref eventEmitterState, 
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

            private void HandleStop(Entity entity, ref FMODEventEmitterState eventEmitterState)
            {
                IsActiveLookup.SetComponentEnabled(entity, false);
                FMODDotsUtility.StopInstance(in eventEmitterState);
                PlayStateUpdateLookup.SetComponentEnabled(entity, false);
            }
        }

        [BurstCompile]
        [WithAll(typeof(IsActiveEmitter))]
        public partial struct UpdateEventEmittersJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public Entity SingletonEntity;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<FMODSingleton> SingletonLookup;

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
                eventEmitterState.EventInstance.set3DAttributes(FMODDotsUtility.To3DAttributes(ltw, eventEmitterState.Velocity));
                
                FMODDotsUtility.UpdatePlayingStatus(
                    ref _singleton,
                    in eventEmitter.EventGUID,
                    ref eventEmitterState, 
                    ref eventEmitterParameters,
                    in ltw,
                    eventEmitterState.Velocity,
                    false);
                
                eventEmitterState.PreviousPosition = ltw.Position;
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
}
