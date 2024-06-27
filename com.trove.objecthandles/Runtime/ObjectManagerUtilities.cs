using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;

namespace Trove.ObjectHandles
{
    public struct IndexRangeElement
    {
        public int StartInclusive;
        public int EndExclusive;
    }

    public struct ObjectData<T> where T : unmanaged
    {
        public int Version;
        public T Value;
    }

    public struct ObjectHandle<T> : IEquatable<ObjectHandle<T>>, IComparable<ObjectHandle<T>> where T : unmanaged
    {
        internal readonly int Index;
        internal readonly int Version;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(ObjectHandle<T> other)
        {
            return Index - other.Index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is VirtualObjectHandle<T> h)
            {
                return Equals(h);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ObjectHandle<T> other)
        {
            return this == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 55339;
            hash = hash * 104579 + Index.GetHashCode();
            hash = hash * 104579 + Version.GetHashCode();
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ObjectHandle<T> x, ObjectHandle<T> y)
        {
            return x.Index == y.Index && x.Version == y.Version;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ObjectHandle<T> x, ObjectHandle<T> y)
        {
            return x.Index != y.Index || x.Version != y.Version;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return $"ObjectHandle {{ Index = {Index}, Version = {Version} }}";
        }
    }

    public struct VirtualObjectHandle<T> : IEquatable<VirtualObjectHandle<T>>, IComparable<VirtualObjectHandle<T>> where T : unmanaged
    {
        public readonly int ByteIndex;
        internal readonly ulong Id;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal VirtualObjectHandle(int index, ulong id)
        {
            ByteIndex = index;
            Id = id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(VirtualObjectHandle<T> other)
        {
            return ByteIndex - other.ByteIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is VirtualObjectHandle<T> h)
            {
                return Equals(h);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(VirtualObjectHandle<T> other)
        {
            return this == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 55609;
            hash = hash * 104773 + ByteIndex.GetHashCode();
            hash = hash * 104773 + Id.GetHashCode();
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(VirtualObjectHandle<T> x, VirtualObjectHandle<T> y)
        {
            return x.ByteIndex == y.ByteIndex && x.Id == y.Id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(VirtualObjectHandle<T> x, VirtualObjectHandle<T> y)
        {
            return x.ByteIndex != y.ByteIndex || x.Id != y.Id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return $"VirtualObjectHandle {{ ByteIndex = {ByteIndex}, Id = {Id} }}";
        }
    }

    public struct VirtualObjectData<T> where T : unmanaged
    {
        public VirtualObjectHeader Header;
        public T Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct VirtualObjectHeader
    {
        [FieldOffset(0)]
        private ValidObjectCode ValidObjectCode;
        [FieldOffset(8)]
        internal ulong Id;
        [FieldOffset(16)]
        internal int Size;

        private static readonly ValidObjectCode ValidObjectCodeValue = new ValidObjectCode
        {
            A = 643209875123, // magic
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VirtualObjectHeader(ulong id, int size)
        {
            ValidObjectCode = ValidObjectCodeValue;
            Id = id;
            Size = size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSameObject<T>(VirtualObjectHandle<T> handle)
            where T : unmanaged
        {
            return ValidObjectCode == ValidObjectCodeValue && Id == handle.Id;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ValidObjectCode
    {
        [FieldOffset(0)]
        public ulong A;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return this.Equals(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ValidObjectCode other)
        {
            return A == other.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + A.GetHashCode();
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ValidObjectCode x, ValidObjectCode y)
        {
            return x.A == y.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ValidObjectCode x, ValidObjectCode y)
        {
            return x.A == y.A;
        }
    }

    public unsafe interface IVirtualObjectView
    {
        public void Clear();
        public int GetLength();
        public byte* GetDataPtr();
        public void Resize(int newLength, NativeArrayOptions nativeArrayOptions);
        public void SetCapacity(int newCapacity);
    }

    public unsafe struct DynamicBufferVirtualObjectView<T> : IVirtualObjectView
            where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        internal DynamicBuffer<T>* BufferPtr;
        [NativeDisableUnsafePtrRestriction]
        internal byte* DataPtr;
        internal int Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynamicBufferVirtualObjectView(ref DynamicBuffer<T> buffer)
        {
            Assert.AreEqual(UnsafeUtility.SizeOf<byte>(), UnsafeUtility.SizeOf<T>());
            BufferPtr = (DynamicBuffer<T>*)UnsafeUtility.AddressOf(ref buffer);
            DataPtr = (byte*)buffer.GetUnsafePtr();
            Length = buffer.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            BufferPtr->Clear();
            Length = BufferPtr->Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLength()
        {
            return Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetDataPtr()
        {
            return DataPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newLength, NativeArrayOptions nativeArrayOptions = NativeArrayOptions.ClearMemory)
        {
            BufferPtr->Resize(newLength, nativeArrayOptions);
            Length = BufferPtr->Length;
            DataPtr = (byte*)BufferPtr->GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCapacity(int newCapacity)
        {
            BufferPtr->Capacity = newCapacity;
            Length = BufferPtr->Length;
            DataPtr = (byte*)BufferPtr->GetUnsafePtr();
        }
    }

    public unsafe struct NativeListVirtualObjectView : IVirtualObjectView
    {
        [NativeDisableUnsafePtrRestriction]
        internal NativeList<byte>* ListPtr;
        [NativeDisableUnsafePtrRestriction]
        internal byte* DataPtr;
        internal int Length;
        internal byte _isCreated;

        public bool IsCreated => _isCreated == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeListVirtualObjectView(ref NativeList<byte> list)
        {
            ListPtr = (NativeList<byte>*)UnsafeUtility.AddressOf(ref list);
            DataPtr = list.GetUnsafePtr();
            Length = list.Length;
            _isCreated = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ListPtr->Clear();
            Length = ListPtr->Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLength()
        {
            return Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetDataPtr()
        {
            return DataPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newLength, NativeArrayOptions nativeArrayOptions = NativeArrayOptions.ClearMemory)
        {
            ListPtr->Resize(newLength, nativeArrayOptions);
            Length = ListPtr->Length;
            DataPtr = ListPtr->GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCapacity(int newCapacity)
        {
            ListPtr->SetCapacity(newCapacity);
            Length = ListPtr->Length;
            DataPtr =ListPtr->GetUnsafePtr();
        }
    }

    public unsafe struct UnsafeListVirtualObjectView : IVirtualObjectView
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList<byte>* ListPtr;
        [NativeDisableUnsafePtrRestriction]
        internal byte* DataPtr;
        internal int Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeListVirtualObjectView(ref UnsafeList<byte> list)
        {
            ListPtr = (UnsafeList<byte>*)UnsafeUtility.AddressOf(ref list);
            DataPtr = list.Ptr;
            Length = list.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ListPtr->Clear();
            Length = ListPtr->Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLength()
        {
            return Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetDataPtr()
        {
            return DataPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newLength, NativeArrayOptions nativeArrayOptions = NativeArrayOptions.ClearMemory)
        {
            ListPtr->Resize(newLength, nativeArrayOptions);
            Length = ListPtr->Length;
            DataPtr = ListPtr->Ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCapacity(int newCapacity)
        {
            ListPtr->SetCapacity(newCapacity);
            Length = ListPtr->Length;
            DataPtr = ListPtr->Ptr;
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
        public static bool RangeContains(int startInclusive, int endExclusive, int value)
        {
            return value >= startInclusive && value < endExclusive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IndexIsValid(int index, int collectionLength)
        {
            return index >= 0 && index < collectionLength;
        }
    }
}