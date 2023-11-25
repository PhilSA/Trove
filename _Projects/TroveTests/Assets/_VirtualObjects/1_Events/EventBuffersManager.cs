using Unity.Entities;
using Unity.Mathematics;
using System;
using Unity.Logging;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Trove.PolymorphicElements;
using Unity.Burst;
using Unity.Jobs;

public unsafe struct EventWriterSingle : IByteList
{
    [NativeDisableUnsafePtrRestriction]
    internal UnsafeList<byte>* List;

    public int Length => List->Length;

    public byte* Ptr => List->Ptr;

    public void Resize(int newLength, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
    {
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
        Data->EventListsClearingDep.Complete();

        UnsafeList<byte> newList = new UnsafeList<byte>(initialCapacity, Allocator.Persistent);
        Data->EventLists.Add(newList); // todo: allocator

        EventWriterSingle eventWriter = new EventWriterSingle
        {
            List = (Data->EventLists.GetUnsafePtr() + (long)(Data->EventLists.Length - 1)),
        };

        return eventWriter;
    }

    public EventWriterParallel CreateEventWriterParallel(int bufferCount, ref SystemState state)
    {
        Data->EventListsClearingDep.Complete();

        UnsafeStream newStream = new UnsafeStream(bufferCount, Allocator.Persistent);
        Data->EventStreams.Add(newStream); // todo: allocator

        EventWriterParallel eventWriter = new EventWriterParallel
        {
            StreamWriter = newStream.AsWriter(),
        };

        return eventWriter;
    }
}

public unsafe struct EventBuffersManagerData
{
    [ReadOnly]
    internal NativeList<UnsafeList<byte>> EventLists;
    [ReadOnly]
    internal NativeList<UnsafeStream> EventStreams;
    internal JobHandle EventListsClearingDep;

    public EventBuffersManagerData(ref SystemState state)
    {
        EventLists = new NativeList<UnsafeList<byte>>(Allocator.Persistent);
        EventStreams = new NativeList<UnsafeStream>(Allocator.Persistent);
        EventListsClearingDep = default;
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
        if (EventStreams.IsCreated)
        {
            returnDep = EventStreams.Dispose(dep);
        }
        return returnDep;
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
        };
        returnDep = clearJob.Schedule(returnDep);

        EventListsClearingDep = returnDep;
        state.Dependency = returnDep;
    }

    [BurstCompile]
    public struct ClearEventListsJob : IJob
    {
        public NativeList<UnsafeList<byte>> EventLists;
        public NativeList<UnsafeStream> EventStreams;

        public void Execute()
        {
            EventLists.Clear();
            EventStreams.Clear();
        }
    }
}