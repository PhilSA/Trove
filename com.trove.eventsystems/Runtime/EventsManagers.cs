
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Trove.EventSystems
{
    public interface IEntityEventsSingleton<E> where E : unmanaged
    {
        public QueueEventsManager<E> QueueEventsManager { get; set; }
        public StreamEventsManager StreamEventsManager { get; set; }
    }
    
    public interface IGlobalEventsSingleton<E> where E : unmanaged
    {
        public QueueEventsManager<E> QueueEventsManager { get; set; }
        public StreamEventsManager StreamEventsManager { get; set; }
        public NativeList<E> EventsList { get; set; }
    }
    
    public interface IEntityPolymorphicEventsSingleton
    {
        public StreamEventsManager StreamEventsManager { get; set; }
    }
    
    public interface IGlobalPolymorphicEventsSingleton
    {
        public StreamEventsManager StreamEventsManager { get; set; }
        public NativeList<byte> EventsList { get; set; }
    }
    
    public unsafe struct QueueEventsManager<E> where E : unmanaged
    {
        private byte* JobIncompatibilityPtr; // This is to prevent having this struct inside a job
        internal NativeReference<UnsafeList<NativeQueue<E>>> _eventQueuesReference;
        internal Allocator _allocator;

        public bool IsCreated => _eventQueuesReference.IsCreated;

        public QueueEventsManager(
            NativeReference<UnsafeList<NativeQueue<E>>> eventQueuesReference,
            ref SystemState state)
        {
            JobIncompatibilityPtr = default;
            _eventQueuesReference = eventQueuesReference;
            _allocator = state.WorldUpdateAllocator;
        }

        public NativeQueue<E> CreateEventQueue()
        {
            NativeQueue<E> newEventsQueue = new NativeQueue<E>(_allocator);
            _eventQueuesReference.GetUnsafePtr()->Add(newEventsQueue);
            return newEventsQueue;
        }

        public UnsafeList<NativeQueue<E>> InternalGetEventQueues()
        {
            return _eventQueuesReference.Value;
        }

        public JobHandle ScheduleClearWriterCollections(JobHandle dep)
        {
            JobHandle newDep = dep;

            // Dispose queues
            {
                UnsafeList<NativeQueue<E>>* queuesPtr = _eventQueuesReference.GetUnsafePtr();
                for (int i = 0; i < queuesPtr->Length; i++)
                {
                    if (queuesPtr->IsCreated)
                    {
                        newDep = JobHandle.CombineDependencies(newDep, queuesPtr->Ptr[i].Dispose(dep));
                    }
                }

                queuesPtr->Clear();
            }

            return newDep;
        }
    }

    public unsafe struct StreamEventsManager
    {
        private byte* JobIncompatibilityPtr; // This is to prevent having this struct inside a job
        internal NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;
        internal Allocator _allocator;

        public bool IsCreated => _eventStreamsReference.IsCreated;

        public StreamEventsManager(
            NativeReference<UnsafeList<NativeStream>> eventStreamsReference,
            ref SystemState state)
        {
            JobIncompatibilityPtr = default;
            _eventStreamsReference = eventStreamsReference;
            _allocator = state.WorldUpdateAllocator;
        }

        public NativeStream CreateEventStream(int bufferCount)
        {
            // TODO: unsafelist grow factors

            NativeStream newEventsStream = new NativeStream(bufferCount, _allocator);
            _eventStreamsReference.GetUnsafePtr()->Add(newEventsStream);
            return newEventsStream;
        }

        public UnsafeList<NativeStream> InternalGetEventStreams()
        {
            return _eventStreamsReference.Value;
        }

        public JobHandle ScheduleClearWriterCollections(JobHandle dep)
        {
            JobHandle newDep = dep;

            // Dispose streams
            {
                UnsafeList<NativeStream>* streamsPtr = _eventStreamsReference.GetUnsafePtr();
                for (int i = 0; i < streamsPtr->Length; i++)
                {
                    if (streamsPtr->IsCreated)
                    {
                        newDep = JobHandle.CombineDependencies(newDep, streamsPtr->Ptr[i].Dispose(dep));
                    }
                }
                streamsPtr->Clear();
            }

            return newDep;
        }
    }
}