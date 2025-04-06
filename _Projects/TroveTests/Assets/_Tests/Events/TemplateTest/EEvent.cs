

using Trove.EventSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<EEvent, HasEEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferQueueToBufferJob<EEventForEntity, EEvent, HasEEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferStreamToBufferJob<EEventForEntity, EEvent, HasEEvents>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get queues/streams to write events in.
/// </summary>
public struct EEventsSingleton : IComponentData, IEntityEventsSingleton<EEventForEntity, EEvent>
{
    public QueueEventsManager<EEventForEntity> QueueEventsManager { get; set; }
    public EntityStreamEventsManager<EEventForEntity, EEvent> StreamEventsManager { get; set; }
}

/// <summary>
/// This is the event struct that is written by event writers.
/// It contains an "AffectedEntity" field to determine on which Entity the event will be transfered.
/// "Event" represents what actually gets added to the entity's dynamic buffer.
/// </summary>
public struct EEventForEntity : IEventForEntity<EEvent>
{
    public Entity AffectedEntity { get; set; }
    public EEvent Event { get; set; }
}

/// <summary>
/// This is a DynamicBuffer that stores events on entities.
/// You must ensure this buffer is added to entities that can receive this type of event.
/// </summary>
[InternalBufferCapacity(0)] // TODO: adjust internal capacity
public struct EEvent : IBufferElementData
{
    // TODO: Define event data
    public int Val;
}

/// <summary>
/// This is an enableable component that flags entities that currently have events to process.
/// You must ensure this component is added to entities that can receive this type of event.
/// </summary>
public struct HasEEvents : IComponentData, IEnableableComponent
{ }

/// <summary>
/// This is the event system that transfers events from the queues/streams to their destination entity buffers.
/// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// </summary>
partial struct EEventSystem : ISystem
{
    private EntityEventSubSystem<EEventsSingleton, EEventForEntity, EEvent, HasEEvents> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new EntityEventSubSystem<EEventsSingleton, EEventForEntity, EEvent, HasEEvents>(
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
[UpdateBefore(typeof(EEventSystem))]
partial struct EEventWriterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EEventsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        EEventsSingleton eventsSingleton = SystemAPI.GetSingletonRW<EEventsSingleton>().ValueRW;
        
        // Schedule a job writing to an events queue.
        state.Dependency = new EEventQueueWriterJob
        {
            EventsQueue  = eventsSingleton.QueueEventsManager.CreateEventQueue(),
        }.Schedule(state.Dependency);
        
        // Schedule a job writing to an events stream.
        state.Dependency = new EEventStreamWriterJob
        {
            EventsStream  = eventsSingleton.StreamEventsManager.CreateWriter(1),
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct EEventQueueWriterJob : IJob
    {
        public NativeQueue<EEventForEntity> EventsQueue;
        
        public void Execute()
        {
            // Write an example event
            EventsQueue.Enqueue(new EEventForEntity
            {
                // AffectedEntity = someEntity, // TODO: Find some valid entity with a DynamicBuffer<EEvent> to target
                Event = new EEvent { Val = 1 },
            });
        }
    }

    [BurstCompile]
    public struct EEventStreamWriterJob : IJob
    {
        public EntityStreamEventsManager<EEventForEntity, EEvent>.Writer EventsStream;
        
        public void Execute()
        {
            // When writing to a stream, we must begin/end foreach index
            EventsStream.BeginForEachIndex(0);
            
            // Write an example event
            EventsStream.Write(new EEventForEntity
            {
                // AffectedEntity = someEntity, // TODO: Find some valid entity with a DynamicBuffer<EEvent> to target
                Event = new EEvent { Val = 1 },
            });

            EventsStream.EndForEachIndex();
        }
    }
}

/// <summary>
/// Example of an events reader system
/// </summary>
[UpdateAfter(typeof(EEventSystem))]
partial struct EEventReaderSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Schedule a job iterating entities with a DynamicBuffer<EEvent> to read events
        state.Dependency = new EEventReaderJob
        {
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct EEventReaderJob : IJobEntity
    {
        public void Execute(DynamicBuffer<EEvent> eventsBuffer)
        {
            // Read events
            for (int i = 0; i < eventsBuffer.Length; i++)
            {
                // Debug.Log($"Read EEvent with value: {eventsBuffer[i].Val}");
            }
        }
    }
}