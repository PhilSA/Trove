
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Trove;

namespace Trove.EventSystems
{
    public unsafe struct EntityPolymorphicEventSubSystem<S, B, H, E, P>
        where S : unmanaged, IComponentData, IEntityPolymorphicEventsSingleton // The events singleton
        where B : unmanaged, IBufferElementData // The event buffer element
        where H : unmanaged, IComponentData, IEnableableComponent // The enableable component that signals presence of events on buffer entities
        where E : unmanaged, IPolymorphicEventForEntity<P> // The event struct
        where P : unmanaged, IPolymorphicObject
    {
        private EntityQuery _singletonRWQuery;
        private EntityQuery _query;
        private BufferTypeHandle<B> _eventBufferTypeHandle;
        private ComponentTypeHandle<H> _hasEventsTypeHandle;
        private BufferLookup<B> _eventBufferLookup;
        private ComponentLookup<H> _hasEventsLookup;
        private NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;

        public EntityPolymorphicEventSubSystem(ref SystemState state, int initialStreamsCapacity)
        {
            if (UnsafeUtility.SizeOf<B>() != 1)
            {
                throw new System.Exception("EntityPolymorphicEventSubSystem generic parameter \"B\" must have a size of exactly 1 byte.");
            }

            state.RequireForUpdate<S>();
            _singletonRWQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<S>().Build(ref state);
            _query = new EntityQueryBuilder(Allocator.Temp).WithAll<B, H>().Build(ref state);

            _eventStreamsReference = new NativeReference<UnsafeList<NativeStream>>(
                new UnsafeList<NativeStream>(initialStreamsCapacity, Allocator.Persistent),
                    Allocator.Persistent);

            // Create the event singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            S singleton = default(S);
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

            UnsafeList<NativeStream> eventStreams = singletonRW.ValueRW.StreamEventsManager.InternalGetEventStreams();
            for (int i = 0; i < eventStreams.Length; i++)
            {
                state.Dependency = new EventTransferPolymorphicStreamToBufferJob<B, H, E, P>
                {
                    EventsStream = eventStreams[i].AsReader(),
                    EventBufferLookup = _eventBufferLookup,
                    HasEventsLookup = _hasEventsLookup,
                }.Schedule(state.Dependency);
            }

            state.Dependency = singletonRW.ValueRW.StreamEventsManager.ScheduleClearWriterCollections(state.Dependency);
        }
    }

    [BurstCompile]
    public unsafe struct EventTransferPolymorphicStreamToBufferJob<B, H, E, P> : IJob
        where B : unmanaged, IBufferElementData // The event buffer element
        where H : unmanaged, IComponentData, IEnableableComponent // The enableable component that signals presence of events on buffer entities
        where E : unmanaged, IPolymorphicEventForEntity<P> // The event struct
        where P : unmanaged, IPolymorphicObject
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
                    // Read from stream
                    E e = EventsStream.Read<E>();
                    //PolymorphicObjectUtilities.GetNextObject(ref EventsStream, out P polymorphicObject, out int readSize);
                    
                    if (EventBufferLookup.TryGetBuffer(e.AffectedEntity, out DynamicBuffer<B> eventBuffer))
                    {
                        DynamicBuffer<byte> bytesBuffer = eventBuffer.Reinterpret<byte>();
                        PolymorphicObjectUtilities.AddObject(e.Event, ref bytesBuffer,
                            out int addedByteIndex, out int writeSize);

                        // Mark as having events
                        HasEventsLookup.SetComponentEnabled(e.AffectedEntity, true);
                    }
                }

                EventsStream.EndForEachIndex();
            }
        }
    }
}