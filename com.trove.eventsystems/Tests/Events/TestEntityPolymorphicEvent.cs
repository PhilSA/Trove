
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Trove.EventSystems;
using Trove.EventSystems.Tests;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

// Register generic job types
[assembly: RegisterGenericJobType(typeof(EventClearBuffersJob<TestEntityPolymorphicEventBufferElement, HasTestEntityPolymorphicEvents>))]
[assembly: RegisterGenericJobType(typeof(EventTransferPolymorphicStreamToBufferJob<TestEntityPolymorphicEventBufferElement, HasTestEntityPolymorphicEvents, TestEntityPolymorphicEventManager>))]

namespace Trove.EventSystems.Tests
{
    /// <summary>
    /// This is the singleton containing a manager for this event type.
    /// It is automatically created by the event system for this event type.
    /// Event writers access the event manager in this singleton in order to get streams to write events in.
    /// </summary>
    public struct TestEntityPolymorphicEventsSingleton : IComponentData, IEntityPolymorphicEventsSingleton
    {
        public StreamEventsManager StreamEventsManager { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    /// <summary>
    /// This is an example polymorphic event type. You can create more of these containing different data.
    /// </summary>
    public struct TestEntityPolymorphicEventA
    {
        public int Val;
    }

    /// <summary>
    /// This is an example polymorphic event type. You can create more of these containing different data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TestEntityPolymorphicEventB
    {
        public int Val1;
        public int Val2;
        public int Val3;
    }

    /// <summary>
    /// This is a DynamicBuffer of bytes that acts as a generic pool of memory on entities, where to store polymorphic events.
    /// You must ensure this buffer is added to entities that can receive this type of event.
    /// IMPORTANT: You must not add any more data to this struct. It needs to remain a single byte.
    /// </summary>
    public struct TestEntityPolymorphicEventBufferElement : IBufferElementData, ISingleByteElement
    {
        public byte Element { get; set; }
    }

    /// <summary>
    /// This is an enableable component that flags entities that currently have events to process.
    /// You must ensure this component is added to entities that can receive this type of event.
    /// </summary>
    public struct HasTestEntityPolymorphicEvents : IComponentData, IEnableableComponent
    { }

    /// <summary>
    /// This is a manager that knows how to write, read, and interpret polymorphic events.
    /// When you want to create a new polymorphic event, you must:
    /// - Create a new struct representing your event and its data.
    /// - Add a new element to the "TypeId" enum for this struct (this gives it a unique Id).
    /// - Add a switch case for this new event TypeId in "GetSizeForTypeId", and make it return the size of that event
    ///   struct.
    /// - Add a "Write" method for that new event struct (you can take inspiration from the existing "Write" method for the
    ///   example event type in the manager.
    //    NOTE: By convention, "Entity Polymorphic Events" must always start by writing their affected entity.
    /// - Add a switch case for this new event TypeId in "ExecuteNextEvent", in order to read and execute the event. (you
    ///   can take inspiration from the example event execution already in "ExecuteNextEvent".
    ///
    /// If your events need parameters for their execution, you are free to add extra parameters to the "ExecuteNextEvent"
    /// function. Your event execution may also return data via an "out" parameter.
    /// </summary>
    public unsafe struct TestEntityPolymorphicEventManager : IPolymorphicEventTypeManager
    {
        // Event types
        public enum TypeId
        {
            TestEntityPolymorphicEventA = 1,
            TestEntityPolymorphicEventB = 2,
        }

        // Event sizes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSizeForTypeId(int typeId)
        {
            switch ((TypeId)typeId)
            {
                case TypeId.TestEntityPolymorphicEventA:
                    return UnsafeUtility.SizeOf<TestEntityPolymorphicEventA>();
                case TypeId.TestEntityPolymorphicEventB:
                    return UnsafeUtility.SizeOf<TestEntityPolymorphicEventB>();
            }

            return 0;
        }

        // Event writers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(ref NativeStream.Writer streamWriter, Entity affectedEntity, TestEntityPolymorphicEventA e)
        {
            streamWriter.Write(affectedEntity); // For "Entity Polymorphic Events", we must always start by writing the affected Entity
            streamWriter.Write<int>((int)TypeId.TestEntityPolymorphicEventA); // Then we write the event type Id
            streamWriter.Write(e); // Finally we write the event data
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(ref NativeStream.Writer streamWriter, Entity affectedEntity, TestEntityPolymorphicEventB e)
        {
            streamWriter.Write(affectedEntity); // For "Entity Polymorphic Events", we must always start by writing the affected Entity
            streamWriter.Write<int>((int)TypeId.TestEntityPolymorphicEventB); // Then we write the event type Id
            streamWriter.Write(e); // Finally we write the event data
        }

        // Event readers and executors
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ExecuteNextEvent(byte* byteArrayPtr, int byteArrayLength, ref int index, ref EntityEventReceiver eventReceiver, int contextType)
        {
            if (PolymorphicUtilities.CanRead<int>(byteArrayLength, index))
            {
                // First we read the event type Id
                PolymorphicUtilities.ReadValue(byteArrayPtr, ref index, out int typeId);

                // Then, depending on type Id, we read the event data in different ways and execute the event
                switch ((TypeId)typeId)
                {
                    // Event A
                    case TypeId.TestEntityPolymorphicEventA:
                        {
                            if (PolymorphicUtilities.CanRead(byteArrayLength, index, UnsafeUtility.SizeOf<TestEntityPolymorphicEventA>()))
                            {
                                PolymorphicUtilities.ReadValue(byteArrayPtr, ref index, out TestEntityPolymorphicEventA e);
                                switch (e.Val)
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
                                return true;
                            }
                            return false;
                        }
                    // Event B
                    case TypeId.TestEntityPolymorphicEventB:
                        {
                            if (PolymorphicUtilities.CanRead(byteArrayLength, index, UnsafeUtility.SizeOf<TestEntityPolymorphicEventB>()))
                            {
                                PolymorphicUtilities.ReadValue(byteArrayPtr, ref index, out TestEntityPolymorphicEventB e);
                                switch (e.Val3)
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
    /// This is the event system that transfers events from the streams to their destination entity buffers.
    /// It also clears all event buffers before adding to them, meaning events from the previous frame are still valid
    /// until this system updates.
    /// All event writer systems should update before this system, and all event reader systems should update after this system.
    /// </summary>
    partial struct TestEntityPolymorphicEventSystem : ISystem
    {
        private EntityPolymorphicEventSubSystem<TestEntityPolymorphicEventsSingleton, TestEntityPolymorphicEventBufferElement, HasTestEntityPolymorphicEvents, TestEntityPolymorphicEventManager> _subSystem;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _subSystem =
                new EntityPolymorphicEventSubSystem<TestEntityPolymorphicEventsSingleton, TestEntityPolymorphicEventBufferElement, HasTestEntityPolymorphicEvents, TestEntityPolymorphicEventManager>(
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