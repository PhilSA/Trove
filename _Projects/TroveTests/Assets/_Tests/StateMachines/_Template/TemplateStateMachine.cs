using System.Runtime.CompilerServices;
using Trove;
using Unity.Burst;
using Unity.Entities;
using Trove.PolymorphicStructs;
using Trove.Statemachines;
using Unity.Transforms;
using UnityEngine;

#region States

/// <summary>
/// This is the polymorphic state buffer element.
/// </summary>
[InternalBufferCapacity(8)] // TODO: tweak internal capacity
public struct TemplateState : IBufferElementData, IPoolObject, IState<TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>
{
    // Required for VersionedPool handling. Determines if the state exists in the states pool.
    public int Version { get; set; }
    // This is the generated polymorphic state struct, based on the ITemplateStateMachineState polymorphic interface
    public PolyTemplateState State;

    public void OnStateEnter(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData,
        ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        State.OnStateEnter(ref stateMachine, ref globalData, ref entityData);
    }

    public void OnStateExit(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData,
        ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        State.OnStateExit(ref stateMachine, ref globalData, ref entityData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData,
        ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        State.Update(ref stateMachine, ref globalData, ref entityData);
    }
}

/// <summary>
/// This is the polymorphic interface definition for our states. It inherits the IState interface.
/// </summary>
[PolymorphicStructInterface]
public interface ITemplateState : IState<TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>
{ }

/// <summary>
/// This is an example state
/// </summary>
[PolymorphicStruct] 
public struct ITemplateStateA : ITemplateState
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref TemplateStateMachineGlobalStateUpdateData globalData, ref TemplateStateMachineEntityStateUpdateData entityData)
    {
        // TODO: implement
    }
}

/// <summary>
/// This is an example state
/// </summary>
[PolymorphicStruct]
public struct ITemplateStateB : ITemplateState 
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    public DynamicBuffer<TemplateState> StatesBuffer;
    public RefRW<LocalTransform> LocalTransform;
    // TODO: add/change entity data
    
    public TemplateStateMachineEntityStateUpdateData(
        Entity entity,
        DynamicBuffer<TemplateState> statesBuffer,
        RefRW<LocalTransform> localTransform)
    {
        Entity = entity;
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
        state.RequireForUpdate<TemplateState>();
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
            ref DynamicBuffer<TemplateState> statesBuffer)
        {
            // Here we build the per-entity data
            TemplateStateMachineEntityStateUpdateData entityData = new TemplateStateMachineEntityStateUpdateData(
                entity, 
                statesBuffer,
                localTransform);

            // Update the state machine
            StateMachineUtilities.Update(ref stateMachine, ref statesBuffer, ref GlobalData, ref entityData);
        }
    }
}
#endregion

#region Example Authoring
/// <summary>
/// This an example of an authoring component for this state machine
/// TODO: move this code out of this file, to a new file named "TemplateStateMachineStateMachineAuthoring". MonoBehaviours need their file name to match.
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
                .BakeStateMachineComponents<TemplateState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    this,
                    entity,
                    out StateMachine stateMachine,
                    out DynamicBuffer<TemplateState> statesBuffer);

            // Initialize the state machine buffers with an initial capacity
            StateMachineUtilities
                .InitStateMachine<TemplateState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    ref stateMachine,
                    ref statesBuffer,
                    8);

            // Create a few states and remember their StateHandles.
            StateMachineUtilities
                .CreateState<TemplateState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    ref statesBuffer,
                    default,
                    out StateHandle state1Handle);
            StateMachineUtilities
                .CreateState<TemplateState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                    ref statesBuffer,
                    default,
                    out StateHandle state2Handle);

            // Set state data, now that we have all of our state handles created.
            // Note: it can be useful to set state data after creating all of our state handles, in cases where
            // Our states must store state handles to transition to. If not, we could've also set state data directly
            // in the "CreateState" function.
            StateMachineUtilities.TrySetState<TemplateState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                ref statesBuffer,
                state1Handle,
                new TemplateState
                {
                    // TODO: set state data
                    State = new ITemplateStateA
                    {
                        
                    },
                });
            StateMachineUtilities.TrySetState<TemplateState, TemplateStateMachineGlobalStateUpdateData, TemplateStateMachineEntityStateUpdateData>(
                ref statesBuffer,
                state2Handle,
                new TemplateState
                {
                    // TODO: set state data
                    State = new ITemplateStateB
                    {
                        
                    },
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