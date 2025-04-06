
using System.Runtime.CompilerServices;
using Trove;
using Trove.EventSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Trove.PolymorphicStructs;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventTransferPolyByteArrayStreamToListJob<PolyMyGlobalPolyEvent>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get streams to write events in.
/// Event readers access the event manager in this singleton in order to get a list of events to read.
/// </summary>
public struct MyGlobalPolyEventsSingleton : IComponentData, IGlobalPolyByteArrayEventsSingleton<PolyMyGlobalPolyEvent>
{
    public GlobalPolyByteArrayStreamEventsManager<PolyMyGlobalPolyEvent> StreamEventsManager { get; set; }
    public NativeList<byte> ReadEventsList { get; set; }
}

/// <summary>
/// Polymorphic interface used for generating our event polymorphic struct. 
///
/// This will generate a new polymorphic struct named PolyMyGlobalPolyEvent that can act as any event type implementing
/// this interface and using the [PolymorphicStruct] attribute.
///
/// You can add parameters and return types to the Execute function, or even add new functions. "Execute()" is only
/// a suggestion.
/// </summary>
[PolymorphicStructInterface]
public interface IMyGlobalPolyEvent
{
    public void Execute();
}

/// <summary>
/// This is an example polymorphic event type. You can create more of these containing different data.
/// </summary>
[PolymorphicStruct]
public struct MyGlobalPolyEventA : IMyGlobalPolyEvent
{
    // TODO: Define event data
    public int Val;
    
    public void Execute()
    {
        // TODO: implement event execution
        // Debug.Log($"Executing MyGlobalPolyEventA with value: {Val}");
    }
}

/// <summary>
/// This is an example polymorphic event type. You can create more of these containing different data.
/// </summary>
[PolymorphicStruct]
public struct MyGlobalPolyEventB : IMyGlobalPolyEvent
{
    // TODO: Define event data
    public int Val1;
    public int Val2;
    public int Val3;
    
    public void Execute()
    {
        // TODO: implement event execution
        // Debug.Log($"Executing MyGlobalPolyEventB with values: {Val1} {Val2} {Val3}");
    }
}

// TODO: Define more polymorphic event structs

/// <summary>
/// This is the event system that transfers events from the streams to the global events list.
/// It also clears the events list before adding to it, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// TODO: You can change the update order of this system.
/// </summary>
partial struct MyGlobalPolyEventSystem : ISystem
{
    private GlobalPolyByteArrayEventSubSystem<MyGlobalPolyEventsSingleton, PolyMyGlobalPolyEvent> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new GlobalPolyByteArrayEventSubSystem<MyGlobalPolyEventsSingleton, PolyMyGlobalPolyEvent>(
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
/// Example of an events writer system.
/// TODO: Delete or change or move elsewhere.
/// </summary>
[UpdateBefore(typeof(MyGlobalPolyEventSystem))]
partial struct ExampleMyGlobalPolyEventWriterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MyGlobalPolyEventsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        MyGlobalPolyEventsSingleton eventsSingleton = SystemAPI.GetSingletonRW<MyGlobalPolyEventsSingleton>().ValueRW;
        
        // Schedule a job writing to an events stream.
        state.Dependency = new MyGlobalPolyEventWriterJob
        {
            EventsStream  = eventsSingleton.StreamEventsManager.CreateWriter(1),
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct MyGlobalPolyEventWriterJob : IJob
    {
        public GlobalPolyByteArrayStreamEventsManager<PolyMyGlobalPolyEvent>.Writer EventsStream;
        
        public void Execute()
        {
            // When writing to a stream, we must begin/end foreach index
            EventsStream.BeginForEachIndex(0);
            
            // Write an example event A
            EventsStream.Write(new MyGlobalPolyEventA { Val = 1 });
            // Write an example event B
            EventsStream.Write(new MyGlobalPolyEventB { Val1 = 3, Val2 = 5, Val3 = 11 });
            
            EventsStream.EndForEachIndex();
        }
    }
}

/// <summary>
/// Example of an events reader system.
/// TODO: Delete or change or move elsewhere.
/// </summary>
[UpdateAfter(typeof(MyGlobalPolyEventSystem))]
partial struct ExampleMyGlobalPolyEventReaderSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MyGlobalPolyEventsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        MyGlobalPolyEventsSingleton eventsSingleton = SystemAPI.GetSingletonRW<MyGlobalPolyEventsSingleton>().ValueRW;
        
        // Schedule a job with the ReadEventsList gotten from the singleton.
        state.Dependency = new MyGlobalPolyEventReaderJob
        {
            ReadEventsList  = eventsSingleton.ReadEventsList,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct MyGlobalPolyEventReaderJob : IJob
    {
        [ReadOnly]
        public NativeList<byte> ReadEventsList;
        
        public void Execute()
        {
            // Get the iterator that can read through the polymorphic structs of the list
            PolymorphicObjectNativeListIterator<PolyMyGlobalPolyEvent> iterator = 
                PolymorphicObjectUtilities.GetIterator<PolyMyGlobalPolyEvent>(ReadEventsList);
            while (iterator.GetNext(out PolyMyGlobalPolyEvent e, out _, out _))
            {
                // Execute the event (execution logic is implemented in the event struct itself)
                e.Execute();
            }
        }
    }
}