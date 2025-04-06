#if HAS_TROVE_POLYMORPHICSTRUCTS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Trove.EventSystems;
using Trove.EventSystems.Tests;
using Trove.PolymorphicStructs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<TestEntityPolymorphicEventBufferElement, HasTestEntityPolymorphicEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferPolymorphicStreamToBufferJob<TestEntityPolymorphicEventBufferElement, HasTestEntityPolymorphicEvents, TestEntityPolymorphicEventForEntity, PStruct_ITestEntityPolymorphicEvent>))]

namespace Trove.EventSystems.Tests
{
    /// <summary>
    /// This is the singleton containing a manager for this event type.
    /// It is automatically created by the event system for this event type.
    /// Event writers access the event manager in this singleton in order to get streams to write events in.
    /// </summary>
    public struct TestEntityPolymorphicEventsSingleton : IComponentData, IEntityPolymorphicEventsSingleton<TestEntityPolymorphicEventForEntity, PStruct_ITestEntityPolymorphicEvent>
    {
        public EntityPolymorphicStreamEventsManager<TestEntityPolymorphicEventForEntity, PStruct_ITestEntityPolymorphicEvent> StreamEventsManager { get; set; }
    }

    /// <summary>
    /// This is the event struct that is written by event writers.
    /// It contains an "AffectedEntity" field to determine on which Entity the event will be transfered.
    /// "BufferElement" represents what actually gets added to the entity's dynamic buffer.
    /// </summary>
    public struct TestEntityPolymorphicEventForEntity : IPolymorphicEventForEntity<PStruct_ITestEntityPolymorphicEvent>
    {
	    public Entity AffectedEntity { get; set; }
	    public PStruct_ITestEntityPolymorphicEvent Event { get; set; }
    }

    /// <summary>
    /// Polymorphic interface used for generating our event polymorphic struct
    /// </summary>
    [PolymorphicStructInterface]
    public interface ITestEntityPolymorphicEvent
    {
	    public void Execute(ref EntityEventReceiver eventReceiver, int contextType);
    }

    /// <summary>
    /// This is an example polymorphic event type. You can create more of these containing different data.
    /// </summary>
    [PolymorphicStruct]
    public struct TestEntityPolymorphicEventA : ITestEntityPolymorphicEvent
    {
	    public int Val;

	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    public void Execute(ref EntityEventReceiver eventReceiver, int contextType)
	    {
		    switch (Val)
		    {
			    case 1:
				    switch (contextType)
				    {
					    case 1:
						    eventReceiver.MainThread_EventCounterVal1++;
						    break;
					    case 2:
						    eventReceiver.SingleJob_EventCounterVal1++;
						    break;
					    case 3:
						    eventReceiver.ParallelJob_EventCounterVal1++;
						    break;
				    }

				    break;
			    case 2:
				    switch (contextType)
				    {
					    case 1:
						    eventReceiver.MainThread_EventCounterVal2++;
						    break;
					    case 2:
						    eventReceiver.SingleJob_EventCounterVal2++;
						    break;
					    case 3:
						    eventReceiver.ParallelJob_EventCounterVal2++;
						    break;
				    }

				    break;
			    case 3:
				    switch (contextType)
				    {
					    case 1:
						    eventReceiver.MainThread_EventCounterVal3++;
						    break;
					    case 2:
						    eventReceiver.SingleJob_EventCounterVal3++;
						    break;
					    case 3:
						    eventReceiver.ParallelJob_EventCounterVal3++;
						    break;
				    }

				    break;
			    case 4:
				    switch (contextType)
				    {
					    case 1:
						    eventReceiver.MainThread_EventCounterVal4++;
						    break;
					    case 2:
						    eventReceiver.SingleJob_EventCounterVal4++;
						    break;
					    case 3:
						    eventReceiver.ParallelJob_EventCounterVal4++;
						    break;
				    }

				    break;
			    case 5:
				    switch (contextType)
				    {
					    case 1:
						    eventReceiver.MainThread_EventCounterVal5++;
						    break;
					    case 2:
						    eventReceiver.SingleJob_EventCounterVal5++;
						    break;
					    case 3:
						    eventReceiver.ParallelJob_EventCounterVal5++;
						    break;
				    }

				    break;
			    case 6:
				    switch (contextType)
				    {
					    case 1:
						    eventReceiver.MainThread_EventCounterVal6++;
						    break;
					    case 2:
						    eventReceiver.SingleJob_EventCounterVal6++;
						    break;
					    case 3:
						    eventReceiver.ParallelJob_EventCounterVal6++;
						    break;
				    }

				    break;
		    }
	    }
    }

