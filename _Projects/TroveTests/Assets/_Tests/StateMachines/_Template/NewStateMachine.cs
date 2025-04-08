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
/// a polymorphic interface by making IZoinkState implement the same interface (ex: the IState interface is implemented
/// by both PolyZoinkState and IZoinkState, and this works because the polymorphic structs codegen handles interface
/// inheritance, and the polymorphic states will implement that interface).
/// </summary>
[InternalBufferCapacity(8)] // TODO: tweak internal capacity
public partial struct PolyZoinkState : IState<ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>, IBufferElementData
{ }  

/// <summary>
/// This is the polymorphic interface definition for our states. It inherits the IState interface.
/// </summary>
[PolymorphicStructInterface]
public interface IZoinkState : IState<ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>
{ }

/// <summary>
/// This is an example state
/// </summary>
[PolymorphicStruct] 
public struct ZoinkStateA : IZoinkState
{
    // TODO: add state data
    
    public void OnStateEnter(ref StateMachine stateMachine, ref ZoinkGlobalStateUpdateData globalData, ref ZoinkEntityStateUpdateData entityData)
    {
        // TODO: implement
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref ZoinkGlobalStateUpdateData globalData, ref ZoinkEntityStateUpdateData entityData)
    {
        // TODO: implement
    }

    public void Update(ref StateMachine stateMachine, ref ZoinkGlobalStateUpdateData globalData, ref ZoinkEntityStateUpdateData entityData)
    {
        // TODO: implement
    }
}

/// <summary>
/// This is an example state
/// </summary>
[PolymorphicStruct]
public struct ZoinkStateB : IZoinkState 
{
    // TODO: add state data
    
    public void OnStateEnter(ref StateMachine stateMachine, ref ZoinkGlobalStateUpdateData globalData, ref ZoinkEntityStateUpdateData entityData)
    {
        // TODO: implement
    }

    public void OnStateExit(ref StateMachine stateMachine, ref ZoinkGlobalStateUpdateData globalData, ref ZoinkEntityStateUpdateData entityData)
    {
        // TODO: implement
    }

    public void Update(ref StateMachine stateMachine, ref ZoinkGlobalStateUpdateData globalData, ref ZoinkEntityStateUpdateData entityData)
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
public struct ZoinkGlobalStateUpdateData
{
    public float DeltaTime;
    // TODO: add/change global data

    public ZoinkGlobalStateUpdateData(float deltaTime)
    {
        DeltaTime = deltaTime;
    }
}

/// <summary>
/// This represents the per-entity data that our state updates may need access to.
/// Note that if you need access to a component by reference, you can do so by using RefRW<T>. The LocalTransform
/// field demonstrates this here.
/// </summary>
public struct ZoinkEntityStateUpdateData
{
    public Entity Entity;
    public DynamicBuffer<StateData> StateDatasBuffer;
    public DynamicBuffer<PolyZoinkState> StatesBuffer;
    public RefRW<LocalTransform> LocalTransform;
    // TODO: add/change entity data
    
    public ZoinkEntityStateUpdateData(
        Entity entity,
        DynamicBuffer<StateData> stateDatasBuffer, 
        DynamicBuffer<PolyZoinkState> statesBuffer,
        RefRW<LocalTransform> localTransform)
    {
        Entity = entity;
        StateDatasBuffer = stateDatasBuffer;
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
public partial struct ExampleZoinkStateMachineSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PolyZoinkState>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ZoinkStateMachineUpdateJob
        {
            // Here we build the global data and pass it to the job
            GlobalData = new ZoinkGlobalStateUpdateData(SystemAPI.Time.DeltaTime), 
        }.ScheduleParallel(state.Dependency); 
    }
    
    [BurstCompile]
    public partial struct ZoinkStateMachineUpdateJob : IJobEntity
    {
        public ZoinkGlobalStateUpdateData GlobalData;
        
        public void Execute(
            Entity entity, 
            ref StateMachine stateMachine, 
            RefRW<LocalTransform> localTransform,
            ref DynamicBuffer<StateData> stateVersionsBuffer, 
            ref DynamicBuffer<PolyZoinkState> statesBuffer)
        {
            // Here we build the per-entity data
            ZoinkEntityStateUpdateData entityData = new ZoinkEntityStateUpdateData(
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
/// TODO: move this code out of this file, to a new file named "ZoinkStateMachineAuthoring". Otherwise it won't work
/// </summary>
class ZoinkStateMachineAuthoring : MonoBehaviour
{
    class Baker : Baker<ZoinkStateMachineAuthoring>
    {
        public override void Bake(ZoinkStateMachineAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);

            // Add the state machine components
            StateMachineUtilities
                .BakeStateMachineComponents<PolyZoinkState, ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>(
                    this,
                    entity,
                    out StateMachine stateMachine,
                    out DynamicBuffer<StateData> stateVersionsBuffer,
                    out DynamicBuffer<PolyZoinkState> statesBuffer);

            // Initialize the state machine buffers with an initial capacity
            StateMachineUtilities
                .InitStateMachine<PolyZoinkState, ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>(
                    ref stateMachine,
                    ref stateVersionsBuffer,
                    ref statesBuffer,
                    8);

            // Create a few states and remember their StateHandles.
            // Note: you can create multiple states of the same type.
            StateMachineUtilities
                .CreateState<PolyZoinkState, ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>(
                    ref stateVersionsBuffer,
                    ref statesBuffer,
                    default,
                    out StateHandle state1Handle);
            StateMachineUtilities
                .CreateState<PolyZoinkState, ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>(
                    ref stateVersionsBuffer,
                    ref statesBuffer,
                    default,
                    out StateHandle state2Handle);
            StateMachineUtilities
                .CreateState<PolyZoinkState, ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>(
                    ref stateVersionsBuffer,
                    ref statesBuffer,
                    default,
                    out StateHandle state3Handle);

            // Set state data, now that we have all of our state handles created.
            // Note: it can be useful to set state data after creating all of our state handles, in cases where
            // Our states must store state handles to transition to. If not, we could've also set state data directly
            // in the "CreateState" function.
            StateMachineUtilities.TrySetState<PolyZoinkState, ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>(
                ref stateVersionsBuffer,
                ref statesBuffer,
                state1Handle,
                new ZoinkStateA
                {
                    // TODO: set state data
                });
            StateMachineUtilities.TrySetState<PolyZoinkState, ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>(
                ref stateVersionsBuffer,
                ref statesBuffer,
                state2Handle,
                new ZoinkStateB
                {
                    // TODO: set state data
                });
            StateMachineUtilities.TrySetState<PolyZoinkState, ZoinkGlobalStateUpdateData, ZoinkEntityStateUpdateData>(
                ref stateVersionsBuffer,
                ref statesBuffer,
                state3Handle,
                new ZoinkStateA
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