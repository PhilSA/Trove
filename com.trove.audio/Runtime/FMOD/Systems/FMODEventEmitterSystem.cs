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
            if(!singleton.StudioSystem.isValid())
                return;
            
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            state.Dependency = new EventEmittersStartJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
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
                         .Query<RefRW<FMODEventEmitter>, RefRW<FMODEventEmitterCleanup>,
                             DynamicBuffer<FMODEmitterParameterElement>>()
                         .WithAll<LoadEventDescriptionRequest>()
                         .WithEntityAccess())
            {
                DynamicBuffer<FMODEmitterParameterElement> parametersBuffer = parameters;
                emitterCleanup.ValueRW.EventDescription =
                    FMODDotsUtility.LoadEventFromGUID(ref singleton, in emitter.ValueRO.EventGUID, ref parametersBuffer);
                emitterCleanup.ValueRW.EventDescription.loadSampleData();
            }
        }

        [BurstCompile]
        [WithNone(typeof(FMODEventEmitterCleanup))]
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
                ref DynamicBuffer<FMODEmitterParameterElement> eventEmitterParameters,
                EnabledRefRW<LoadEventDescriptionRequest> loadEventDescriptionRequest,
                EnabledRefRW<FMODPlayRequest> playRequest)
            {
                FMODEventEmitterCleanup cleanup = new FMODEventEmitterCleanup();
                
                if (eventEmitter.Preload)
                {
                    loadEventDescriptionRequest.ValueRW = true;
                }

                if (eventEmitter.PlayOnCreated)
                {
                    playRequest.ValueRW = true;
                }
                
                ECB.AddComponent(entity, cleanup);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!_singleton.StudioSystem.isValid())
                {
                    SingletonLookup.TryGetComponent(SingletonEntity, out _singleton);
                }
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            { }
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
            if(!singleton.StudioSystem.isValid())
                return;

            state.Dependency = new EventEmittersDestroyJob
            {

            }.Schedule(state.Dependency);

            state.Dependency = new PlayEventEmittersJob
            {

            }.Schedule(state.Dependency);

            state.Dependency = new StopEventEmittersJob
            {

            }.Schedule(state.Dependency);

            state.Dependency = new UpdateEventEmittersJob
            {

            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithNone(typeof(FMODEventEmitter))]
        public partial struct EventEmittersDestroyJob : IJobEntity
        {
            public EntityCommandBuffer ECB;
            
            public void Execute(Entity entity, ref FMODEventEmitterCleanup eventEmitter, EnabledRefRW<FMODStopRequest> stopRequest)
            {
                if (eventEmitter.StopOnDestroyed)
                {
                    stopRequest.ValueRW = true;
                }
                
                ECB.RemoveComponent<FMODEventEmitterCleanup>(entity);
            }
        }

        [BurstCompile]
        public partial struct PlayEventEmittersJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public Entity SingletonEntity;
            public ComponentLookup<FMODSingleton> SingletonLookup;

            [NativeDisableContainerSafetyRestriction]
            private FMODSingleton _singleton;
            
            public void Execute(ref FMODEventEmitterCleanup eventEmitter, EnabledRefRW<FMODPlayRequest> playRequest)
            {
                playRequest.ValueRW = false;
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!_singleton.StudioSystem.isValid())
                {
                    SingletonLookup.TryGetComponent(SingletonEntity, out _singleton);
                }
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            { }
        }

        [BurstCompile]
        public partial struct StopEventEmittersJob : IJobEntity
        {
            public void Execute(ref FMODEventEmitterCleanup eventEmitter, EnabledRefRW<FMODStopRequest> stopRequest)
            {
                stopRequest.ValueRW = false;
            }
        }

        [BurstCompile]
        [WithAll(typeof(FMODEventEmitterCleanup))]
        public partial struct UpdateEventEmittersJob : IJobEntity
        {
            public void Execute(ref FMODEventEmitter eventEmitter)
            {
                // If at least one listener is within the max distance, ensure an event instance is playing
                // bool playInstance = StudioListener.DistanceSquaredToNearestListener(transform.position) <= (MaxDistance * MaxDistance);

                // if (force || playInstance != IsPlaying())
                // {
                //     if (playInstance)
                //     {
                //         PlayInstance();
                //     }
                //     else
                //     {
                //         StopInstance();
                //     }
                // }
            }
        }
    }
}
