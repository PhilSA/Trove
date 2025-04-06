
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Trove.EventSystems
{
    public unsafe struct GlobalEventSubSystem<S, E>
        where S : unmanaged, IComponentData, IGlobalEventsSingleton<E> // The events singleton
        where E : unmanaged // The event struct
    {
        private EntityQuery _singletonRWQuery;
        private NativeReference<UnsafeList<NativeQueue<E>>> _eventQueuesReference;
        private NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;
        private NativeList<E> _eventList;

        public GlobalEventSubSystem(ref SystemState state, int initialQueuesCapacity, int initialStreamsCapacity, int initialEventsListCapacity)
        {
            state.RequireForUpdate<S>();
            _singletonRWQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<S>().Build(ref state);

            _eventQueuesReference = new NativeReference<UnsafeList<NativeQueue<E>>>(
                new UnsafeList<NativeQueue<E>>(initialQueuesCapacity, Allocator.Persistent),
                Allocator.Persistent);
            _eventList = new NativeList<E>(initialEventsListCapacity, Allocator.Persistent);

            _eventStreamsReference = new NativeReference<UnsafeList<NativeStream>>(
                new UnsafeList<NativeStream>(initialStreamsCapacity, Allocator.Persistent),
                Allocator.Persistent);

            // Create the event singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            S singleton = default(S);
            singleton.QueueEventsManager = new QueueEventsManager<E>(
                _eventQueuesReference,
                ref state);
            singleton.StreamEventsManager = new GlobalStreamEventsManager<E>(
                _eventStreamsReference,
                ref state);
            singleton.ReadEventsList = _eventList;
            state.EntityManager.AddComponentData(singletonEntity, singleton);
        }

        public void OnDestroy(ref SystemState state)
        {
            // Dispose queues
            if (_eventQueuesReference.GetUnsafePtr()->IsCreated)
            {
                UnsafeList<NativeQueue<E>> eventQueues = _eventQueuesReference.Value;
                for (int i = 0; i < eventQueues.Length; i++)
                {
                    if (eventQueues[i].IsCreated)
                    {
                        eventQueues[i].Dispose();
                    }
                }

                _eventQueuesReference.GetUnsafePtr()->Dispose();
            }
            
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

            state.Dependency = new EventClearListJob<E>
            {
                EventList = singletonRW.ValueRW.ReadEventsList,
            }.Schedule(state.Dependency);

            UnsafeList<NativeQueue<E>> eventQueues = singletonRW.ValueRW.QueueEventsManager.InternalGetEventQueues();
            for (int i = 0; i < eventQueues.Length; i++)
            {
                state.Dependency = new EventTransferQueueToListJob<E>
                {
                    EventsQueue = eventQueues[i],
                    EventList = singletonRW.ValueRW.ReadEventsList,
                }.Schedule(state.Dependency);
            }

            UnsafeList<NativeStream> eventStreams = singletonRW.ValueRW.StreamEventsManager.InternalGetEventStreams();
            for (int i = 0; i < eventStreams.Length; i++)
            {
                state.Dependency = new EventTransferStreamToListJob<E>
                {
                    EventsStream = eventStreams[i].AsReader(),
                    EventList = singletonRW.ValueRW.ReadEventsList,
                }.Schedule(state.Dependency);
            }

            state.Dependency = singletonRW.ValueRW.QueueEventsManager.ScheduleClearWriterCollections(state.Dependency);
            state.Dependency = singletonRW.ValueRW.StreamEventsManager.ScheduleClearWriterCollections(state.Dependency);
        }
    }

    [BurstCompile]
    public struct EventTransferQueueToListJob<E> : IJob
        where E : unmanaged // The event struct
    {
        public NativeQueue<E> EventsQueue;
        public NativeList<E> EventList;

        public void Execute()
        {
            while (EventsQueue.TryDequeue(out E e))
            {
                EventList.Add(e);
            }
        }
    }

    [BurstCompile]
    public struct EventTransferStreamToListJob<E> : IJob
        where E : unmanaged // The event struct
    {
        public NativeStream.Reader EventsStream;
        public NativeList<E> EventList;

        public void Execute()
        {
            for (int i = 0; i < EventsStream.ForEachCount; i++)
            {
                EventsStream.BeginForEachIndex(i);
                while (EventsStream.RemainingItemCount > 0)
                {
                    E e = EventsStream.Read<E>();
                    EventList.Add(e);
                }
                EventsStream.EndForEachIndex();
            }
        }
    }
}