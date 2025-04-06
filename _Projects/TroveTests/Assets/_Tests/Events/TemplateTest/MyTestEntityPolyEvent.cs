
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
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<MyTestEntityPolyEventBufferElement, HasMyTestEntityPolyEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferPolymorphicStreamToBufferJob<MyTestEntityPolyEventBufferElement, HasMyTestEntityPolyEvents, MyTestEntityPolyEventForEntity, PStruct_IMyTestEntityPolyEvent>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get streams to write events in.
/// </summary>
public struct MyTestEntityPolyEventsSingleton : IComponentData, IEntityPolymorphicEventsSingleton
{
    public StreamEventsManager StreamEventsManager { get; set; }
}

/// <summary>
/// This is the event struct that is written by event writers.
/// It contains an "AffectedEntity" field to determine on which Entity the event will be transfered.
/// "Event" represents what actually gets serialized to the entity's dynamic buffer.
/// </summary>
public struct MyTestEntityPolyEventForEntity : IPolymorphicEventForEntity<PStruct_IMyTestEntityPolyEvent>
{
    public Entity AffectedEntity { get; set; }
    public PStruct_IMyTestEntityPolyEvent Event { get; set; }
}

/// <summary>
/// Polymorphic interface used for generating our event polymorphic struct. 
///
/// This will generate a new polymorphic struct named PStruct_IMyTestEntityPolyEvent that can act as any event type implementing
/// this interface and using the [PolymorphicStruct] attribute.
///
/// You can add parameters and return types to the Execute function, or even add new functions. "Execute()" is only
/// a suggestion.
/// </summary>
[PolymorphicStructInterface]
public interface IMyTestEntityPolyEvent
{
    public void Execute();
}

/// <summary>
/// This is an example polymorphic event type. You can create more of these containing different data.
/// </summary>
[PolymorphicStruct]
public struct MyTestEntityPolyEventA : IMyTestEntityPolyEvent
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
public struct MyTestEntityPolyEventB : IMyTestEntityPolyEvent
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
/// This is a DynamicBuffer of bytes that acts as a generic pool of memory on entities, where to store polymorphic events.
/// You must ensure this buffer is added to entities that can receive this type of event.
/// IMPORTANT: You must not add any more data to this struct. It needs to remain a single byte.
/// </summary>
[InternalBufferCapacity(0)] // TODO: adjust internal capacity
public struct MyTestEntityPolyEventBufferElement : IBufferElementData
{
    public byte Element;
}

/// <summary>
/// This is an enableable component that flags entities that currently have events to process.
/// You must ensure this component is added to entities that can receive this type of event.
/// </summary>
public struct HasMyTestEntityPolyEvents : IComponentData, IEnableableComponent
{ }


/// <summary>
/// This is the event system that transfers events from the streams to their destination entity buffers.
/// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// </summary>
partial struct MyTestEntityPolyEventSystem : ISystem
{
    private EntityPolymorphicEventSubSystem<MyTestEntityPolyEventsSingleton, MyTestEntityPolyEventBufferElement, HasMyTestEntityPolyEvents, MyTestEntityPolyEventForEntity, PStruct_IMyTestEntityPolyEvent> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new EntityPolymorphicEventSubSystem<MyTestEntityPolyEventsSingleton, MyTestEntityPolyEventBufferElement, HasMyTestEntityPolyEvents, MyTestEntityPolyEventForEntity, PStruct_IMyTestEntityPolyEvent>(
                ref state, 32); // TODO: tweak initial capacities
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