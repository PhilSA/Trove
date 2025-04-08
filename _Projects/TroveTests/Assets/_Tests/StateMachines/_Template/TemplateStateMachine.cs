using Unity.Burst;
using Unity.Entities;
using Trove.PolymorphicStructs;
using Trove.Statemachines;
using Unity.Transforms;
using UnityEngine;

#region States
/// <summary>
/// This is the generated polymorphic struct representing our state buffer elements.
/// 
/// Note: You can't add any new fields to it, but you can make it implement interfaces. You can also make it implement
/// a polymorphic interface by making ITemplateStateMachineState implement the same interface (ex: the IState interface is implemented
/// by both PolyTemplateStateMachineState and ITemplateStateMachineState, and this works because the polymorphic structs codegen handles interface
/// inheritance, and the polymorphic states will implement that interface).
/// </summary>
[InternalBufferCapacity(8)] // TODO: tweak internal capacity
public partial struct PolyTemplateStateMachineState : IState<TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>, IBufferElementData
{ }  

/// <summary>
/// This is the polymorphic interface definition for our states. It inherits the IState interface.
/// </summary>
[PolymorphicStructInterface]
public interface ITemplateStateMachineState : IState<TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>
{ }

/// <summary>
/// This is an example state
/// </summary>
[PolymorphicStruct] 
public struct TemplateStateMachineStateA : ITemplateStateMachineState
{
    // TODO: add state data
    
    public void OnStateEnter(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData, ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        // TODO: implement
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData, ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        // TODO: implement
    }

    public void Update(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData, ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        // TODO: implement
    }
}

/// <summary>
/// This is an example state
/// </summary>
[PolymorphicStruct]
public struct TemplateStateMachineStateB : ITemplateStateMachineState 
{
    // TODO: add state data
    
    public void OnStateEnter(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData, ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        // TODO: implement
    }

    public void OnStateExit(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData, ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        // TODO: implement
    }

    public void Update(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData, ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        // TODO: implement
    }
}
#endregion

#region State Update Datas
/// <summary>
/// This represents the global data that our state updates may need access to.
/// Here you can store time, singletons, component lookups, native collections, etc....
/// </summary>
public struct TemplateStateMachineGlobalStateUpdateData
{
    public float DeltaTime;
    // TODO: add/change global data

    public TemplateStateMachineGlobalStateUpdateData(float deltaTime)
    {
        DeltaTime = deltaTime;
    }
}

/// <summary>
/// This represents the per-entity data that our state updates may need access to.
/// Note that if you need access to a component by reference, you can do so by using RefRW<T>. The LocalTransform
/// field demonstrates this here.
/// </summary>
public struct TemplateStateMachineEntityStateUpdateData
{
    public Entity Entity;
    public DynamicBuffer<StateVersion> StateVersionsBuffer;
    public DynamicBuffer<PolyTemplateStateMachineState> StatesBuffer;
    public RefRW<LocalTransform> LocalTransform;
    // TODO: add/change entity data
    
    public TemplateStateMachineEntityStateUpdateData(
        Entity entity,
        DynamicBuffer<StateVersion> stateVersionsBuffer, 
        DynamicBuffer<PolyTemplateStateMachineState> statesBuffer,
        RefRW<LocalTransform> localTransform)
    {
        Entity = entity;
        StateVersionsBuffer = stateVersionsBuffer;
        StatesBuffer = statesBuffer;
        LocalTransform = localTransform;
    }
}
#endregion

