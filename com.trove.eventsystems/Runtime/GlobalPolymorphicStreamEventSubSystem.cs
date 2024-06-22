
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Trove.EventSystems
{
    public unsafe struct GlobalPolymorphicEventSubSystem<S, P>
        where S : unmanaged, IComponentData, IGlobalPolymorphicEventsSingleton // The events singleton
        where P : unmanaged, IPolymorphicEventTypeManager
    {
        private EntityQuery _singletonRWQuery;
        private NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;
        private NativeList<byte> _eventList;

        public GlobalPolymorphicEventSubSystem(ref SystemState state, int initialStreamsCapacity, int initialEventsListCapacity)
        {
            state.RequireForUpdate<S>();
            _singletonRWQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<S>().Build(ref state);

            _eventStreamsReference = new NativeReference<UnsafeList<NativeStream>>(
                new UnsafeList<NativeStream>(initialStreamsCapacity, Allocator.Persistent),
                Allocator.Persistent);
            _eventList = new NativeList<byte>(initialEventsListCapacity, Allocator.Persistent);

            // Create the event singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            S singleton = default(S);
            singleton.StreamEventsManager = new StreamEventsManager(
                _eventStreamsReference,
                ref state);
            singleton.EventsList = _eventList;
            state.EntityManager.AddComponentData(singletonEntity, singleton);
        }

        public void OnDestroy(ref SystemState state)
        {
            // Dispose streams
            if (_eventStreamsReference.GetUnsafePtr()->IsCreated)
            {
                UnsafeList<NativeStream> eventStreams = _eventStreamsReference.Value;
                for (int i = 0; i < eventStreams.Length; i++)
                {
                    if (eventStreams[i].IsCreated)
                    {
                        eventStreams[i].Dispose();
                    }
                }

                _eventStreamsReference.GetUnsafePtr()->Dispose();
            }

            if (_eventList.IsCreated)
            {
                _eventList.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<S> singletonRW = _singletonRWQuery.GetSingletonRW<S>();

            state.Dependency = new EventClearListJob<byte>
            {
                EventList = singletonRW.ValueRW.EventsList,
            }.Schedule(state.Dependency);

            UnsafeList<NativeStream> eventStreams = singletonRW.ValueRW.StreamEventsManager.InternalGetEventStreams();
            for (int i = 0; i < eventStreams.Length; i++)
            {
                state.Dependency = new EventTransferPolymorphicStreamToListJob<P>
                {
                    PolymorphicTypeManager = default(P),
                    EventsStream = eventStreams[i].AsReader(),
                    EventList = singletonRW.ValueRW.EventsList,
                }.Schedule(state.Dependency);
            }

            state.Dependency = singletonRW.ValueRW.StreamEventsManager.ScheduleClearWriterCollections(state.Dependency);
        }
    }

    [BurstCompile]
    public unsafe struct EventTransferPolymorphicStreamToListJob<P> : IJob
        where P : unmanaged, IPolymorphicEventTypeManager
    {
        public P PolymorphicTypeManager;
        public NativeStream.Reader EventsStream;
        public NativeList<byte> EventList;

        public void Execute()
        {
            int writeIndex = EventList.Length;
            byte* listPtr = EventList.GetUnsafePtr();

            for (int i = 0; i < EventsStream.ForEachCount; i++)
            {
                EventsStream.BeginForEachIndex(i);
                while (EventsStream.RemainingItemCount > 1)
                {
                    // Read from stream
                    int typeId = EventsStream.Read<int>();
                    int eventDataSize = PolymorphicTypeManager.GetSizeForTypeId(typeId);
                    byte* eventData = EventsStream.ReadUnsafePtr(eventDataSize);

                    // List resize
                    int newListSize = EventList.Length + UnsafeUtility.SizeOf<int>() + eventDataSize;
                    if (newListSize > EventList.Capacity)
                    {
                        EventList.SetCapacity(newListSize * 2);
                        listPtr = EventList.GetUnsafePtr();
                    }
                    EventList.ResizeUninitialized(newListSize);

                    // Write to list
                    ByteArrayUtilities.WriteValue(listPtr, ref writeIndex, typeId);
                    ByteArrayUtilities.WriteValue(listPtr, ref writeIndex, eventData, eventDataSize);
                }
                EventsStream.EndForEachIndex();
            }
        }
    }
}