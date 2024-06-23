using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Trove;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using Unity.Assertions;

namespace Trove.ObjectHandles
{
    public unsafe struct VirtualArray<T>
        where T : unmanaged
    {
        internal int _length;

        public int Length => _length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetDataSizeBytes()
        {
            return UnsafeUtility.SizeOf<T>() * _length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSizeBytes()
        {
            return UnsafeUtility.SizeOf<VirtualArray<T>>() + GetDataSizeBytes();
        }

        public static VirtualArrayHandle<T> Allocate(
            ref DynamicBuffer<byte> byteBuffer,
            int capacity)
        {
            VirtualArray<T> array = new VirtualArray<T>();
            array._length = 0;

            int objectSize = array.GetSizeBytes();
            VirtualObjectHandle<T> tmpHandle = VirtualObjectManager.AllocateObject<T>(
                ref byteBuffer,
                objectSize,
                out byte* valueDestinationPtr);
            VirtualArrayHandle<T> handle = new VirtualArrayHandle<T>(tmpHandle.MetadataByteIndex, tmpHandle.Version);

            UnsafeUtility.CopyStructureToPtr(ref array, valueDestinationPtr);
            valueDestinationPtr += (long)UnsafeUtility.SizeOf<VirtualArray<T>>();
            UnsafeUtility.MemClear(valueDestinationPtr, array.GetDataSizeBytes());

            return handle;
        }
    }

    /// <summary>
    /// Note: unsafe due to operating on a ptr to the data in the dynamicBuffer of bytes
    /// </summary>
    public unsafe struct UnsafeVirtualArray<T>
        where T : unmanaged
    {
        internal T* _ptr;
        internal int _length;

        public int Length => _length;

        public UnsafeVirtualArray(T* ptr, int length)
        {
            _ptr = ptr;
            _length = length;
        }

        public T this[int i]
        {
            get 
            {
                Assert.IsTrue(i >= 0 && i < _length);
                return _ptr[i]; 
            }
            set
            {
                Assert.IsTrue(i >= 0 && i < _length);
                _ptr[i] = value; 
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetUnsafePtr()
        { 
            return _ptr; 
        }
    }

    public unsafe struct VirtualArrayHandle<T> where T : unmanaged
    {
        internal readonly int MetadataByteIndex;
        internal readonly int Version;
        internal readonly VirtualObjectHandle<VirtualArray<T>> _objectHandle;

        internal VirtualArrayHandle(int index, int version)
        {
            MetadataByteIndex = index;
            Version = version;
            _objectHandle = new VirtualObjectHandle<VirtualArray<T>>(new VirtualObjectHandle(MetadataByteIndex, Version));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLength(ref DynamicBuffer<byte> byteBuffer, out int length)
        {
            if (VirtualObjectManager.TryGetObjectValue(
                ref byteBuffer,
                this._objectHandle,
                out VirtualArray<T> array))
            {
                length = array._length;
                return true;
            }
            length = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetElementAt(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            out T value)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualArray<T>>(
                ref byteBuffer,
                this._objectHandle,
                out byte* arrayPtr))
            {
                T* arrayData = (T*)(arrayPtr + (long)UnsafeUtility.SizeOf<VirtualArray<T>>());
                value = arrayData[index];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because as soon as the array grows and gets reallocated, the ref is no longer valid
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T TryGetRefElementAtUnsafe(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            out bool success)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualArray<T>>(
                ref byteBuffer,
                this._objectHandle,
                out byte* arrayPtr))
            {
                T* arrayData = (T*)(arrayPtr + (long)UnsafeUtility.SizeOf<VirtualArray<T>>());
                T* elementPtr = arrayData + (long)(UnsafeUtility.SizeOf<T>() * index);
                success = true;
                return ref *elementPtr;
            }
            success = false;
            return ref *(T*)byteBuffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetElementAt(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            T value)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualArray<T>>(
                ref byteBuffer,
                this._objectHandle,
                out byte* arrayPtr))
            {
                T* arrayData = (T*)(arrayPtr + (long)UnsafeUtility.SizeOf<VirtualArray<T>>());
                arrayData[index] = value;
                return true;
            }
            return false;
        }

        public bool TryAsUnsafeVirtualArray(
            ref DynamicBuffer<byte> byteBuffer,
            out UnsafeVirtualArray<T> unsafeArray)
        {
            if (VirtualObjectManager.TryGetObjectValue(
                ref byteBuffer,
                this._objectHandle,
                out VirtualArray<T> array))
            {
                byte* dataPtr = (byte*)byteBuffer.GetUnsafePtr() + (long)this.MetadataByteIndex + (long)UnsafeUtility.SizeOf<VirtualArray<T>>();
                unsafeArray = new UnsafeVirtualArray<T>((T*)dataPtr, array.Length);
                return true;
            }
            unsafeArray = default;
            return false;
        }
    }
}