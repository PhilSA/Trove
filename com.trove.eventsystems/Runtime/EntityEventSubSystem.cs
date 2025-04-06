
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;


namespace Trove.EventSystems
{
    public unsafe struct EntityEventSubSystem<S, E, B, H>
        where S : unmanaged, IComponentData, IEntityEventsSingleton<E> // The events singleton
        where E : unmanaged, IEventForEntity<B> // The event struct
        where B : unmanaged, IBufferElementData // The event buffer element
        where H : unmanaged, IComponentData, IEnableableComponent // The enableable component that signals presence of events on buffer entities
    {
        private EntityQuery _singletonRWQuery;
        private EntityQuery _query;
        private BufferTypeHandle<B> _eventBufferTypeHandle;
        private ComponentTypeHandle<H> _hasEventsTypeHandle;
        private BufferLookup<B> _eventBufferLookup;
        private ComponentLookup<H> _hasEventsLookup;
        private NativeReference<UnsafeList<NativeQueue<E>>> _eventQueuesReference;
        private NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;

        public EntityEventSubSystem(ref SystemState state, int initialQueuesCapacity, int initialStreamsCapacity)
        {
            state.RequireForUpdate<S>();
            _singletonRWQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<S>().Build(ref state);
            _query = new EntityQueryBuilder(Allocator.Temp).WithAll<B, H>().Build(ref state);

            _eventQueuesReference = new NativeReference<UnsafeList<NativeQueue<E>>>(
                new UnsafeList<NativeQueue<E>>(initialQueuesCapacity, Allocator.Persistent),
                Allocator.Persistent);

            _eventStreamsReference = new NativeReference<UnsafeList<NativeStream>>(
                new UnsafeList<NativeStream>(initialStreamsCapacity, Allocator.Persistent),
                Allocator.Persistent);

            // Create the event singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            S singleton = default(S);
            singleton.QueueEventsManager = new QueueEventsManager<E>(
                _eventQueuesReference,
                ref state);
            singleton.StreamEventsManager = new StreamEventsManager(
                _eventStreamsReference,
                ref state);
            state.EntityManager.AddComponentData(singletonEntity, singleton);
            
            _eventBufferTypeHandle = state.GetBufferTypeHandle<B>(false);
            _hasEventsTypeHandle = state.GetComponentTypeHandle<H>(false);
            _eventBufferLookup = state.GetBufferLookup<B>(false);
            _hasEventsLookup = state.GetComponentLookup<H>(false);
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
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<S> singletonRW = _singletonRWQuery.GetSingletonRW<S>();
        
            _eventBufferTypeHandle.Update(ref state);
            _hasEventsTypeHandle.Update(ref state);
            _eventBufferLookup.Update(ref state);
            _hasEventsLookup.Update(ref state);

            state.Dependency = new EventClearBuffersJob<B, H>
            {
                EventBufferType = _eventBufferTypeHandle,
                HasEventType = _hasEventsTypeHandle,
            }.ScheduleParallel(_query, state.Dependency);

            UnsafeList<NativeQueue<E>> eventQueues = singletonRW.ValueRW.QueueEventsManager.InternalGetEventQueues();
            for (int i = 0; i < eventQueues.Length; i++)
            {
                state.Dependency = new EventTransferQueueToBufferJob<E, B, H>
                {
                    EventsQueue = eventQueues[i],
                    EventBufferLookup = _eventBufferLookup,
                    HasEventsLookup = _hasEventsLookup,
                }.Schedule(state.Dependency);
            }

            UnsafeList<NativeStream> eventStreams = singletonRW.ValueRW.StreamEventsManager.InternalGetEventStreams();
            for (int i = 0; i < eventStreams.Length; i++)
            {
                state.Dependency = new EventTransferStreamToBufferJob<E, B, H>
                {
                    EventsStream = eventStreams[i].AsReader(),
                    EventBufferLookup = _eventBufferLookup,
                    HasEventsLookup = _hasEventsLookup,
                }.Schedule(state.Dependency);
            }


            state.Dependency = singletonRW.ValueRW.QueueEventsManager.ScheduleClearWriterCollections(state.Dependency);
            state.Dependency = singletonRW.ValueRW.StreamEventsManager.ScheduleClearWriterCollections(state.Dependency);
        }
    }

    [BurstCompile]
    public struct EventTransferQueueToBufferJob<E, B, H> : IJob
        where E : unmanaged, IEventForEntity<B> // The event struct
        where B : unmanaged, IBufferElementData // The event buffer element
        where H : unmanaged, IComponentData, IEnableableComponent // The enableable component that signals presence of events on buffer entities
    {
        public NativeQueue<E> EventsQueue;
        public BufferLookup<B> EventBufferLookup;
        public ComponentLookup<H> HasEventsLookup;

        public void Execute()
        {
            while (EventsQueue.TryDequeue(out E e))
            {
                if (EventBufferLookup.TryGetBuffer(e.AffectedEntity, out DynamicBuffer<B> eventBuffer))
                {
                    eventBuffer.Add(e.Event);
                    HasEventsLookup.SetComponentEnabled(e.AffectedEntity, true);
                }
            }
        }
    }

    [BurstCompile]
    public struct EventTransferStreamToBufferJob<E, B, H> : IJob
        where E : unmanaged, IEventForEntity<B> // The event struct
        where B : unmanaged, IBufferElementData // The event buffer element
        where H : unmanaged, IComponentData, IEnableableComponent // The enableable component that signals presence of events on buffer entities
    {
        public NativeStream.Reader EventsStream;
        public BufferLookup<B> EventBufferLookup;
        public ComponentLookup<H> HasEventsLookup;

        public void Execute()
        {
            for (int i = 0; i < EventsStream.ForEachCount; i++)
            {
                EventsStream.BeginForEachIndex(i);
                while (EventsStream.RemainingItemCount > 0)
                {
                    E e = EventsStream.Read<E>();
                    if (EventBufferLookup.TryGetBuffer(e.AffectedEntity, out DynamicBuffer<B> eventBuffer))
                    {
                        eventBuffer.Add(e.Event);
                        HasEventsLookup.SetComponentEnabled(e.AffectedEntity, true);
                    }
                }
                EventsStream.EndForEachIndex();
            }
        }
    }
}