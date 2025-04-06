
using System.Runtime.CompilerServices;
using Trove;
using Trove.EventSystems;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Trove.PolymorphicStructs;

// See all TODO comments for things you are expected to modify.

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<JOINKBufferElement, HasJOINKs>))]
[assembly: RegisterGenericJobType(typeof(EventTransferPolymorphicStreamToBufferJob<JOINKBufferElement, HasJOINKs, JOINKForEntity, PStruct_IJOINK>))]

/// <summary>
/// This is the singleton containing a manager for this event type.
/// It is automatically created by the event system for this event type.
/// Event writers access the event manager in this singleton in order to get streams to write events in.
/// </summary>
public struct JOINKsSingleton : IComponentData, IEntityPolymorphicEventsSingleton<JOINKForEntity, PStruct_IJOINK>
{
    public EntityPolymorphicStreamEventsManager<JOINKForEntity, PStruct_IJOINK> StreamEventsManager { get; set; }
}

/// <summary>
/// This is the event struct that is written by event writers.
/// It contains an "AffectedEntity" field to determine on which Entity the event will be transfered.
/// "Event" represents what actually gets serialized to the entity's dynamic buffer.
/// </summary>
public struct JOINKForEntity : IPolymorphicEventForEntity<PStruct_IJOINK>
{
    public Entity AffectedEntity { get; set; }
    public PStruct_IJOINK Event { get; set; }

    public void WriteToStream(ref NativeStream.Writer stream)
    {
        
    }
}

/// <summary>
/// Polymorphic interface used for generating our event polymorphic struct. 
///
/// This will generate a new polymorphic struct named PStruct_IJOINK that can act as any event type implementing
/// this interface and using the [PolymorphicStruct] attribute.
///
/// You can add parameters and return types to the Execute function, or even add new functions. "Execute()" is only
/// a suggestion.
/// </summary>
[PolymorphicStructInterface]
public interface IJOINK
{
    public void Execute();
}

/// <summary>
/// This is an example polymorphic event type. You can create more of these containing different data.
/// </summary>
[PolymorphicStruct]
public struct JOINKA : IJOINK
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
public struct JOINKB : IJOINK
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
public struct JOINKBufferElement : IBufferElementData
{
    public byte Element;
}

/// <summary>
/// This is an enableable component that flags entities that currently have events to process.
/// You must ensure this component is added to entities that can receive this type of event.
/// </summary>
public struct HasJOINKs : IComponentData, IEnableableComponent
{ }


/// <summary>
/// This is the event system that transfers events from the streams to their destination entity buffers.
/// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
/// until this system updates.
/// All event writer systems should update before this system, and all event reader systems should update after this system.
/// </summary>
partial struct JOINKSystem : ISystem
{
    private EntityPolymorphicEventSubSystem<JOINKsSingleton, JOINKBufferElement, HasJOINKs, JOINKForEntity, PStruct_IJOINK> _subSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _subSystem =
            new EntityPolymorphicEventSubSystem<JOINKsSingleton, JOINKBufferElement, HasJOINKs, JOINKForEntity, PStruct_IJOINK>(
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

/// <summary>
/// Example of an events writer system
/// </summary>
[UpdateBefore(typeof(JOINKSystem))]
partial struct JOINKWriterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<JOINKsSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Get the events singleton for this event type
        JOINKsSingleton eventsSingleton = SystemAPI.GetSingletonRW<JOINKsSingleton>().ValueRW;
        
        // Schedule a job with an events queue gotten from the "QueueEventsManager" in the singleton.
        // Note: for parallel writing, you can get a StreamEventsManager.CreateEventStream() from the singleton instead.
        state.Dependency = new JOINKWriterJob
        {
            EventsStream  = eventsSingleton.StreamEventsManager.CreateWriter(1),
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct JOINKWriterJob : IJob
    {
        public EntityPolymorphicStreamEventsManager<JOINKForEntity, PStruct_IJOINK>.Writer EventsStream;
        
        public void Execute()
        {
            // When writing to a stream, we must begin/end foreach index
            EventsStream.BeginForEachIndex(0);
            
            // Write an example event A
            EventsStream.Write(new JOINKForEntity
            {
                // AffectedEntity = someEntity, // TODO: Find some valid entity with a DynamicBuffer<JOINK> to target
                Event = new JOINKA { Val = 1 },
            });
            // Write an example event B
            EventsStream.Write(new JOINKForEntity
            {
                // AffectedEntity = someEntity, // TODO: Find some valid entity with a DynamicBuffer<JOINK> to target
                Event = new JOINKB { Val1 = 3, Val2 = 5, Val3 = 11 },
            });
            
            EventsStream.EndForEachIndex();
        }
    }
}

/// <summary>
/// Example of an events reader system
/// </summary>
[UpdateAfter(typeof(JOINKSystem))]
partial struct JOINKReaderSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Schedule a job iterating entities with a DynamicBuffer<JOINK> to read events
        state.Dependency = new JOINKReaderJob
        {
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct JOINKReaderJob : IJobEntity
    {
        public void Execute(DynamicBuffer<JOINKBufferElement> eventsBuffer)
        {
            // Read and ecexute all events from the bytes buffer
            int readIndex = 0;
            DynamicBuffer<byte> eventsBytesBuffer = eventsBuffer.Reinterpret<byte>();
            while (readIndex < eventsBuffer.Length)
            {
                // Important: when reading polymorphic events from a bytes list, you MUST use "PolymorphicObjectUtilities.GetObject"
                // Get the polymorphic object at the read index, as our event polymorphic struct type
                PolymorphicObjectUtilities.GetObject(ref eventsBytesBuffer, readIndex, out PStruct_IJOINK e, out int readSize);
                // Increment read index by read size
                readIndex += readSize;
                
                // Execute the event (execution logic is implemented in the event struct itself)
                e.Execute();
            }
        }
    }
}