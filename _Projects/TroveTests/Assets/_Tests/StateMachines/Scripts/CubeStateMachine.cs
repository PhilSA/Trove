using Unity.Burst;
using Unity.Entities;
using Trove.PolymorphicStructs;
using Trove.Statemachines;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct PolyCubeState : IState<CubeGlobalStateUpdateData, CubeEntityStateUpdateData>, IBufferElementData
{ }  

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
public struct CubeStateA : ICubeState
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

    public void Update(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Update(globalData.DeltaTime, out bool mustTransition);

        float normTime = math.saturate(TransitionTimer.Timer / TransitionTimer.TransitionTime);
        entityData.LocalTransformRef.ValueRW.Position = StartPosition + math.lerp(float3.zero, RandomDirection * PositionOffset, math.sin(normTime * math.PI));
        
        if (mustTransition)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StateDatasBuffer,
                ref entityData.StatesBuffer, ref globalData, ref entityData, NextState);
        }
    }
}

[PolymorphicStruct]
public struct CubeStateB : ICubeState 
{
    public StateTransitionTimer TransitionTimer;
    public StateHandle NextState;
    public float RotationSpeed;
    
    public StateMachine StateMachine;
    
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

    public void Update(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Update(globalData.DeltaTime, out bool mustTransition);

        entityData.LocalTransformRef.ValueRW.Rotation = math.mul(
            quaternion.Euler(RandomDirection * RotationSpeed * globalData.DeltaTime), entityData.LocalTransformRef.ValueRW.Rotation);
            
        if (mustTransition)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StateDatasBuffer,
                ref entityData.StatesBuffer, ref globalData, ref entityData, NextState);
        }
        
        // Update the sub-state machine
        StateMachineUtilities.Update(ref StateMachine, ref entityData.StateDatasBuffer, ref entityData.StatesBuffer, ref globalData, ref entityData);
    }
}

[PolymorphicStruct]
public struct CubeStateC : ICubeState 
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

    public void Update(ref StateMachine stateMachine, ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Update(globalData.DeltaTime, out bool mustTransition);

        entityData.LocalTransformRef.ValueRW.Scale = Scale;
            
        if (mustTransition)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StateDatasBuffer,
                ref entityData.StatesBuffer, ref globalData, ref entityData, NextState);
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
    public DynamicBuffer<StateData> StateDatasBuffer;
    public DynamicBuffer<PolyCubeState> StatesBuffer;
    
    public CubeEntityStateUpdateData(
        Entity entity,
        RefRW<LocalTransform> localTransformRef,
        DynamicBuffer<StateData> stateDatasBuffer, 
        DynamicBuffer<PolyCubeState> statesBuffer)
    {
        Entity = entity;
        LocalTransformRef = localTransformRef;
        StateDatasBuffer = stateDatasBuffer;
        StatesBuffer = statesBuffer;
    }
}

[BurstCompile]
public partial struct ExampleCubeStateMachineSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PolyCubeState>();
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
            ref DynamicBuffer<StateData> stateVersionsBuffer, 
            ref DynamicBuffer<PolyCubeState> statesBuffer)
        {
            CubeEntityStateUpdateData entityData = new CubeEntityStateUpdateData(
                entity, 
                localTransformRef,
                stateVersionsBuffer,
                statesBuffer);
            
            StateMachineUtilities.Update(ref stateMachine, ref stateVersionsBuffer, ref statesBuffer, ref GlobalData, ref entityData);
        }
    }
}