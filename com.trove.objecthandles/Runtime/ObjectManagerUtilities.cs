using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using static Trove.ObjectHandles.VirtualObjectManager;

namespace Trove.ObjectHandles
{
    public struct IndexRangeElement
    {
        public int StartInclusive;
        public int EndExclusive;
    }

    public struct ObjectHandle<T> where T : unmanaged
    {
        internal readonly int Index;
        internal readonly int Version;

        internal ObjectHandle(ObjectHandle handle)
        {
            Index = handle.Index;
            Version = handle.Version;
        }

        public static implicit operator ObjectHandle(ObjectHandle<T> o) => new ObjectHandle(o.Index, o.Version);
    }

    public struct ObjectData<T> where T : unmanaged
    {
        public int Version;
        public T Value;
    }

    public struct ObjectHandle
    {
        internal readonly int Index;
        internal readonly int Version;

        internal ObjectHandle(int index, int version)
        {
            Index = index;
            Version = version;
        }
    }

    internal struct VirtualObjectMetadata
    {
        public int ByteIndex;
        public int Version;
        public int Size;
    }

    public struct VirtualObjectHandle<T> where T : unmanaged
    {
        internal readonly int MetadataByteIndex;
        internal readonly int Version;

        internal VirtualObjectHandle(int index, int version)
        {
            MetadataByteIndex = index;
            Version = version;
        }

        internal VirtualObjectHandle(VirtualObjectHandle handle)
        {
            MetadataByteIndex = handle.MetadataByteIndex;
            Version = handle.Version;
        }

        public static implicit operator VirtualObjectHandle(VirtualObjectHandle<T> o) => new VirtualObjectHandle(o.MetadataByteIndex, o.Version);
    }

    public unsafe struct VirtualObjectHandle
    {
        internal readonly int MetadataByteIndex;
        internal readonly int Version;

        internal VirtualObjectHandle(int index, int version)
        {
            MetadataByteIndex = index;
            Version = version;
        }
    }

    public static class ObjectManagerUtilities
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RangesOverlap(int aStartInclusive, int aEndExclusive, int bStartInclusive, int bEndExclusive)
        {
            return aStartInclusive < bEndExclusive && bStartInclusive < aEndExclusive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConsumeFreeRange(IndexRangeElement freeIndexRange, int objectIndexesSize,
            out bool isFullyConsumed, out int consumedStartIndex)
        {
            // Consume memory out of the found range
            consumedStartIndex = freeIndexRange.StartInclusive;
            freeIndexRange.StartInclusive += objectIndexesSize;

            Assert.IsTrue(freeIndexRange.StartInclusive <= freeIndexRange.EndExclusive);

            if (freeIndexRange.StartInclusive == freeIndexRange.EndExclusive)
            {
                isFullyConsumed = true;
            }
            isFullyConsumed = false;
        }
    }
}