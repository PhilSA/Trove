

using Trove.EventSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<XOINK, HasXOINKs>))]
[assembly: RegisterGenericJobType(typeof(EventTransferQueueToBufferJob<XOINKForEntity, XOINK, HasXOINKs>))]
[assembly: RegisterGenericJobType(typeof(EventTransferStreamToBufferJob<XOINKForEntity, XOINK, HasXOINKs>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get queues/streams to write events in.
/// </summary>
public struct XOINKsSingleton : IComponentData, IEntityEventsSingleton<XOINKForEntity>
{
    public QueueEventsManager<XOINKForEntity> QueueEventsManager { get; set; }
    public StreamEventsManager StreamEventsManager { get; set; }
}

/// <summary>
/// This is the event struct that is written by event writers.
/// It contains an "AffectedEntity" field to determine on which Entity the event will be transfered.
/// "Event" represents what actually gets added to the entity's dynamic buffer.
/// </summary>
public struct XOINKForEntity : IEventForEntity<XOINK>
{
    public Entity AffectedEntity { get; set; }
    public XOINK Event { get; set; }
}

/// <summary>
/// This is a DynamicBuffer that stores events on entities.
/// You must ensure this buffer is added to entities that can receive this type of event.
/// </summary>
[InternalBufferCapacity(0)] // TODO: adjust internal capacity
public struct XOINK : IBufferElementData
{
    // TODO: Define event data
    public int Val;
}

/// <summary>
/// This is an enableable component that flags entities that currently have events to process.
/// You must ensure this component is added to entities that can receive this type of event.
/// </summary>
public struct HasXOINKs : IComponentData, IEnableableComponent
{ }

/// <summary>
/// This is the event system that transfers events from the queues/streams to their destination entity buffers.
/// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// </summary>
partial struct XOINKSystem : ISystem
{
    private EntityEventSubSystem<XOINKsSingleton, XOINKForEntity, XOINK, HasXOINKs> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new EntityEventSubSystem<XOINKsSingleton, XOINKForEntity, XOINK, HasXOINKs>(
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

/// <summary>
/// Example of an events writer system
/// </summary>
[UpdateBefore(typeof(XOINKSystem))]
partial struct XOINKWriterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<XOINKsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        XOINKsSingleton eventsSingleton = SystemAPI.GetSingletonRW<XOINKsSingleton>().ValueRW;
        
        // Schedule a job with an events queue gotten from the "QueueEventsManager" in the singleton.
        // Note: for parallel writing, you can get a StreamEventsManager.CreateEventStream() from the singleton instead.
        state.Dependency = new XOINKWriterJob
        {
            EventsQueue  = eventsSingleton.QueueEventsManager.CreateEventQueue(),
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct XOINKWriterJob : IJob
    {
        public NativeQueue<XOINKForEntity> EventsQueue;
        
        public void Execute()
        {
            // Write an example event
            EventsQueue.Enqueue(new XOINKForEntity
            {
                // AffectedEntity = someEntity, // TODO: Find some valid entity with a DynamicBuffer<XOINK> to target
                Event = new XOINK { Val = 1 },
            });
        }
    }
}

/// <summary>
/// Example of an events reader system
/// </summary>
[UpdateAfter(typeof(XOINKSystem))]
partial struct XOINKReaderSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Schedule a job iterating entities with a DynamicBuffer<XOINK> to read events
        state.Dependency = new XOINKReaderJob
        {
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct XOINKReaderJob : IJobEntity
    {
        public void Execute(DynamicBuffer<XOINK> eventsBuffer)
        {
            // Read events
            for (int i = 0; i < eventsBuffer.Length; i++)
            {
                // Debug.Log($"Read XOINK with value: {eventsBuffer[i].Val}");
            }
        }
    }
}