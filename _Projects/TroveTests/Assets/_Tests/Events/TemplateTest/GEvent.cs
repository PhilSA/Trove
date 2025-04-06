
using Trove.EventSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearListJob<GEvent>))]
[assembly: RegisterGenericJobType(typeof(EventTransferQueueToListJob<GEvent>))]
[assembly: RegisterGenericJobType(typeof(EventTransferStreamToListJob<GEvent>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type. 
/// Event writers access the event manager in this singleton in order to get queues/streams to write events in.
/// Event readers access the event manager in this singleton in order to get a list of events to read.
/// </summary>
public struct GEventsSingleton : IComponentData, IGlobalEventsSingleton<GEvent>
{
    public QueueEventsManager<GEvent> QueueEventsManager { get; set; }
    public GlobalStreamEventsManager<GEvent> StreamEventsManager { get; set; }
    public NativeList<GEvent> ReadEventsList { get; set; }
}

/// <summary>
/// This is the event struct
/// </summary>
public struct GEvent
{
    // TODO: Define event data
    public int Val;
}

/// <summary>
/// This is the event system that transfers events from the queues/streams to the global events list.
/// It also clears the events list before adding to it, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// </summary>
partial struct GEventSystem : ISystem
{
    private GlobalEventSubSystem<GEventsSingleton, GEvent> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new GlobalEventSubSystem<GEventsSingleton, GEvent>(
                ref state, 32, 32, 1000); // TODO: tweak initial capacities
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
[UpdateBefore(typeof(GEventSystem))]
partial struct GEventWriterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GEventsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        GEventsSingleton eventsSingleton = SystemAPI.GetSingletonRW<GEventsSingleton>().ValueRW;
        
        // Schedule a job writing to an events queue.
        state.Dependency = new GEventQueueWriterJob
        {
            EventsQueue  = eventsSingleton.QueueEventsManager.CreateEventQueue(),
        }.Schedule(state.Dependency);
        
        // Schedule a job writing to an events stream.
        state.Dependency = new GEventStreamWriterJob
        {
            EventsStream  = eventsSingleton.StreamEventsManager.CreateWriter(1),
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct GEventQueueWriterJob : IJob
    {
        public NativeQueue<GEvent> EventsQueue;
        
        public void Execute()
        {
            // Write an example event
            EventsQueue.Enqueue(new GEvent { Val = 1 });
        }
    }

    [BurstCompile]
    public struct GEventStreamWriterJob : IJob
    {
        public GlobalStreamEventsManager<GEvent>.Writer EventsStream;
        
        public void Execute()
        {
            // When writing to a stream, we must begin/end foreach index
            EventsStream.BeginForEachIndex(0);
            
            // Write an example event
            EventsStream.Write(new GEvent { Val = 1 });

            EventsStream.EndForEachIndex();
        }
    }
}

/// <summary>
/// Example of an events reader system
/// </summary>
[UpdateAfter(typeof(GEventSystem))]
partial struct GEventReaderSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GEventsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        GEventsSingleton eventsSingleton = SystemAPI.GetSingletonRW<GEventsSingleton>().ValueRW;
        
        // Schedule a job with the ReadEventsList gotten from the singleton
        state.Dependency = new GEventReaderJob
        {
            ReadEventsList  = eventsSingleton.ReadEventsList,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct GEventReaderJob : IJob
    {
        [ReadOnly]
        public NativeList<GEvent> ReadEventsList;
        
        public void Execute()
        {
            // Read events
            for (int i = 0; i < ReadEventsList.Length; i++)
            {
                // Debug.Log($"Read GEvent with value: {ReadEventsList[i].Val}");
            }
        }
    }
}