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
            ComponentLookup<IsActiveEmitter> isActiveEmitterLookup = SystemAPI.GetComponentLookup<IsActiveEmitter>(false);

            state.Dependency = new EventEmittersStartJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
                PlayStateUpdateLookup = playStateUpdateLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new EventEmittersDestroyJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginPresentationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                PlayStateUpdateLookup = playStateUpdateLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new EventEmittersEnableJob
            {
                SingletonEntity = singletonEntity,
                SingletonLookup = singletonLookup,
                IsActiveLookup = isActiveEmitterLookup,
                IsEnabledEmitterLookup = isEnabledEmitterLookup,
            }.Schedule(state.Dependency);

            state.Dependency = new EventEmittersDisableJob
            {
                IsActiveLookup = isActiveEmitterLookup,
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

                if (eventEmitterState.StopOnDestroyed)
                {
                    EnabledRefRW<FMODEmitterPlayStateUpdate> playStateUpdate = PlayStateUpdateLookup.GetEnabledRefRW<FMODEmitterPlayStateUpdate>(entity);
                    FMODDotsUtility.Stop(ref eventEmitterState, playStateUpdate);
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
            public ComponentLookup<IsActiveEmitter> IsActiveLookup;
            public ComponentLookup<IsEnabledEmitter> IsEnabledEmitterLookup;

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

                if (emitter.PlayOnEnabled)
                {
                    FMODDotsUtility.HandlePlay(entity, ref _singleton, in emitter, ref eventEmitterState,
                        ref eventEmitterParameters, in ltw, ref IsActiveLookup);
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
            public ComponentLookup<IsActiveEmitter> IsActiveLookup;
            
            internal void Execute(
                Entity entity,
                in FMODEventEmitter emitter,
                ref FMODEventEmitterState eventEmitterState,
                EnabledRefRW<IsEnabledEmitter> emitterIsEnabled)
            {
                emitterIsEnabled.ValueRW = false;

                if (emitter.StopOnDisabled)
                {
                    FMODDotsUtility.HandleStop(entity, ref eventEmitterState, ref IsActiveLookup);
                }
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
                PlayStateUpdateLookup.SetComponentEnabled(entity, false);
                
                switch (eventEmitterState.PlayStateEventType)
                {
                    case EmitterControlEventType.Play:
                        FMODDotsUtility.HandlePlay(entity, ref _singleton, in emitter, ref eventEmitterState,
                            ref eventEmitterParameters, in ltw, ref IsActiveLookup);
                        break;
                    case EmitterControlEventType.Stop:
                        FMODDotsUtility.HandleStop(entity, ref eventEmitterState, ref IsActiveLookup);
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
                ref FMODEventEmitter eventEmitter,
                ref DynamicBuffer<FMODEventParameter> eventEmitterParameters,
                in LocalToWorld ltw,
                ref FMODEventEmitterState eventEmitterState)
            {
                if (!ltw.Position.Equals(eventEmitterState.PreviousPosition))
                {
                    eventEmitterState._eventInstance.set3DAttributes(
                        FMODDotsUtility.To3DAttributes(ltw, eventEmitterState.Velocity));
                }

                // FMODDotsUtility.UpdatePlayingStatus(
                //     ref _singleton,
                //     in eventEmitter.EventGUID,
                //     ref eventEmitterState, 
                //     ref eventEmitterParameters,
                //     in ltw,
                //     eventEmitterState.Velocity,
                //     false);
                
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
