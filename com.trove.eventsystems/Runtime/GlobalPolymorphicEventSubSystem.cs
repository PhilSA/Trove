
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Trove.EventSystems
{
    public unsafe struct GlobalPolymorphicEventSubSystem<S, E>
        where S : unmanaged, IComponentData, IGlobalPolymorphicEventsSingleton // The events singleton
        where E : unmanaged, IPolymorphicObject
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
            singleton.ReadEventsList = _eventList;
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
                EventList = singletonRW.ValueRW.ReadEventsList,
            }.Schedule(state.Dependency);

            UnsafeList<NativeStream> eventStreams = singletonRW.ValueRW.StreamEventsManager.InternalGetEventStreams();
            for (int i = 0; i < eventStreams.Length; i++)
            {
                state.Dependency = new EventTransferPolymorphicStreamToListJob<E>
                {
                    EventsStream = eventStreams[i].AsReader(),
                    EventList = singletonRW.ValueRW.ReadEventsList,
                }.Schedule(state.Dependency);
            }

            state.Dependency = singletonRW.ValueRW.StreamEventsManager.ScheduleClearWriterCollections(state.Dependency);
        }
    }

    [BurstCompile]
    public unsafe struct EventTransferPolymorphicStreamToListJob<P> : IJob
        where P : unmanaged, IPolymorphicObject
    {
        public NativeStream.Reader EventsStream;
        public NativeList<byte> EventList;

        public void Execute()
        {
            for (int i = 0; i < EventsStream.ForEachCount; i++)
            {
                EventsStream.BeginForEachIndex(i);
                while (EventsStream.RemainingItemCount > 0)
                {
                    PolymorphicObjectUtilities.GetNextObject(ref EventsStream, out P polymorphicObject, out int readSize);
                    PolymorphicObjectUtilities.AddObject(polymorphicObject, ref EventList, out int addedByteIndex, out int writeSize);
                }
                EventsStream.EndForEachIndex();
            }
        }
    }
}