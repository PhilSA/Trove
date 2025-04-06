
using System.Runtime.CompilerServices;
using Trove;
using Trove.EventSystems;
using Trove.EventSystems.Tests;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Trove.PolymorphicStructs;
using UnityEngine;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventTransferPolymorphicStreamToListJob<PStruct_IGPEvent>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get streams to write events in.
/// Event readers access the event manager in this singleton in order to get a list of events to read.
/// </summary>
public struct GPEventsSingleton : IComponentData, IGlobalPolymorphicEventsSingleton<PStruct_IGPEvent>
{
    public GlobalPolymorphicStreamEventsManager<PStruct_IGPEvent> StreamEventsManager { get; set; }
    public NativeList<byte> ReadEventsList { get; set; }
}

/// <summary>
/// Polymorphic interface used for generating our event polymorphic struct. 
///
/// This will generate a new polymorphic struct named PStruct_IGPEvent that can act as any event type implementing
/// this interface and using the [PolymorphicStruct] attribute.
///
/// You can add parameters and return types to the Execute function, or even add new functions. "Execute()" is only
/// a suggestion.
/// </summary>
[PolymorphicStructInterface]
public interface IGPEvent
{
    public void Execute();
}

/// <summary>
/// This is an example polymorphic event type. You can create more of these containing different data.
/// </summary>
[PolymorphicStruct]
public struct GPEventA : IGPEvent
{
    // TODO: Define event data
    public int Val;
    
    public void Execute()
    {
        // TODO: implement event execution
        // Debug.Log($"Executing GPEventA with value: {Val}");
    }
}

/// <summary>
/// This is an example polymorphic event type. You can create more of these containing different data.
/// </summary>
[PolymorphicStruct]
public struct GPEventB : IGPEvent
{
    // TODO: Define event data
    public int Val1;
    public int Val2;
    public int Val3;
    
    public void Execute()
    {
        // TODO: implement event execution
        // Debug.Log($"Executing GPEventB with values: {Val1} {Val2} {Val3}");
    }
}

// TODO: Define more polymorphic event structs

/// <summary>
/// This is the event system that transfers events from the streams to the global events list.
/// It also clears the events list before adding to it, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// </summary>
partial struct GPEventSystem : ISystem
{
    private GlobalPolymorphicEventSubSystem<GPEventsSingleton, PStruct_IGPEvent> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new GlobalPolymorphicEventSubSystem<GPEventsSingleton, PStruct_IGPEvent>(
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

/// <summary>
/// Example of an events writer system
/// </summary>
[UpdateBefore(typeof(GPEventSystem))]
partial struct GPEventWriterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GPEventsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        GPEventsSingleton eventsSingleton = SystemAPI.GetSingletonRW<GPEventsSingleton>().ValueRW;
        
        // Schedule a job writing to an events stream.
        state.Dependency = new GPEventWriterJob
        {
            EventsStream  = eventsSingleton.StreamEventsManager.CreateWriter(1),
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct GPEventWriterJob : IJob
    {
        public GlobalPolymorphicStreamEventsManager<PStruct_IGPEvent>.Writer EventsStream;
        
        public void Execute()
        {
            // When writing to a stream, we must begin/end foreach index
            EventsStream.BeginForEachIndex(0);
            
            // Write an example event A
            EventsStream.Write(new GPEventA { Val = 1 });
            // Write an example event B
            EventsStream.Write(new GPEventB { Val1 = 3, Val2 = 5, Val3 = 11 });
            
            EventsStream.EndForEachIndex();
        }
    }
}

/// <summary>
/// Example of an events reader system
/// </summary>
[UpdateAfter(typeof(GPEventSystem))]
partial struct GPEventReaderSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GPEventsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        GPEventsSingleton eventsSingleton = SystemAPI.GetSingletonRW<GPEventsSingleton>().ValueRW;
        
        // Schedule a job with the ReadEventsList gotten from the singleton.
        // Note: for polymorphic events, the read list is always just a list of bytes.
        state.Dependency = new GPEventReaderJob
        {
            ReadEventsList  = eventsSingleton.ReadEventsList,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct GPEventReaderJob : IJob
    {
        [ReadOnly]
        public NativeList<byte> ReadEventsList;
        
        public void Execute()
        {
            // Get the iterator that can read through the polymorphic structs of the list
            PolymorphicObjectNativeListIterator<PStruct_IGPEvent> iterator = 
                PolymorphicObjectUtilities.GetIterator<PStruct_IGPEvent>(ReadEventsList);
            while (iterator.GetNext(out PStruct_IGPEvent e, out _, out _))
            {
                // Execute the event (execution logic is implemented in the event struct itself)
                e.Execute();
            }
        }
    }
}