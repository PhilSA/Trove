

using Trove.EventSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<MyEntityEvent, HasMyEntityEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferQueueToBufferJob<MyEntityEventForEntity, MyEntityEvent, HasMyEntityEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferStreamToBufferJob<MyEntityEventForEntity, MyEntityEvent, HasMyEntityEvents>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get queues/streams to write events in.
/// </summary>
public struct MyEntityEventsSingleton : IComponentData, IEntityEventsSingleton<MyEntityEventForEntity, MyEntityEvent>
{
    public QueueEventsManager<MyEntityEventForEntity> QueueEventsManager { get; set; }
    public EntityStreamEventsManager<MyEntityEventForEntity, MyEntityEvent> StreamEventsManager { get; set; }
}

/// <summary>
/// This is the event struct that is written by event writers.
/// It contains an "AffectedEntity" field to determine on which Entity the event will be transfered.
/// "Event" represents what actually gets added to the entity's dynamic buffer.
/// </summary>
public struct MyEntityEventForEntity : IEventForEntity<MyEntityEvent>
{
    public Entity AffectedEntity { get; set; }
    public MyEntityEvent Event { get; set; }
}

/// <summary>
/// This is a DynamicBuffer that stores events on entities.
/// You must ensure this buffer is added to entities that can receive this type of event.
/// </summary>
[InternalBufferCapacity(0)] // TODO: adjust internal capacity
public struct MyEntityEvent : IBufferElementData
{
    // TODO: Define event data
    public int Val;
}

/// <summary>
/// This is an enableable component that flags entities that currently have events to process.
/// You must ensure this component is added to entities that can receive this type of event.
/// </summary>
public struct HasMyEntityEvents : IComponentData, IEnableableComponent
{ }

/// <summary>
/// This is the event system that transfers events from the queues/streams to their destination entity buffers.
/// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// TODO: You can change the update order of this system.
/// </summary>
partial struct MyEntityEventSystem : ISystem
{
    private EntityEventSubSystem<MyEntityEventsSingleton, MyEntityEventForEntity, MyEntityEvent, HasMyEntityEvents> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new EntityEventSubSystem<MyEntityEventsSingleton, MyEntityEventForEntity, MyEntityEvent, HasMyEntityEvents>(
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
/// Example of an events writer system.
/// TODO: Delete or change or move elsewhere.
/// </summary>
[UpdateBefore(typeof(MyEntityEventSystem))]
partial struct ExampleMyEntityEventWriterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MyEntityEventsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        MyEntityEventsSingleton eventsSingleton = SystemAPI.GetSingletonRW<MyEntityEventsSingleton>().ValueRW;
        
        // Schedule a job writing to an events queue.
        state.Dependency = new MyEntityEventQueueWriterJob
        {
            EventsQueue  = eventsSingleton.QueueEventsManager.CreateWriter(),
        }.Schedule(state.Dependency);
        
        // Schedule a job writing to an events stream.
        state.Dependency = new MyEntityEventStreamWriterJob
        {
            EventsStream  = eventsSingleton.StreamEventsManager.CreateWriter(1),
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct MyEntityEventQueueWriterJob : IJob
    {
        public NativeQueue<MyEntityEventForEntity> EventsQueue;
        
        public void Execute()
        {
            // Write an example event
            EventsQueue.Enqueue(new MyEntityEventForEntity
            {
                // AffectedEntity = someEntity, // TODO: Find some valid entity with a DynamicBuffer<MyEntityEvent> to target
                Event = new MyEntityEvent { Val = 1 },
            });
        }
    }

    [BurstCompile]
    public struct MyEntityEventStreamWriterJob : IJob
    {
        public EntityStreamEventsManager<MyEntityEventForEntity, MyEntityEvent>.Writer EventsStream;
        
        public void Execute()
        {
            // When writing to a stream, we must begin/end foreach index
            EventsStream.BeginForEachIndex(0);
            
            // Write an example event
            EventsStream.Write(new MyEntityEventForEntity
            {
                // AffectedEntity = someEntity, // TODO: Find some valid entity with a DynamicBuffer<MyEntityEvent> to target
                Event = new MyEntityEvent { Val = 1 },
            });

            EventsStream.EndForEachIndex();
        }
    }
}

/// <summary>
/// Example of an events reader system
/// TODO: Delete or change or move elsewhere.
/// </summary>
[UpdateAfter(typeof(MyEntityEventSystem))]
partial struct ExampleMyEntityEventReaderSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Schedule a job iterating entities with a DynamicBuffer<MyEntityEvent> to read events
        state.Dependency = new MyEntityEventReaderJob
        {
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(HasMyEntityEvents))] // This ensures we only iterate entities that have received events
    public partial struct MyEntityEventReaderJob : IJobEntity
    {
        public void Execute(DynamicBuffer<MyEntityEvent> eventsBuffer)
        {
            // Read events
            for (int i = 0; i < eventsBuffer.Length; i++)
            {
                // Debug.Log($"Read MyEntityEvent with value: {eventsBuffer[i].Val}");
            }
        }
    }
}