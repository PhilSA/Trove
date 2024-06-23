using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;
using UnityEngine.UIElements;

namespace Trove.ObjectHandles
{
    public struct IndexRangeElement
    {
        public int StartInclusive;
        public int EndExclusive;
    }

    public struct ObjectHandle<T> : IEquatable<ObjectHandle>, IEquatable<ObjectHandle<T>> where T : unmanaged
    {
        internal readonly int Index;
        internal readonly int Version;

        internal ObjectHandle(ObjectHandle handle)
        {
            Index = handle.Index;
            Version = handle.Version;
        }

        public static implicit operator ObjectHandle(ObjectHandle<T> o) => new ObjectHandle(o.Index, o.Version);

        public bool Equals(ObjectHandle other)
        {
            return Index == other.Index && Version == other.Version;
        }

        public bool Equals(ObjectHandle<T> other)
        {
            return Index == other.Index && Version == other.Version;
        }

        public static bool operator ==(ObjectHandle<T> x, ObjectHandle<T> y)
        {
            return x.Index == y.Index && x.Version == y.Version;
        }
        public static bool operator !=(ObjectHandle<T> x, ObjectHandle<T> y)
        {
            return x.Index != y.Index || x.Version != y.Version;
        }

        public static bool operator ==(ObjectHandle<T> x, ObjectHandle y)
        {
            return x.Index == y.Index && x.Version == y.Version;
        }
        public static bool operator !=(ObjectHandle<T> x, ObjectHandle y)
        {
            return x.Index != y.Index || x.Version != y.Version;
        }
    }

    public struct ObjectData<T> where T : unmanaged
    {
        public int Version;
        public T Value;
    }

    public struct ObjectHandle : IEquatable<ObjectHandle>
    {
        internal readonly int Index;
        internal readonly int Version;

        internal ObjectHandle(int index, int version)
        {
            Index = index;
            Version = version;
        }

        public bool Equals(ObjectHandle other)
        {
            return Index == other.Index && Version == other.Version;
        }

        public static bool operator ==(ObjectHandle x, ObjectHandle y)
        {
            return x.Index == y.Index && x.Version == y.Version;
        }
        public static bool operator !=(ObjectHandle x, ObjectHandle y)
        {
            return x.Index != y.Index || x.Version != y.Version;
        }
    }

    public struct VirtualObjectMetadata
    {
        public int ByteIndex;
        public int Version;
        public int Size;
    }

    public struct VirtualObjectHandle<T> : IEquatable<VirtualObjectHandle>, IEquatable<VirtualObjectHandle<T>> where T : unmanaged
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

        public bool Equals(VirtualObjectHandle other)
        {
            return MetadataByteIndex == other.MetadataByteIndex && Version == other.Version;
        }

        public bool Equals(VirtualObjectHandle<T> other)
        {
            return MetadataByteIndex == other.MetadataByteIndex && Version == other.Version;
        }

        public static bool operator ==(VirtualObjectHandle<T> x, VirtualObjectHandle<T> y)
        {
            return x.MetadataByteIndex == y.MetadataByteIndex && x.Version == y.Version;
        }
        public static bool operator !=(VirtualObjectHandle<T> x, VirtualObjectHandle<T> y)
        {
            return x.MetadataByteIndex != y.MetadataByteIndex || x.Version != y.Version;
        }

        public static bool operator ==(VirtualObjectHandle<T> x, VirtualObjectHandle y)
        {
            return x.MetadataByteIndex == y.MetadataByteIndex && x.Version == y.Version;
        }
        public static bool operator !=(VirtualObjectHandle<T> x, VirtualObjectHandle y)
        {
            return x.MetadataByteIndex != y.MetadataByteIndex || x.Version != y.Version;
        }
    }

    [System.Serializable]
    public unsafe struct VirtualObjectHandle : IEquatable<VirtualObjectHandle>
    {
        internal readonly int MetadataByteIndex;
        internal readonly int Version;

        internal VirtualObjectHandle(int index, int version)
        {
            MetadataByteIndex = index;
            Version = version;
        }

        public bool Equals(VirtualObjectHandle other)
        {
            return MetadataByteIndex == other.MetadataByteIndex && Version == other.Version;
        }

        public static bool operator ==(VirtualObjectHandle x, VirtualObjectHandle y)
        {
            return x.MetadataByteIndex == y.MetadataByteIndex && x.Version == y.Version;
        }
        public static bool operator !=(VirtualObjectHandle x, VirtualObjectHandle y)
        {
            return x.MetadataByteIndex != y.MetadataByteIndex || x.Version != y.Version;
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
        public static void ConsumeFreeRange(ref IndexRangeElement freeIndexRange, int objectSize,
            out bool isFullyConsumed, out int consumedStartIndex)
        {
            // Consume memory out of the found range
            consumedStartIndex = freeIndexRange.StartInclusive;
            freeIndexRange.StartInclusive += objectSize;

            Assert.IsTrue(freeIndexRange.StartInclusive <= freeIndexRange.EndExclusive);

            if (freeIndexRange.StartInclusive == freeIndexRange.EndExclusive)
            {
                isFullyConsumed = true;
            }
            isFullyConsumed = false;
        }
    }
}