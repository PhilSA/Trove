using Unity.Entities;
using Unity.Mathematics;
using System;
using Unity.Logging;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Trove.PolymorphicElements;

public struct EventBuffersManager : IComponentData
{
    public NativeList<UnsafeList<byte>> ListBuffers;
    public NativeList<UnsafeStream> StreamBuffers;

    public unsafe struct Writer : IByteList
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList<byte>* EventsList;

        public int Length => EventsList->Length;

        public unsafe byte* Ptr => EventsList->Ptr;

        public void Resize(int newLength)
        {
            EventsList->Resize(newLength);
        }
    }

    public unsafe struct ParallelWriter : IStreamWriter
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeStream.Writer* EventsStreamWriter;

        public unsafe byte* Allocate(int size)
        {
            return EventsStreamWriter->Allocate(size);
        }

        public void Write<T>(T t) where T : unmanaged
        {
            EventsStreamWriter->Write(t);
        }
    }
}