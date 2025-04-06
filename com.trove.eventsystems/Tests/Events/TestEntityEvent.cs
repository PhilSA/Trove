
using Trove.EventSystems;
using Trove.EventSystems.Tests;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<TestEntityEventBufferElement, HasTestEntityEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferQueueToBufferJob<TestEntityEventForEntity, TestEntityEventBufferElement, HasTestEntityEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferStreamToBufferJob<TestEntityEventForEntity, TestEntityEventBufferElement, HasTestEntityEvents>))]

namespace Trove.EventSystems.Tests
{
    /// <summary>
    /// This is the singleton containing a manager for this event type.
    /// It is automatically created by the event system for this event type.
    /// Event writers access the event manager in this singleton in order to get queues/streams to write events in.
    /// </summary>
    public struct TestEntityEventsSingleton : IComponentData, IEntityEventsSingleton<TestEntityEventForEntity, TestEntityEventBufferElement>
    {
        public QueueEventsManager<TestEntityEventForEntity> QueueEventsManager { get; set; }
        public EntityStreamEventsManager<TestEntityEventForEntity, TestEntityEventBufferElement> StreamEventsManager { get; set; }
    }

    /// <summary>
    /// This is the event struct that is written by event writers.
    /// It contains an "AffectedEntity" field to determine on which Entity the event will be transfered.
    /// "BufferElement" represents what actually gets added to the entity's dynamic buffer.
    /// </summary>
    public struct TestEntityEventForEntity : IEventForEntity<TestEntityEventBufferElement>
    {
        public Entity AffectedEntity { get; set; }
        public TestEntityEventBufferElement Event { get; set; }
    }

    /// <summary>
    /// This is a DynamicBuffer that stores events on entities.
    /// You must ensure this buffer is added to entities that can receive this type of event.
    /// </summary>
    public struct TestEntityEventBufferElement : IBufferElementData
    {
        public int Val;
    }

    /// <summary>
    /// This is an enableable component that flags entities that currently have events to process.
    /// You must ensure this component is added to entities that can receive this type of event.
    /// </summary>
    public struct HasTestEntityEvents : IComponentData, IEnableableComponent
    { }

    /// <summary>
    /// This is the event system that transfers events from the queues/streams to their destination entity buffers.
    /// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
    /// until this system updates.
    /// All event writer systems should update before this system, and all event reader systems should update after this system.
    /// </summary>
    partial struct TestEntityEventSystem : ISystem
    {
        private EntityEventSubSystem<TestEntityEventsSingleton, TestEntityEventForEntity, TestEntityEventBufferElement, HasTestEntityEvents> _subSystem;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _subSystem =
                new EntityEventSubSystem<TestEntityEventsSingleton, TestEntityEventForEntity, TestEntityEventBufferElement, HasTestEntityEvents>(
                    ref state, 32, 32);
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