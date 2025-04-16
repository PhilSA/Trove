using System.Runtime.CompilerServices;
using Trove;
using Unity.Burst;
using Unity.Entities;
using Trove.PolymorphicStructs;
using Trove.Statemachines;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#region States






/// <summary>
/// This is the polymorphic state buffer element.
/// </summary>
[InternalBufferCapacity(8)] // TODO: tweak internal capacity
public struct CubeSMState : IBufferElementData, IPoolElement, IState<CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>
{
    // Required for VersionedPool handling. Determines if the state exists in the states pool.
    public int Version { get; set; }
    // This is the generated polymorphic state struct, based on the ICubeSMStateMachineState polymorphic interface
    public PolyCubeSMState State;

    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData,
        ref CubeSMEntityStateUpdateData entityData)
    {
        State.OnStateEnter(ref stateMachine, ref globalData, ref entityData);
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData,
        ref CubeSMEntityStateUpdateData entityData)
    {
        State.OnStateExit(ref stateMachine, ref globalData, ref entityData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData,
        ref CubeSMEntityStateUpdateData entityData)
    {
        State.Update(ref stateMachine, ref globalData, ref entityData);
    }
}

/// <summary>
/// This is the polymorphic interface definition for our states. It inherits the IState interface.
/// </summary>
[PolymorphicStructInterface]
public interface ICubeSMState : IState<CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>
{
}





/// <summary>
/// This is an example state
/// </summary>
[PolymorphicStruct] 
public struct CubeSMPositionState : ICubeSMState
{
    public StateHandle TargetState;
    public float Duration;
    public float PositionOffset;
    
    public float Timer;
    public float3 StartPosition;
    public float3 RandomDirection;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer = 0f;
        StartPosition = entityData.LocalTransformRef.ValueRW.Position;
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        entityData.LocalTransformRef.ValueRW.Position = StartPosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;

        float normTime = math.saturate(Timer / Duration);
        entityData.LocalTransformRef.ValueRW.Position = StartPosition + math.lerp(float3.zero, RandomDirection * PositionOffset, math.sin(normTime * math.PI));
        
        if (Timer >= Duration)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, TargetState);
        }
    }
}

/// <summary>
/// This is an example state
/// </summary>
[PolymorphicStruct]
public struct CubeSMRotationState : ICubeSMState 
{
    public StateHandle TargetState;
    public float Duration;
    public float RotationSpeed;
    
    public StateMachine StateMachine;
    
    public float Timer;
    public float3 RandomDirection;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer = 0f;
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;

        entityData.LocalTransformRef.ValueRW.Rotation = math.mul(
            quaternion.Euler(RandomDirection * RotationSpeed * globalData.DeltaTime), entityData.LocalTransformRef.ValueRW.Rotation);
            
        if (Timer >= Duration)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, TargetState);
        }
        
        StateMachineUtilities.Update(ref StateMachine, ref entityData.StatesBuffer, ref globalData, ref entityData);
    }
}

[PolymorphicStruct]
public struct CubeSMScaleState : ICubeSMState 
{
    public StateHandle TargetState;
    public float Duration;
    public float Scale;
    
    public float Timer;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer = 0f;
        entityData.LocalTransformRef.ValueRW.Scale = Scale;
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;
            
        if (Timer >= Duration)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, TargetState);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FixedUpdate(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData,
        ref CubeSMEntityStateUpdateData entityData)
    {
    }
}
#endregion

#region State Update Datas
/// <summary>
/// This represents the global data that our state updates may need access to.
/// Here you can store time, singletons, component lookups, native collections, etc....
/// </summary>
public struct CubeSMGlobalStateUpdateData
{
    public float DeltaTime;
    // TODO: add/change global data

    public CubeSMGlobalStateUpdateData(float deltaTime)
    {
        DeltaTime = deltaTime;
    }
}

/// <summary>
/// This represents the per-entity data that our state updates may need access to.
/// Note that if you need access to a component by reference, you can do so by using RefRW<T>. The LocalTransform
/// field demonstrates this here.
/// </summary>
public struct CubeSMEntityStateUpdateData
{
    public Entity Entity;
    public DynamicBuffer<CubeSMState> StatesBuffer;
    public RefRW<LocalTransform> LocalTransformRef;
    
    public CubeSMEntityStateUpdateData(
        Entity entity,
        DynamicBuffer<CubeSMState> statesBuffer,
        RefRW<LocalTransform> localTransformRef)
    {
        Entity = entity;
        StatesBuffer = statesBuffer;
        LocalTransformRef = localTransformRef;
    }
}
#endregion

#region Example System
/// <summary>
/// An example of a system that schedules a state machine update job
/// </summary>
[BurstCompile]
public partial struct ExampleCubeSMSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CubeSMState>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new CubeSMUpdateJob
        {
            // Here we build the global data and pass it to the job
            GlobalData = new CubeSMGlobalStateUpdateData(SystemAPI.Time.DeltaTime), 
        }.ScheduleParallel(state.Dependency); 
    }
    
    [BurstCompile]
    public partial struct CubeSMUpdateJob : IJobEntity
    {
        public CubeSMGlobalStateUpdateData GlobalData;
        
        public void Execute(
            Entity entity, 
            ref StateMachine stateMachine, 
            ref DynamicBuffer<CubeSMState> statesBuffer,
            RefRW<LocalTransform> localTransformRef)
        {
            // Here we build the per-entity data
            CubeSMEntityStateUpdateData entityData = new CubeSMEntityStateUpdateData(
                entity, 
                statesBuffer,
                localTransformRef);

            // Update the state machine
            StateMachineUtilities.Update(ref stateMachine, ref statesBuffer, ref GlobalData, ref entityData);
        }
    }
}
#endregion
