
using Trove.EventSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearListJob<ZOINK>))]
[assembly: RegisterGenericJobType(typeof(EventTransferQueueToListJob<ZOINK>))]
[assembly: RegisterGenericJobType(typeof(EventTransferStreamToListJob<ZOINK>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type. 
/// Event writers access the event manager in this singleton in order to get queues/streams to write events in.
/// Event readers access the event manager in this singleton in order to get a list of events to read.
/// </summary>
public struct ZOINKsSingleton : IComponentData, IGlobalEventsSingleton<ZOINK>
{
    public QueueEventsManager<ZOINK> QueueEventsManager { get; set; }
    public StreamEventsManager StreamEventsManager { get; set; }
    public NativeList<ZOINK> ReadEventsList { get; set; }
}

/// <summary>
/// This is the event struct
/// </summary>
public struct ZOINK
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
partial struct ZOINKSystem : ISystem
{
    private GlobalEventSubSystem<ZOINKsSingleton, ZOINK> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new GlobalEventSubSystem<ZOINKsSingleton, ZOINK>(
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
[UpdateBefore(typeof(ZOINKSystem))]
partial struct ZOINKWriterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZOINKsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        ZOINKsSingleton eventsSingleton = SystemAPI.GetSingletonRW<ZOINKsSingleton>().ValueRW;
        
        // Schedule a job with an events queue gotten from the "QueueEventsWriter" in the singleton.
        // Note: for parallel writing, you can get a StreamEventsWriter.CreateEventStream() from the singleton instead.
        state.Dependency = new ZOINKWriterJob
        {
            EventsQueue  = eventsSingleton.QueueEventsManager.CreateEventQueue(),
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct ZOINKWriterJob : IJob
    {
        public NativeQueue<ZOINK> EventsQueue;
        
        public void Execute()
        {
            // Write an example event
            EventsQueue.Enqueue(new ZOINK { Val = 1 });
        }
    }
}

/// <summary>
/// Example of an events reader system
/// </summary>
[UpdateAfter(typeof(ZOINKSystem))]
partial struct ZOINKReaderSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZOINKsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        ZOINKsSingleton eventsSingleton = SystemAPI.GetSingletonRW<ZOINKsSingleton>().ValueRW;
        
        // Schedule a job with the ReadEventsList gotten from the singleton
        state.Dependency = new ZOINKReaderJob
        {
            ReadEventsList  = eventsSingleton.ReadEventsList,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct ZOINKReaderJob : IJob
    {
        [ReadOnly]
        public NativeList<ZOINK> ReadEventsList;
        
        public void Execute()
        {
            // Read events
            for (int i = 0; i < ReadEventsList.Length; i++)
            {
                // Debug.Log($"Read ZOINK with value: {EventsList[i].Val}");
            }
        }
    }
}