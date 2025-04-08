using Unity.Burst;
using Unity.Entities;
using Trove.PolymorphicStructs;
using Trove.Statemachines;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using StateMachine = Trove.Statemachines.StateMachine;

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
public struct StateA : ICubeState
{
    public StateTransitionTimer TransitionTimer;
    public StateHandle NextState;
    public float PositionOffset;
    
    public float3 StartPosition;
    public float3 RandomDirection;
    
    public void OnStateEnter(ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
        StartPosition = entityData.LocalTransformRef.ValueRW.Position;
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    } 

    public void OnStateExit(ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
        entityData.LocalTransformRef.ValueRW.Position = StartPosition;
    }

    public void Update(ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Update(globalData.DeltaTime, out bool mustTransition);

        float normTime = math.saturate(TransitionTimer.Timer / TransitionTimer.TransitionTime);
        entityData.LocalTransformRef.ValueRW.Position = StartPosition + math.lerp(float3.zero, RandomDirection * PositionOffset, math.sin(normTime * math.PI));
        
        if (mustTransition)
        {
            StateMachineUtilities.TryStateTransition(ref entityData.StateMachineRef.ValueRW, ref entityData.StateVersionsBuffer,
                ref entityData.StatesBuffer, ref globalData, ref entityData, NextState);
        }
    }
}

[PolymorphicStruct]
public struct StateB : ICubeState 
{
    public StateTransitionTimer TransitionTimer;
    public StateHandle NextState;
    public float RotationSpeed;
    
    public float3 RandomDirection;
    
    public void OnStateEnter(ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    }

    public void OnStateExit(ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Reset();
    }

    public void Update(ref CubeGlobalStateUpdateData globalData, ref CubeEntityStateUpdateData entityData)
    {
        TransitionTimer.Update(globalData.DeltaTime, out bool mustTransition);

        entityData.LocalTransformRef.ValueRW.Rotation = math.mul(
            quaternion.Euler(RandomDirection * RotationSpeed * globalData.DeltaTime), entityData.LocalTransformRef.ValueRW.Rotation);
            
        if (mustTransition)
        {
            StateMachineUtilities.TryStateTransition(ref entityData.StateMachineRef.ValueRW, ref entityData.StateVersionsBuffer,
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
    public RefRW<StateMachine> StateMachineRef;
    public RefRW<LocalTransform> LocalTransformRef;
    public DynamicBuffer<StateVersion> StateVersionsBuffer;
    public DynamicBuffer<PolyCubeState> StatesBuffer;
    
    public CubeEntityStateUpdateData(
        Entity entity,
        RefRW<StateMachine> stateMachineRef, 
        RefRW<LocalTransform> localTransformRef,
        DynamicBuffer<StateVersion> stateVersionsBuffer, 
        DynamicBuffer<PolyCubeState> statesBuffer)
    {
        Entity = entity;
        StateMachineRef = stateMachineRef;
        LocalTransformRef = localTransformRef;
        StateVersionsBuffer = stateVersionsBuffer;
        StatesBuffer = statesBuffer;
    }
}

[BurstCompile]
public partial struct ExampleZoinkStateMachineSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PolyCubeState>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ZoinkStateMachineUpdateJob
        {
            GlobalData = new CubeGlobalStateUpdateData(SystemAPI.Time.DeltaTime), 
        }.ScheduleParallel(state.Dependency); 
    }
    
    [BurstCompile]
    public partial struct ZoinkStateMachineUpdateJob : IJobEntity
    {
        public CubeGlobalStateUpdateData GlobalData;
        
        public void Execute(
            Entity entity, 
            RefRW<StateMachine> stateMachineRef, 
            RefRW<LocalTransform> localTransformRef,
            ref DynamicBuffer<StateVersion> stateVersionsBuffer, 
            ref DynamicBuffer<PolyCubeState> statesBuffer)
        {
            CubeEntityStateUpdateData entityData = new CubeEntityStateUpdateData(
                entity, 
                stateMachineRef, 
                localTransformRef,
                stateVersionsBuffer,
                statesBuffer);

            // Transition to initial state
            if (stateMachineRef.ValueRW.CurrentStateHandle == default)
            {
                StateMachineUtilities.TryStateTransition(ref entityData.StateMachineRef.ValueRW, ref entityData.StateVersionsBuffer,
                    ref entityData.StatesBuffer, ref GlobalData, ref entityData, new StateHandle(0, stateVersionsBuffer[0].Version));
            }
            
            StateMachineUtilities.Update(ref stateMachineRef.ValueRW, ref stateVersionsBuffer, ref statesBuffer, ref GlobalData, ref entityData);
        }
    }
}