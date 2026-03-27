using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Mathematics;
#if UNITY_PHYSICS_PRESENT
using Unity.Physics;
using Unity.Physics.Systems;
#endif
using UnityEngine;
using FMODUnity;

namespace DOTSFMOD
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
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
            singleton.StudioSystem.setNumListeners(Mathf.Clamp(listenersQuery.CalculateEntityCount(), 0, FMOD.CONSTANTS.MAX_LISTENERS));

            state.Dependency = new FMODAssignListenerNumberJob
            {
            }.Schedule(state.Dependency);

            state.Dependency = new FMODListenerJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                FMODSingleton = singleton,
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
                    listener.ListenerIndex < FMOD.CONSTANTS.MAX_LISTENERS)
                {
                    float3 velocity = float3.zero;
                    if (DeltaTime != 0f)
                    {
                        velocity = (ltw.Position - listener.PreviousPosition) / DeltaTime;
                        velocity = Vector3.ClampMagnitude(velocity, 20.0f);
                    }
                    
                    FMODSingleton.StudioSystem.setListenerAttributes(listener.ListenerIndex, FMODDotsUtility.To3DAttributes(ltw, velocity), FMODDotsUtility.ToFMODVector(listener.Attenuation));
                }

                listener.PreviousPosition = ltw.Position;
            }
        }
    }

#if UNITY_PHYSICS_PRESENT
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct FMODListenerPhysicsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FMODSingleton>();
            state.RequireForUpdate<FMODListener>();

            RuntimeUtils.EnforceLibraryOrder();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new FMODListenerPhysicsJob
            {
                FMODSingleton = SystemAPI.GetSingleton<FMODSingleton>()
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct FMODListenerPhysicsJob : IJobEntity
        {
            public FMODSingleton FMODSingleton;

            public void Execute(ref FMODListener listener, in LocalToWorld ltw, in PhysicsVelocity physicsVelocity)
            {
                if (listener.ListenerIndex >= 0 &&
                    listener.ListenerIndex < FMOD.CONSTANTS.MAX_LISTENERS)
                {
                    FMODSingleton.StudioSystem.setListenerAttributes(listener.ListenerIndex, FMODDotsUtility.To3DAttributes(ltw, physicsVelocity.Linear), FMODDotsUtility.ToFMODVector(listener.Attenuation));
                }
            }
        }
    }
#endif
}
