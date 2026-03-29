using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
#if UNITY_PHYSICS_PRESENT
using Unity.Physics;
using Unity.Physics.Systems;
#endif
using UnityEngine;
using FMOD;
using FMODUnity;

namespace Trove.Audio.FMOD
{
    [UpdateInGroup(typeof(FMODUpdateSystemGroup))]
    [UpdateAfter(typeof(FMODEventEmitterSystem))]
    public partial struct FMODListenerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FMODSingleton>();
            state.RequireForUpdate<FMODListener>();

            RuntimeUtils.EnforceLibraryOrder();
        }

        public void OnUpdate(ref SystemState state)
        {
            FMODSingleton singleton = SystemAPI.GetSingleton<FMODSingleton>();
            if(!singleton.StudioSystem.isValid())
                return;
            
            EntityQuery listenersQuery = SystemAPI.QueryBuilder().WithAll<FMODListener>().Build();
            singleton.StudioSystem.setNumListeners(Mathf.Clamp(listenersQuery.CalculateEntityCount(), 0, CONSTANTS.MAX_LISTENERS));

            state.Dependency = new FMODAssignListenerNumberJob
            {
            }.Schedule(state.Dependency);
            
            state.Dependency = new CacheAttenuationEntityPositionsJob
            {
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            }.Schedule(state.Dependency);

            state.Dependency = new FMODListenerJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                FMODSingleton = singleton,
            }.Schedule(state.Dependency);
            
#if UNITY_PHYSICS_PRESENT
            state.Dependency = new FMODListenerPhysicsJob
            {
                DeltaTime =  SystemAPI.Time.DeltaTime,
                FMODSingleton = singleton
            }.Schedule(state.Dependency);
#endif
            
            state.Dependency = new FMODListenerPreviousPositionsJob
            {
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(LocalToWorld))]
        public partial struct FMODAssignListenerNumberJob : IJobEntity
        {
            public void Execute([EntityIndexInQuery] int listenerIndex, ref FMODListener listener, in LocalTransform localTransform)
            {
                listener.ListenerIndex = listenerIndex;
            }
        }

        [BurstCompile]
        public partial struct CacheAttenuationEntityPositionsJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            
            public void Execute(ref FMODListener listener)
            {
                if (listener.AttenuationEntity != Entity.Null &&
                    LocalToWorldLookup.TryGetComponent(listener.AttenuationEntity, out LocalToWorld attenuationLtW))
                {
                    listener.AttenuationPosition = attenuationLtW.Position;
                }
                else
                {
                    listener.AttenuationPosition = default;
                }
            }
        }

        [BurstCompile]
#if UNITY_PHYSICS_PRESENT
        [WithNone(typeof(PhysicsVelocity))]
#endif
        public partial struct FMODListenerJob : IJobEntity
        {
            public float DeltaTime;
            public FMODSingleton FMODSingleton;

            public void Execute(ref FMODListener listener, in LocalToWorld ltw)
            {
                if (listener.ListenerIndex >= 0 &&
                    listener.ListenerIndex < CONSTANTS.MAX_LISTENERS)
                {
                    float3 velocity = float3.zero;
                    if (DeltaTime != 0f)
                    {
                        velocity = (ltw.Position - listener.PreviousPosition) / DeltaTime;
                        velocity = FMODDotsUtility.ClampToMaxLength(velocity, 20f);
                    }
                    
                    FMODSingleton.StudioSystem.setListenerAttributes(
                        listener.ListenerIndex, 
                        FMODDotsUtility.To3DAttributes(ltw, velocity), 
                        FMODDotsUtility.ToFMODVector(listener.AttenuationPosition));
                }
            }
        }

#if UNITY_PHYSICS_PRESENT
        [BurstCompile]
        public partial struct FMODListenerPhysicsJob : IJobEntity
        {
            public float DeltaTime;
            public FMODSingleton FMODSingleton;

            public void Execute(ref FMODListener listener, in LocalToWorld ltw, in PhysicsVelocity physicsVelocity)
            {
                if (listener.ListenerIndex >= 0 &&
                    listener.ListenerIndex < CONSTANTS.MAX_LISTENERS)
                {
                    if (listener.NonRigidbodyVelocity)
                    {
                        float3 velocity = float3.zero;
                        if (DeltaTime != 0f)
                        {
                            velocity = (ltw.Position - listener.PreviousPosition) / DeltaTime;
                            velocity = FMODDotsUtility.ClampToMaxLength(velocity, 20f);
                        }

                        FMODSingleton.StudioSystem.setListenerAttributes(
                            listener.ListenerIndex, 
                            FMODDotsUtility.To3DAttributes(ltw, velocity), 
                            FMODDotsUtility.ToFMODVector(listener.AttenuationPosition));
                    }
                    else
                    {
                        FMODSingleton.StudioSystem.setListenerAttributes(
                            listener.ListenerIndex, 
                            FMODDotsUtility.To3DAttributes(ltw, physicsVelocity.Linear), 
                            FMODDotsUtility.ToFMODVector(listener.AttenuationPosition));
                    }
                }
            }
        }
#endif

        [BurstCompile]
        public partial struct FMODListenerPreviousPositionsJob : IJobEntity
        {
            public void Execute(ref FMODListener listener, in LocalToWorld ltw)
            {
                listener.PreviousPosition = ltw.Position;
            }
        }
    }
}
