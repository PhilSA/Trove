using Unity.Entities;
using Unity.Mathematics;
using System;
using Unity.Logging;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Trove.PolymorphicElements;
using Unity.Burst;
using Unity.Jobs;

public unsafe struct EventWriter : IStreamWriter
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

    public UnsafeStream.Writer CreateEventWriter(int bufferCount, ref SystemState state)
    {
        Data->EventsFinalizerDep.Complete();
        var newStream = new UnsafeStream(bufferCount, Allocator.Persistent);
        Data->EventStreams.Add(newStream); // todo: allocator
        //return new EventWriter
        //{
        //    StreamWriter = Data->EventStreams[Data->EventStreams.Length - 1].AsWriter(),
        //};
        //return Data->EventStreams[Data->EventStreams.Length - 1].AsWriter();
        return newStream.AsWriter();
    }
}

public unsafe struct EventBuffersManagerData
{
    internal NativeList<UnsafeStream> EventStreams;
    internal JobHandle EventsFinalizerDep;

    public EventBuffersManagerData(ref SystemState state)
    {
        EventStreams = new NativeList<UnsafeStream>(Allocator.Persistent);
        EventsFinalizerDep = default;
    }

    public JobHandle DisposeAll(JobHandle dep = default)
    {
        JobHandle returnDep = dep;
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

    public JobHandle AfterEventsProcessed(ref SystemState state, JobHandle dep)
    {
        state.Dependency.Complete();
        JobHandle returnDep = dep;
        for (int i = 0; i < EventStreams.Length; i++)
        {
            returnDep = EventStreams[i].Dispose(returnDep);
        }
        ClearListJob clearJob = new ClearListJob
        {
            List = EventStreams,
        };
        returnDep = clearJob.Schedule(returnDep);

        EventsFinalizerDep = returnDep;

        return returnDep;
    }

    [BurstCompile]
    public struct ClearListJob : IJob
    {
        public NativeList<UnsafeStream> List;

        public void Execute()
        {
            List.Clear();
        }
    }
}