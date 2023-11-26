using Unity.Entities;
using Unity.Mathematics;
using System;
using Unity.Logging;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using System.Runtime.CompilerServices;

namespace Trove.PolymorphicElements
{
    [NativeContainer]
    public unsafe struct EventWriterSingle : IByteList
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList<byte>* List;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal static readonly SharedStatic<int> _staticSafetyId = SharedStatic<int>.GetOrCreate<EventWriterSingle>();
#endif

        public EventWriterSingle(AllocatorManager.AllocatorHandle allocatorHandle, UnsafeList<byte>* list)
        {
            List = list;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocatorHandle);
            CollectionHelper.SetStaticSafetyId<EventWriterSingle>(ref m_Safety, ref _staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        public unsafe int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return List->Length;
            }
        }

        public byte* Ptr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                return List->Ptr;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newLength, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            List->Resize(newLength, options);
        }
    }

    public unsafe struct EventWriterParallel : IStreamWriter
    {
        internal UnsafeStream.Writer StreamWriter;

        public unsafe byte* Allocate(int size)
        {
            return StreamWriter.Allocate(size);
        }

        public void Write<T>(T t) where T : unmanaged
        {
            StreamWriter.Write(t);
        }

        public void BeginForEachIndex(int index)
        {
            StreamWriter.BeginForEachIndex(index);
        }

        public void EndForEachIndex()
        {
            StreamWriter.EndForEachIndex();
        }
    }

    public unsafe struct EventBuffersManager
    {
        [NativeDisableUnsafePtrRestriction]
        private EventBuffersManagerData* Data;

        public EventBuffersManager(ref EventBuffersManagerData data)
        {
            Data = (EventBuffersManagerData*)UnsafeUtility.AddressOf(ref data);
        }

        public EventWriterSingle CreateEventWriterSingle(int initialCapacity, ref SystemState state)
        {
            if (!Data->EventListsClearingDepWasCompleted)
            {
                Data->EventListsClearingDep.Complete();
                Data->EventListsClearingDepWasCompleted = true;
            }

            UnsafeList<byte> newList = new UnsafeList<byte>(initialCapacity, Data->Allocator);
            Data->EventLists.Add(newList);

            EventWriterSingle eventWriter = new EventWriterSingle(Data->Allocator,
                (Data->EventLists.GetUnsafePtr() + (long)(Data->EventLists.Length - 1)));

            Data->EventWriterHandles.Add(state.SystemHandle);

            return eventWriter;
        }

        public EventWriterParallel CreateEventWriterParallel(int bufferCount, ref SystemState state)
        {
            if (!Data->EventListsClearingDepWasCompleted)
            {
                Data->EventListsClearingDep.Complete();
                Data->EventListsClearingDepWasCompleted = true;
            }

            UnsafeStream newStream = new UnsafeStream(bufferCount, Data->Allocator);
            Data->EventStreams.Add(newStream);

            EventWriterParallel eventWriter = new EventWriterParallel
            {
                StreamWriter = newStream.AsWriter(),
            };

            Data->EventWriterHandles.Add(state.SystemHandle);

            return eventWriter;
        }
    }

    public unsafe struct EventBuffersManagerData
    {
        [ReadOnly]
        public NativeList<UnsafeList<byte>> EventLists;
        [ReadOnly]
        public NativeList<UnsafeStream> EventStreams;
        [ReadOnly]
        internal NativeList<SystemHandle> EventWriterHandles;
        internal bool EventListsClearingDepWasCompleted;
        internal JobHandle EventListsClearingDep;
        internal Allocator Allocator;

        public EventBuffersManagerData(ref SystemState state)
        {
            EventLists = new NativeList<UnsafeList<byte>>(Allocator.Persistent);
            EventStreams = new NativeList<UnsafeStream>(Allocator.Persistent);
            EventWriterHandles = new NativeList<SystemHandle>(Allocator.Persistent);
            EventListsClearingDepWasCompleted = false;
            EventListsClearingDep = default;
            Allocator = state.WorldUpdateAllocator;
        }

        public JobHandle DisposeAll(JobHandle dep = default)
        {
            JobHandle returnDep = dep;
            for (int i = 0; i < EventLists.Length; i++)
            {
                returnDep = EventLists[i].Dispose(dep);
            }
            for (int i = 0; i < EventStreams.Length; i++)
            {
                returnDep = EventStreams[i].Dispose(dep);
            }
            if (EventLists.IsCreated)
            {
                returnDep = EventLists.Dispose(dep);
            }
            if (EventStreams.IsCreated)
            {
                returnDep = EventStreams.Dispose(dep);
            }
            if (EventWriterHandles.IsCreated)
            {
                returnDep = EventWriterHandles.Dispose(dep);
            }
            return returnDep;
        }

        public void BeforeEventsProcessed(ref SystemState state)
        {
            // Add output dep of all writers
            for (int i = 0; i < EventWriterHandles.Length; i++)
            {
                ref SystemState writerState = ref state.WorldUnmanaged.ResolveSystemStateRef(EventWriterHandles[i]);
                state.Dependency = JobHandle.CombineDependencies(state.Dependency, writerState.Dependency);
            }
        }

        public void AfterEventsProcessed(ref SystemState state)
        {
            JobHandle returnDep = state.Dependency;
            for (int i = 0; i < EventLists.Length; i++)
            {
                returnDep = EventLists[i].Dispose(returnDep);
            }
            for (int i = 0; i < EventStreams.Length; i++)
            {
                returnDep = EventStreams[i].Dispose(returnDep);
            }
            ClearEventListsJob clearJob = new ClearEventListsJob
            {
                EventLists = EventLists,
                EventStreams = EventStreams,
                EventWriterHandles = EventWriterHandles,
            };
            returnDep = clearJob.Schedule(returnDep);

            EventListsClearingDep = returnDep;
            EventListsClearingDepWasCompleted = false;

            state.Dependency = returnDep;
        }

        [BurstCompile]
        public struct ClearEventListsJob : IJob
        {
            public NativeList<UnsafeList<byte>> EventLists;
            public NativeList<UnsafeStream> EventStreams;
            public NativeList<SystemHandle> EventWriterHandles;

            public void Execute()
            {
                EventLists.Clear();
                EventStreams.Clear();
                EventWriterHandles.Clear();
            }
        }
    }
}