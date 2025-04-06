
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Trove.EventSystems
{
    public interface IEntityEventsSingleton<E, B> 
        where E : unmanaged, IEventForEntity<B> 
        where B : unmanaged, IBufferElementData 
    {
        public QueueEventsManager<E> QueueEventsManager { get; set; }
        public EntityStreamEventsManager<E, B> StreamEventsManager { get; set; }
    }
    
    public interface IGlobalEventsSingleton<E> where E : unmanaged
    {
        public QueueEventsManager<E> QueueEventsManager { get; set; }
        public GlobalStreamEventsManager<E> StreamEventsManager { get; set; }
        public NativeList<E> ReadEventsList { get; set; }
    }
    
    public interface IEntityPolymorphicEventsSingleton<E, P> 
        where E : unmanaged, IPolymorphicEventForEntity<P>
        where P : unmanaged, IPolymorphicObject
    {
        public EntityPolymorphicStreamEventsManager<E, P> StreamEventsManager { get; set; }
    }
    
    public interface IGlobalPolymorphicEventsSingleton<E> 
        where E : unmanaged, IPolymorphicObject
    {
        public GlobalPolymorphicStreamEventsManager<E> StreamEventsManager { get; set; }
        public NativeList<byte> ReadEventsList { get; set; }
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

    public unsafe struct GlobalStreamEventsManager<E> where E : unmanaged
    {
        public struct Writer
        {
            private NativeStream.Writer StreamWriter;

            internal Writer(NativeStream stream)
            {
                StreamWriter = stream.AsWriter();
            }

            public void BeginForEachIndex(int index)
            {
                StreamWriter.BeginForEachIndex(index);
            }

            public void EndForEachIndex()
            {
                StreamWriter.EndForEachIndex();
            }

            public void Write(E evnt) 
            {
                StreamWriter.Write(evnt);
            }
        }
        
        private byte* JobIncompatibilityPtr; // This is to prevent having this struct inside a job
        internal NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;
        internal Allocator _allocator;

        public bool IsCreated => _eventStreamsReference.IsCreated;

        public GlobalStreamEventsManager(
            NativeReference<UnsafeList<NativeStream>> eventStreamsReference,
            ref SystemState state)
        {
            JobIncompatibilityPtr = default;
            _eventStreamsReference = eventStreamsReference;
            _allocator = state.WorldUpdateAllocator;
        }

        public Writer CreateWriter(int bufferCount)
        {
            NativeStream newEventsStream = new NativeStream(bufferCount, _allocator);
            _eventStreamsReference.GetUnsafePtr()->AddWithGrowFactor(newEventsStream);
            return new Writer(newEventsStream);
        }

        internal UnsafeList<NativeStream> InternalGetEventStreams()
        {
            return _eventStreamsReference.Value;
        }

        internal JobHandle ScheduleClearWriterCollections(JobHandle dep)
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


    public unsafe struct EntityStreamEventsManager<E, B> 
        where E : unmanaged, IEventForEntity<B> 
        where B : unmanaged, IBufferElementData 
    {
        public struct Writer
        {
            private NativeStream.Writer StreamWriter;

            internal Writer(NativeStream stream)
            {
                StreamWriter = stream.AsWriter();
            }

            public void BeginForEachIndex(int index)
            {
                StreamWriter.BeginForEachIndex(index);
            }

            public void EndForEachIndex()
            {
                StreamWriter.EndForEachIndex();
            }

            public void Write(E evntForEntity) 
            {
                StreamWriter.Write(evntForEntity);
            }
        }
        
        private byte* JobIncompatibilityPtr; // This is to prevent having this struct inside a job
        internal NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;
        internal Allocator _allocator;

        public bool IsCreated => _eventStreamsReference.IsCreated;

        public EntityStreamEventsManager(
            NativeReference<UnsafeList<NativeStream>> eventStreamsReference,
            ref SystemState state)
        {
            JobIncompatibilityPtr = default;
            _eventStreamsReference = eventStreamsReference;
            _allocator = state.WorldUpdateAllocator;
        }

        public Writer CreateWriter(int bufferCount)
        {
            NativeStream newEventsStream = new NativeStream(bufferCount, _allocator);
            _eventStreamsReference.GetUnsafePtr()->AddWithGrowFactor(newEventsStream);
            return new Writer(newEventsStream);
        }

        internal UnsafeList<NativeStream> InternalGetEventStreams()
        {
            return _eventStreamsReference.Value;
        }

        internal JobHandle ScheduleClearWriterCollections(JobHandle dep)
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

    public unsafe struct GlobalPolymorphicStreamEventsManager<E> 
        where E : unmanaged, IPolymorphicObject
    {
        public struct Writer
        {
            private NativeStream.Writer StreamWriter;

            internal Writer(NativeStream stream)
            {
                StreamWriter = stream.AsWriter();
            }

            public void BeginForEachIndex(int index)
            {
                StreamWriter.BeginForEachIndex(index);
            }

            public void EndForEachIndex()
            {
                StreamWriter.EndForEachIndex();
            }

            public void Write(E evnt) 
            {
                PolymorphicObjectUtilities.AddObject(evnt, ref StreamWriter, out _);
            }
        }
        
        private byte* JobIncompatibilityPtr; // This is to prevent having this struct inside a job
        internal NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;
        internal Allocator _allocator;

        public bool IsCreated => _eventStreamsReference.IsCreated;

        public GlobalPolymorphicStreamEventsManager(
            NativeReference<UnsafeList<NativeStream>> eventStreamsReference,
            ref SystemState state)
        {
            JobIncompatibilityPtr = default;
            _eventStreamsReference = eventStreamsReference;
            _allocator = state.WorldUpdateAllocator;
        }

        public Writer CreateWriter(int bufferCount)
        {
            NativeStream newEventsStream = new NativeStream(bufferCount, _allocator);
            _eventStreamsReference.GetUnsafePtr()->AddWithGrowFactor(newEventsStream);
            return new Writer(newEventsStream);
        }

        internal UnsafeList<NativeStream> InternalGetEventStreams()
        {
            return _eventStreamsReference.Value;
        }

        internal JobHandle ScheduleClearWriterCollections(JobHandle dep)
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

    public unsafe struct EntityPolymorphicStreamEventsManager<E, P> 
        where E : unmanaged, IPolymorphicEventForEntity<P>
        where P : unmanaged, IPolymorphicObject
    {
        public struct Writer
        {
            private NativeStream.Writer StreamWriter;

            internal Writer(NativeStream stream)
            {
                StreamWriter = stream.AsWriter();
            }

            public void BeginForEachIndex(int index)
            {
                StreamWriter.BeginForEachIndex(index);
            }

            public void EndForEachIndex()
            {
                StreamWriter.EndForEachIndex();
            }

            public void Write(E evntForEntity) 
            {
                StreamWriter.Write(evntForEntity.AffectedEntity);
                PolymorphicObjectUtilities.AddObject(evntForEntity.Event, ref StreamWriter, out _);
            }
        }
        
        private byte* JobIncompatibilityPtr; // This is to prevent having this struct inside a job
        internal NativeReference<UnsafeList<NativeStream>> _eventStreamsReference;
        internal Allocator _allocator;

        public bool IsCreated => _eventStreamsReference.IsCreated;

        public EntityPolymorphicStreamEventsManager(
            NativeReference<UnsafeList<NativeStream>> eventStreamsReference,
            ref SystemState state)
        {
            JobIncompatibilityPtr = default;
            _eventStreamsReference = eventStreamsReference;
            _allocator = state.WorldUpdateAllocator;
        }

        public Writer CreateWriter(int bufferCount)
        {
            NativeStream newEventsStream = new NativeStream(bufferCount, _allocator);
            _eventStreamsReference.GetUnsafePtr()->AddWithGrowFactor(newEventsStream);
            return new Writer(newEventsStream);
        }

        internal UnsafeList<NativeStream> InternalGetEventStreams()
        {
            return _eventStreamsReference.Value;
        }

        internal JobHandle ScheduleClearWriterCollections(JobHandle dep)
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