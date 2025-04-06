
using Trove.EventSystems;
using Trove.EventSystems.Tests;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearListJob<TestGlobalEvent>))]
[assembly: RegisterGenericJobType(typeof(EventTransferQueueToListJob<TestGlobalEvent>))]
[assembly: RegisterGenericJobType(typeof(EventTransferStreamToListJob<TestGlobalEvent>))]

namespace Trove.EventSystems.Tests
{
    /// <summary>
    /// This is the singleton containing a manager for this event type.
    /// It is automatically created by the event system for this event type. 
    /// Event writers access the event manager in this singleton in order to get queues/streams to write events in.
    /// Event readers access the event manager in this singleton in order to get a list of events to read.
    /// </summary>
    public struct TestGlobalEventsSingleton : IComponentData, IGlobalEventsSingleton<TestGlobalEvent>
    {
        public QueueEventsManager<TestGlobalEvent> QueueEventsManager { get; set; }
        public StreamEventsManager StreamEventsManager { get; set; }
        public NativeList<TestGlobalEvent> ReadEventsList { get; set; }
    }

    /// <summary>
    /// This is the event struct
    /// </summary>
    public struct TestGlobalEvent
    {
        public int Val;
    }

    /// <summary>
    /// This is the event system that transfers events from the queues/streams to the global events list.
    /// It also clears the events list before adding to it, meaning events from the previous frame are still valid
    /// until this system updates.
    /// All event writer systems should update before this system, and all event reader systems should update after this system.
    /// </summary>
    partial struct TestGlobalEventSystem : ISystem
    {
        private GlobalEventSubSystem<TestGlobalEventsSingleton, TestGlobalEvent> _subSystem;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _subSystem =
                new GlobalEventSubSystem<TestGlobalEventsSingleton, TestGlobalEvent>(
                    ref state, 32, 32, 1000);
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
}