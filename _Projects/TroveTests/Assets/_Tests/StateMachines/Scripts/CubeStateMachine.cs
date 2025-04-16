using System.Runtime.CompilerServices;
using Trove;
using Unity.Burst;
using Unity.Entities;
using Trove.PolymorphicStructs;
using Trove.Statemachines;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// This is the polymorphic state buffer element.
/// </summary>
[InternalBufferCapacity(8)] // TODO: tweak internal capacity
public struct CubeState : IBufferElementData, IPoolElement, IState<CubeGlobalStateUpdateData, CubeEntityStateUpdateData>
{
    // Required for VersionedPool handling. Determines if the state exists in the states pool.
    public int Version { get; set; }
    // This is the generated polymorphic state struct, based on the ITemplateStateMachineState polymorphic interface
    public PolyCubeState State;
    
    public StateMachine SubStateMachine;

    public void OnStateEnter(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData,
        ref CubeEntityStateUpdateData entityData)
    {
        State.OnStateEnter(ref stateMachine, ref globalData, ref entityData);
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData,
        ref CubeEntityStateUpdateData entityData)
    {
        State.OnStateExit(ref stateMachine, ref globalData, ref entityData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData,
        ref CubeEntityStateUpdateData entityData)
    {
        State.Update(ref stateMachine, ref globalData, ref entityData);
        
        // Update the sub-state machine
        StateMachineUtilities.Update(ref SubStateMachine, ref entityData.StatesBuffer, ref globalData,
            ref entityData);
    }
}

[PolymorphicStructInterface]
public interface ICubeState : IState<CubeGlobalStateUpdateData, CubeEntityStateUpdateData>
{ }

public struct StateTransitionTimer
{
    public float Timer;
    public float TransitionTime;

    public StateTransitionTimer(float transitionTime)
    {
        Timer = 0f;
        TransitionTime = transitionTime;
    }
    
    public void Reset()
    {
        Timer = 0f;
    }

    public void Update(float deltaTime, out bool mustTransition)
    {
        Timer += deltaTime;

        if (Timer >= TransitionTime)
        {
            mustTransition = true;
            return;
        }
        
        mustTransition = false;
    }
} 

[PolymorphicStruct] 
public struct CubeStatePosition : ICubeState
{
    public StateTransitionTimer TransitionTimer;
    public StateHandle NextState;
    public float PositionOffset;
    
    public float3 StartPosition;
    public float3 RandomDirection;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
        StartPosition = entityData.LocalTransformRef.ValueRW.Position;
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
        entityData.LocalTransformRef.ValueRW.Position = StartPosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Update(globalData.DeltaTime, out bool mustTransition);

        float normTime = math.saturate(TransitionTimer.Timer / TransitionTimer.TransitionTime);
        entityData.LocalTransformRef.ValueRW.Position = StartPosition + math.lerp(float3.zero, RandomDirection * PositionOffset, math.sin(normTime * math.PI));
        
        if (mustTransition)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, NextState);
        }
    }
}

[PolymorphicStruct]
public struct CubeStateRotation : ICubeState 
{
    public StateTransitionTimer TransitionTimer;
    public StateHandle NextState;
    public float RotationSpeed;
    
    public float3 RandomDirection;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Update(globalData.DeltaTime, out bool mustTransition);

        entityData.LocalTransformRef.ValueRW.Rotation = math.mul(
            quaternion.Euler(RandomDirection * RotationSpeed * globalData.DeltaTime), entityData.LocalTransformRef.ValueRW.Rotation);
            
        if (mustTransition)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, NextState);
        }
    }
}

[PolymorphicStruct]
public struct CubeStateScale : ICubeState 
{
    public StateTransitionTimer TransitionTimer;
    public StateHandle NextState;
    public float Scale;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Update(globalData.DeltaTime, out bool mustTransition);

        entityData.LocalTransformRef.ValueRW.Scale = Scale;
            
        if (mustTransition)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, NextState);
        }
    }
}

public struct CubeGlobalStateUpdateData
{
    public float DeltaTime;

    public CubeGlobalStateUpdateData(float deltaTime)
    {
        DeltaTime = deltaTime;
    }
}

public struct CubeEntityStateUpdateData
{
    public Entity Entity;
    public RefRW<LocalTransform> LocalTransformRef;
    public DynamicBuffer<CubeState> StatesBuffer;
    
    public CubeEntityStateUpdateData(
        Entity entity,
        RefRW<LocalTransform> localTransformRef,
        DynamicBuffer<CubeState> statesBuffer)
    {
        Entity = entity;
        LocalTransformRef = localTransformRef;
        StatesBuffer = statesBuffer;
    }
}

[BurstCompile]
public partial struct ExampleCubeStateMachineSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CubeState>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new CubeStateMachineUpdateJob
        {
            GlobalData = new CubeGlobalStateUpdateData(SystemAPI.Time.DeltaTime), 
        }.ScheduleParallel(state.Dependency); 
    }
    
    [BurstCompile]
    public partial struct CubeStateMachineUpdateJob : IJobEntity
    {
        public CubeGlobalStateUpdateData GlobalData;
        
        public void Execute(
            Entity entity, 
            ref StateMachine stateMachine, 
            RefRW<LocalTransform> localTransformRef,
            ref DynamicBuffer<CubeState> statesBuffer)
        {
            CubeEntityStateUpdateData entityData = new CubeEntityStateUpdateData(
                entity, 
                localTransformRef,
                statesBuffer);
            
            StateMachineUtilities.Update(ref stateMachine, ref statesBuffer, ref GlobalData, ref entityData);
        }
    }
}