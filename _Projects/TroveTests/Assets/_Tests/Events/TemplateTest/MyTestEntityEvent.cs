

using Trove.EventSystems;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<MyTestEntityEventBufferElement, HasMyTestEntityEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferQueueToBufferJob<MyTestEntityEvent, MyTestEntityEventBufferElement, HasMyTestEntityEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferStreamToBufferJob<MyTestEntityEvent, MyTestEntityEventBufferElement, HasMyTestEntityEvents>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get queues/streams to write events in.
/// </summary>
public struct MyTestEntityEventsSingleton : IComponentData, IEntityEventsSingleton<MyTestEntityEvent>
{
    public QueueEventsManager<MyTestEntityEvent> QueueEventsManager { get; set; }
    public StreamEventsManager StreamEventsManager { get; set; }
}

/// <summary>
/// This is the event struct that is written by event writers.
/// It contains an "AffectedEntity" field to determine on which Entity the event will be transfered.
/// "BufferElement" represents what actually gets added to the entity's dynamic buffer.
/// </summary>
public struct MyTestEntityEvent : IEntityBufferEvent<MyTestEntityEventBufferElement>
{
    public Entity AffectedEntity { get; set; }
    public MyTestEntityEventBufferElement BufferElement { get; set; }
}

/// <summary>
/// This is a DynamicBuffer that stores events on entities.
/// You must ensure this buffer is added to entities that can receive this type of event.
/// </summary>
[InternalBufferCapacity(0)] // TODO: adjust internal capacity
public struct MyTestEntityEventBufferElement : IBufferElementData
{
    // TODO: Define event data
    public int Val;
}

/// <summary>
/// This is an enableable component that flags entities that currently have events to process.
/// You must ensure this component is added to entities that can receive this type of event.
/// </summary>
public struct HasMyTestEntityEvents : IComponentData, IEnableableComponent
{ }

/// <summary>
/// This is the event system that transfers events from the queues/streams to their destination entity buffers.
/// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// </summary>
partial struct MyTestEntityEventSystem : ISystem
{
    private EntityEventSubSystem<MyTestEntityEventsSingleton, MyTestEntityEvent, MyTestEntityEventBufferElement, HasMyTestEntityEvents> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new EntityEventSubSystem<MyTestEntityEventsSingleton, MyTestEntityEvent, MyTestEntityEventBufferElement, HasMyTestEntityEvents>(
                ref state, 32, 32); // TODO: tweak initial capacities
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