#region Example System
/// <summary>
/// An example of a system that schedules a state machine update job
/// </summary>
[BurstCompile]
public partial struct ExampleTemplateStateMachineStateMachineSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PolyTemplateStateMachineState>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new TemplateStateMachineStateMachineUpdateJob
        {
            // Here we build the global data and pass it to the job
            GlobalData = new TemplateStateMachineGlobalStateUpdateData(SystemAPI.Time.DeltaTime), 
        }.ScheduleParallel(state.Dependency); 
    }
    
    [BurstCompile]
    public partial struct TemplateStateMachineStateMachineUpdateJob : IJobEntity
    {
        public TemplateStateMachineGlobalStateUpdateData GlobalData;
        
        public void Execute(
            Entity entity, 
            ref StateMachine stateMachine, 
            RefRW<LocalTransform> localTransform,
            ref DynamicBuffer<StateVersion> stateVersionsBuffer, 
            ref DynamicBuffer<PolyTemplateStateMachineState> statesBuffer)
        {
            // Here we build the per-entity data
            TemplateStateMachineEntityStateUpdateData entityData = new TemplateStateMachineEntityStateUpdateData(
                entity, 
                stateVersionsBuffer,
                statesBuffer,
                localTransform);

            // Update the state machine
            StateMachineUtilities.Update(ref stateMachine, ref stateVersionsBuffer, ref statesBuffer, ref GlobalData, ref entityData);
        }
    }
}
#endregion

#region Example Authoring
/// <summary>
/// This an example of an authoring component for this state machine
/// TODO: move this code out of this file, to a new file named "TemplateStateMachineStateMachineAuthoring". Otherwise it won't work
/// </summary>
class TemplateStateMachineStateMachineAuthoring : MonoBehaviour
{
    class Baker : Baker<TemplateStateMachineStateMachineAuthoring>
    {
        public override void Bake(TemplateStateMachineStateMachineAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);

            // Add the state machine components
            StateMachineUtilities
                .BakeStateMachineComponents<PolyTemplateStateMachineState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    this,
                    entity,
                    out StateMachine stateMachine,
                    out DynamicBuffer<StateVersion> stateVersionsBuffer,
                    out DynamicBuffer<PolyTemplateStateMachineState> statesBuffer);

            // Initialize the state machine buffers with an initial capacity
            StateMachineUtilities
                .InitStateMachine<PolyTemplateStateMachineState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    ref stateMachine,
                    ref stateVersionsBuffer,
                    ref statesBuffer,
                    8);

            // Create a few states and remember their StateHandles.
            // Note: you can create multiple states of the same type.
            StateMachineUtilities
                .CreateState<PolyTemplateStateMachineState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    ref stateVersionsBuffer,
                    ref statesBuffer,
                    default,
                    out StateHandle state1Handle);
            StateMachineUtilities
                .CreateState<PolyTemplateStateMachineState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    ref stateVersionsBuffer,
                    ref statesBuffer,
                    default,
                    out StateHandle state2Handle);
            StateMachineUtilities
                .CreateState<PolyTemplateStateMachineState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    ref stateVersionsBuffer,
                    ref statesBuffer,
                    default,
                    out StateHandle state3Handle);

            // Set state data, now that we have all of our state handles created.
            // Note: it can be useful to set state data after creating all of our state handles, in cases where
            // Our states must store state handles to transition to. If not, we could've also set state data directly
            // in the "CreateState" function.
            StateMachineUtilities.TrySetState<PolyTemplateStateMachineState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                ref stateVersionsBuffer,
                ref statesBuffer,
                state1Handle,
                new TemplateStateMachineStateA
                {
                    // TODO: set state data
                });
            StateMachineUtilities.TrySetState<PolyTemplateStateMachineState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                ref stateVersionsBuffer,
                ref statesBuffer,
                state2Handle,
                new TemplateStateMachineStateB
                {
                    // TODO: set state data
                });
            StateMachineUtilities.TrySetState<PolyTemplateStateMachineState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                ref stateVersionsBuffer,
                ref statesBuffer,
                state3Handle,
                new TemplateStateMachineStateA
                {
                    // TODO: set state data
                });

            // Set an initial state for our state machine. This is a state the state machine will automatically 
            // transition to the first time it updates.
            // Note: we don't want to set the "stateMachine.CurrentState" here, because if we do that here in baking,
            // the current state would not get an "OnStateEnter" at runtime.
            stateMachine.InitialState = state1Handle;

            // Write back any changes to the stateMachine component
            SetComponent(entity, stateMachine);
        }
    }
}
#endregion