    /// <summary>
	/// This is an example polymorphic event type. You can create more of these containing different data.
	/// </summary>
	[PolymorphicStruct]
	public struct TestEntityPolymorphicEventB : ITestEntityPolymorphicEvent
	{
		public int Val1;
		public int Val2;
		public int Val3;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Execute(ref EntityEventReceiver eventReceiver, int contextType)
		{
			switch (Val3)
			{
				case 1:
					switch (contextType)
					{
						case 1:
							eventReceiver.MainThread_EventCounterVal1++;
							break;
						case 2:
							eventReceiver.SingleJob_EventCounterVal1++;
							break;
						case 3:
							eventReceiver.ParallelJob_EventCounterVal1++;
							break;
					}

					break;
				case 2:
					switch (contextType)
					{
						case 1:
							eventReceiver.MainThread_EventCounterVal2++;
							break;
						case 2:
							eventReceiver.SingleJob_EventCounterVal2++;
							break;
						case 3:
							eventReceiver.ParallelJob_EventCounterVal2++;
							break;
					}

					break;
				case 3:
					switch (contextType)
					{
						case 1:
							eventReceiver.MainThread_EventCounterVal3++;
							break;
						case 2:
							eventReceiver.SingleJob_EventCounterVal3++;
							break;
						case 3:
							eventReceiver.ParallelJob_EventCounterVal3++;
							break;
					}

					break;
				case 4:
					switch (contextType)
					{
						case 1:
							eventReceiver.MainThread_EventCounterVal4++;
							break;
						case 2:
							eventReceiver.SingleJob_EventCounterVal4++;
							break;
						case 3:
							eventReceiver.ParallelJob_EventCounterVal4++;
							break;
					}

					break;
				case 5:
					switch (contextType)
					{
						case 1:
							eventReceiver.MainThread_EventCounterVal5++;
							break;
						case 2:
							eventReceiver.SingleJob_EventCounterVal5++;
							break;
						case 3:
							eventReceiver.ParallelJob_EventCounterVal5++;
							break;
					}

					break;
				case 6:
					switch (contextType)
					{
						case 1:
							eventReceiver.MainThread_EventCounterVal6++;
							break;
						case 2:
							eventReceiver.SingleJob_EventCounterVal6++;
							break;
						case 3:
							eventReceiver.ParallelJob_EventCounterVal6++;
							break;
					}

					break;
			}
		}
	}

    /// <summary>
    /// This is a DynamicBuffer of bytes that acts as a generic pool of memory on entities, where to store polymorphic events.
    /// You must ensure this buffer is added to entities that can receive this type of event.
    /// IMPORTANT: You must not add any more data to this struct. It needs to remain a single byte.
    /// </summary>
    public struct TestEntityPolymorphicEventBufferElement : IBufferElementData
    {
	    public byte Element;
    }

    /// <summary>
    /// This is an enableable component that flags entities that currently have events to process.
    /// You must ensure this component is added to entities that can receive this type of event.
    /// </summary>
    public struct HasTestEntityPolymorphicEvents : IComponentData, IEnableableComponent
    { }

    /// <summary>
    /// This is the event system that transfers events from the streams to their destination entity buffers.
    /// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
    /// until this system updates.
    /// All event writer systems should update before this system, and all event reader systems should update after this system.
    /// </summary>
    partial struct TestEntityPolymorphicEventSystem : ISystem
    {
        private EntityPolymorphicEventSubSystem<TestEntityPolymorphicEventsSingleton, TestEntityPolymorphicEventBufferElement, HasTestEntityPolymorphicEvents, TestEntityPolymorphicEventForEntity, PStruct_ITestEntityPolymorphicEvent> _subSystem;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _subSystem =
                new EntityPolymorphicEventSubSystem<TestEntityPolymorphicEventsSingleton, TestEntityPolymorphicEventBufferElement, HasTestEntityPolymorphicEvents, TestEntityPolymorphicEventForEntity, PStruct_ITestEntityPolymorphicEvent>(
                    ref state, 32); 
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
#endif