
using System.Runtime.CompilerServices;
using Trove.EventSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Trove.PolymorphicStructs;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventTransferPolymorphicStreamToListJob<PStruct_IMyTestGlobalPolyEvent>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get streams to write events in.
/// Event readers access the event manager in this singleton in order to get a list of events to read.
/// </summary>
public struct MyTestGlobalPolyEventsSingleton : IComponentData, IGlobalPolymorphicEventsSingleton
{
    public StreamEventsManager StreamEventsManager { get; set; }
    public NativeList<byte> EventsList { get; set; }
}

/// <summary>
/// Polymorphic interface used for generating our event polymorphic struct. 
///
/// This will generate a new polymorphic struct named PStruct_IMyTestGlobalPolyEvent that can act as any event type implementing
/// this interface and using the [PolymorphicStruct] attribute.
///
/// You can add parameters and return types to the Execute function, or even add new functions. "Execute()" is only
/// a suggestion.
/// </summary>
[PolymorphicStructInterface]
public interface IMyTestGlobalPolyEvent
{
    public void Execute();
}

/// <summary>
/// This is an example polymorphic event type. You can create more of these containing different data.
/// </summary>
[PolymorphicStruct]
public struct MyTestGlobalPolyEventA : IMyTestGlobalPolyEvent
{
    // TODO: Define event data
    public int Val;
    
    public void Execute()
    {
        // TODO: implement event execution
    }
}

/// <summary>
/// This is an example polymorphic event type. You can create more of these containing different data.
/// </summary>
[PolymorphicStruct]
public struct MyTestGlobalPolyEventB : IMyTestGlobalPolyEvent
{
    // TODO: Define event data
    public int Val1;
    public int Val2;
    public int Val3;
    
    public void Execute()
    {
        // TODO: implement event execution
    }
}

// TODO: Define more polymorphic event structs

/// <summary>
/// This is the event system that transfers events from the streams to the global events list.
/// It also clears the events list before adding to it, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// </summary>
partial struct MyTestGlobalPolyEventSystem : ISystem
{
    private GlobalPolymorphicEventSubSystem<MyTestGlobalPolyEventsSingleton, PStruct_IMyTestGlobalPolyEvent> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new GlobalPolymorphicEventSubSystem<MyTestGlobalPolyEventsSingleton, PStruct_IMyTestGlobalPolyEvent>(
                ref state, 32, 1000); // TODO: tweak initial capacities
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _subSystem.OnDestroy(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _subSystem.OnUpdate(ref state);
    }
}