
using System.Runtime.CompilerServices;
using Trove;
using Trove.EventSystems;
using Trove.EventSystems.Tests;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventTransferPolymorphicStreamToListJob<TestGlobalPolymorphicEventManager>))]

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

    /// <summary>
    /// This is an example polymorphic event type. You can create more of these containing different data.
    /// </summary>
    public struct TestGlobalPolymorphicEventA
    {
        public int Val;
    }

    /// <summary>
    /// This is an example polymorphic event type. You can create more of these containing different data.
    /// </summary>
    public struct TestGlobalPolymorphicEventB
    {
        public int Val1;
        public int Val2;
        public int Val3;
    }

    /// <summary>
    /// This is a manager that knows how to write, read, and interpret polymorphic events.
    /// When you want to create a new polymorphic event, you must:
    /// - Create a new struct representing your event and its data.
    /// - Add a new element to the "TypeId" enum for this struct (this gives it a unique Id).
    /// - Add a switch case for this new event TypeId in "GetSizeForTypeId", and make it return the size of that event
    ///   struct.
    /// - Add a "Write" method for that new event struct (you can take inspiration from the existing "Write" method for the
    ///   example event type in the manager.
    /// - Add a switch case for this new event TypeId in "ExecuteNextEvent", in order to read and execute the event. (you
    ///   can take inspiration from the example event execution already in "ExecuteNextEvent".
    ///
    /// If your events need parameters for their execution, you are free to add extra parameters to the "ExecuteNextEvent"
    /// function. Your event execution may also return data via an "out" parameter.
    /// </summary>
    public unsafe struct TestGlobalPolymorphicEventManager : IPolymorphicEventTypeManager
    {
        // Event types
        public enum TypeId
        {
            TestGlobalPolymorphicEventA = 1,
            TestGlobalPolymorphicEventB = 2,
        }

        // Event sizes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSizeForTypeId(int typeId)
        {
            switch ((TypeId)typeId)
            {
                case TypeId.TestGlobalPolymorphicEventA:
                    return UnsafeUtility.SizeOf<TestGlobalPolymorphicEventA>();
                case TypeId.TestGlobalPolymorphicEventB:
                    return UnsafeUtility.SizeOf<TestGlobalPolymorphicEventB>();
            }

            return 0;
        }

        // Event writers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(ref NativeStream.Writer streamWriter, TestGlobalPolymorphicEventA e)
        {
            streamWriter.Write<int>((int)TypeId.TestGlobalPolymorphicEventA); // First we write the event type Id
            streamWriter.Write(e); // Then we write the event data
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(ref NativeStream.Writer streamWriter, TestGlobalPolymorphicEventB e)
        {
            streamWriter.Write<int>((int)TypeId.TestGlobalPolymorphicEventB); // First we write the event type Id
            streamWriter.Write(e); // Then we write the event data
        }

        // Event readers and executors
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ExecuteNextEvent(byte* byteArrayPtr, int byteArrayLength, ref int index, ref NativeHashMap<int, int> eventsCounter)
        {
            if (ByteArrayUtilities.CanReadValue<int>(byteArrayLength, index))
            {
                // First we read the event type Id
                ByteArrayUtilities.ReadValue(byteArrayPtr, ref index, out int typeId);

                // Then, depending on type Id, we read the event data in different ways and execute the event
                switch ((TypeId)typeId)
                {
                    // Event A
                    case TypeId.TestGlobalPolymorphicEventA:
                        {
                            if (ByteArrayUtilities.CanReadValue(byteArrayLength, index, UnsafeUtility.SizeOf<TestGlobalPolymorphicEventA>()))
                            {
                                ByteArrayUtilities.ReadValue(byteArrayPtr, ref index, out TestGlobalPolymorphicEventA e);
                                EventTestUtilities.AddEventToCounter(ref eventsCounter, e.Val);
                                return true;
                            }
                            return false;
                        }
                    // Event B
                    case TypeId.TestGlobalPolymorphicEventB:
                        {
                            if (ByteArrayUtilities.CanReadValue(byteArrayLength, index, UnsafeUtility.SizeOf<TestGlobalPolymorphicEventB>()))
                            {
                                ByteArrayUtilities.ReadValue(byteArrayPtr, ref index, out TestGlobalPolymorphicEventB e);
                                EventTestUtilities.AddEventToCounter(ref eventsCounter, e.Val3);
                                return true;
                            }
                            return false;
                        }
                }
            }

            return false;
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
        private GlobalPolymorphicEventSubSystem<TestGlobalPolymorphicEventsSingleton, TestGlobalPolymorphicEventManager> _subSystem;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _subSystem =
                new GlobalPolymorphicEventSubSystem<TestGlobalPolymorphicEventsSingleton, TestGlobalPolymorphicEventManager>(
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