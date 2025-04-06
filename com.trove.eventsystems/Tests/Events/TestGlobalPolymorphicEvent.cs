#if HAS_TROVE_POLYMORPHICSTRUCTS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Trove;
using Trove.EventSystems;
using Trove.EventSystems.Tests;
using Trove.PolymorphicStructs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventTransferPolymorphicStreamToListJob<PStruct_ITestGlobalPolymorphicEvent>))]

namespace Trove.EventSystems.Tests
{
	/// <summary>
	/// This is the singleton containing a manager for this event type.
	/// It is automatically created by the event system for this event type.
	/// Event writers access the event manager in this singleton in order to get streams to write events in.
	/// Event readers access the event manager in this singleton in order to get a list of events to read.
	/// </summary>
	public struct TestGlobalPolymorphicEventsSingleton : IComponentData, IGlobalPolymorphicEventsSingleton
	{
		public StreamEventsManager StreamEventsManager { get; set; }
		public NativeList<byte> EventsList { get; set; }
	}

	[PolymorphicStructInterface]
	public interface ITestGlobalPolymorphicEvent
	{
		public void Execute(ref NativeHashMap<int, int> eventsCounter);
	}

	/// <summary>
	/// This is an example polymorphic event type. You can create more of these containing different data.
	/// </summary>
	[PolymorphicStruct]
	public struct TestGlobalPolymorphicEventA : ITestGlobalPolymorphicEvent
	{
		public int Val;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Execute(ref NativeHashMap<int, int> eventsCounter)
		{
			EventTestUtilities.AddEventToCounter(ref eventsCounter, Val);
		}
	}

	/// <summary>
	/// This is an example polymorphic event type. You can create more of these containing different data.
	/// </summary>
	[PolymorphicStruct]
	public struct TestGlobalPolymorphicEventB : ITestGlobalPolymorphicEvent
	{
		public int Val1;
		public int Val2;
		public int Val3;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Execute(ref NativeHashMap<int, int> eventsCounter)
		{
			EventTestUtilities.AddEventToCounter(ref eventsCounter, Val3);
		}
	}

	/// <summary>
	/// This is the event system that transfers events from the streams to the global events list.
	/// It also clears the events list before adding to it, meaning events from the previous frame are still valid
	/// until this system updates.
	/// All event writer systems should update before this system, and all event reader systems should update after this system.
	/// </summary>
	partial struct TestGlobalPolymorphicEventSystem : ISystem
	{
		private GlobalPolymorphicEventSubSystem<TestGlobalPolymorphicEventsSingleton, PStruct_ITestGlobalPolymorphicEvent>
			_subSystem;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			_subSystem =
				new GlobalPolymorphicEventSubSystem<TestGlobalPolymorphicEventsSingleton,
					PStruct_ITestGlobalPolymorphicEvent>(
					ref state, 32, 1000);
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