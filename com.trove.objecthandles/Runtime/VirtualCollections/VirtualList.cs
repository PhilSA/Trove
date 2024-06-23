using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Trove;
using Unity.Mathematics;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;

namespace Trove.ObjectHandles
{
    public unsafe struct VirtualList<T>
        where T : unmanaged
    {
        internal int _length;
        internal int _capacity;

        public int Length => _length;
        public int Capacity => _capacity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetDataCapacitySizeBytes()
        {
            return UnsafeUtility.SizeOf<T>() * _capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSizeBytes()
        {
            return UnsafeUtility.SizeOf<VirtualList<T>>() + GetDataCapacitySizeBytes();
        }

        public static VirtualListHandle<T> Allocate(
            ref DynamicBuffer<byte> byteBuffer,
            int capacity)
        {
            VirtualList<T> list = new VirtualList<T>();
            list._length = 0;
            list._capacity = capacity;

            int objectSize = list.GetSizeBytes();
            VirtualObjectHandle<T> tmpHandle = VirtualObjectManager.AllocateObject<T>(
                ref byteBuffer,
                objectSize,
                out byte* valueDestinationPtr);
            VirtualListHandle<T> handle = new VirtualListHandle<T>(tmpHandle.MetadataByteIndex, tmpHandle.Version);

            UnsafeUtility.CopyStructureToPtr(ref list, valueDestinationPtr);
            valueDestinationPtr += (long)UnsafeUtility.SizeOf<VirtualList<T>>();
            UnsafeUtility.MemClear(valueDestinationPtr, list.GetDataCapacitySizeBytes());

            return handle;
        }
    }

    public unsafe struct VirtualListHandle<T> where T : unmanaged
    {
        internal readonly int MetadataByteIndex;
        internal readonly int Version;
        internal readonly VirtualObjectHandle<VirtualList<T>> _objectHandle;

        public const float GrowFactor = 2f;

        internal VirtualListHandle(int index, int version)
        {
            MetadataByteIndex = index;
            Version = version;
            _objectHandle = new VirtualObjectHandle<VirtualList<T>>(new VirtualObjectHandle(MetadataByteIndex, Version));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLength(ref DynamicBuffer<byte> byteBuffer, out int length)
        {
            if (VirtualObjectManager.TryGetObjectValue(
                ref byteBuffer,
                this._objectHandle,
                out VirtualList<T> list))
            {
                length = list._length;
                return true;
            }
            length = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLengthUnsafe(ref DynamicBuffer<byte> byteBuffer)
        {
            return VirtualObjectManager.Unsafe.GetObjectValueUnsafe(ref byteBuffer, this._objectHandle).Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetCapacity(ref DynamicBuffer<byte> byteBuffer, out int capacity)
        {
            if (VirtualObjectManager.TryGetObjectValue(
                ref byteBuffer,
                this._objectHandle,
                out VirtualList<T> list))
            {
                capacity = list._capacity;
                return true;
            }
            capacity = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCapacityUnsafe(ref DynamicBuffer<byte> byteBuffer)
        {
            return VirtualObjectManager.Unsafe.GetObjectValueUnsafe(ref byteBuffer, this._objectHandle).Capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLengthAndCapacity(ref DynamicBuffer<byte> byteBuffer, out int length, out int capacity)
        {
            if (VirtualObjectManager.TryGetObjectValue(
                ref byteBuffer,
                this._objectHandle,
                out VirtualList<T> list))
            {
                length = list._length;
                capacity = list._capacity;
                return true;
            }
            length = default;
            capacity = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetLengthAndCapacityUnsafe(ref DynamicBuffer<byte> byteBuffer, out int length, out int capacity)
        {
            VirtualList<T> list = VirtualObjectManager.Unsafe.GetObjectValueUnsafe(ref byteBuffer, this._objectHandle);
            length = list._length;
            capacity = list._capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryClear(ref DynamicBuffer<byte> byteBuffer)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                this._objectHandle,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                list._length = 0;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearUnsafe(ref DynamicBuffer<byte> byteBuffer)
        {
            byte* listPtr = VirtualObjectManager.Unsafe.GetObjectValuePtrUnsafe(
                ref byteBuffer, this._objectHandle);
            ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
            list._length = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetElementAt(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            out T value)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                this._objectHandle,
                out byte* listPtr))
            {
                T* listData = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
                value = listData[index];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetElementAtUnsafe(
            ref DynamicBuffer<byte> byteBuffer,
            int index)
        {
            byte* listPtr = VirtualObjectManager.Unsafe.GetObjectValuePtrUnsafe(
                ref byteBuffer, this._objectHandle);
            T* listDataPtr = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
            return listDataPtr[index];
        }

        /// <summary>
        /// Note: unsafe because as soon as the list grows and gets reallocated, the ref is no longer valid
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T TryGetUnsafeRefElementAt(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            out bool success)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                ref byteBuffer,
                this._objectHandle,
                out byte* listPtr))
            {
                T* listData = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
                T* elementPtr = listData + (long)(UnsafeUtility.SizeOf<T>() * index);
                success = true;
                return ref *elementPtr;
            }
            success = false;
            return ref *(T*)byteBuffer.GetUnsafePtr();
        }

        /// <summary>
        /// Note: unsafe because as soon as the list grows and gets reallocated, the ref is no longer valid
        /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetUnsafeRefElementAtUnsafe(
            ref DynamicBuffer<byte> byteBuffer,
            int index)
        {
            byte* listPtr = VirtualObjectManager.Unsafe.GetObjectValuePtrUnsafe(
                ref byteBuffer, this._objectHandle);
            T* listData = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
            T* elementPtr = listData + (long)(UnsafeUtility.SizeOf<T>() * index);
            return ref *elementPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetElementAt(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            T value)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                this._objectHandle,
                out byte* listPtr))
            {
                T* listData = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
                listData[index] = value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetElementAtUnsafe(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            T value)
        {
            byte* listPtr = VirtualObjectManager.Unsafe.GetObjectValuePtrUnsafe(
                ref byteBuffer, this._objectHandle);
            T* listData = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
            listData[index] = value;
        }

        public bool TryAsUnsafeVirtualArray(
            ref DynamicBuffer<byte> byteBuffer,
            out UnsafeVirtualArray<T> unsafeArray)
        {
            if (VirtualObjectManager.TryGetObjectValue(
                ref byteBuffer,
                this._objectHandle,
                out VirtualList<T> array))
            {
                byte* dataPtr = (byte*)byteBuffer.GetUnsafePtr() + (long)this.MetadataByteIndex + (long)UnsafeUtility.SizeOf<VirtualArray<T>>();
                unsafeArray = new UnsafeVirtualArray<T>((T*)dataPtr, array.Length);
                return true;
            }
            unsafeArray = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetCapacity(
            ref DynamicBuffer<byte> byteBuffer,
            int capacity)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                this._objectHandle,
                out byte* listPtr))
            {
                VirtualList<T> list = *(VirtualList<T>*)listPtr;
                if (list._capacity != capacity)
                {
                    VirtualObjectManager.ReallocateObject(
                        new VirtualObjectHandle(this.MetadataByteIndex, this.Version), 
                        ref byteBuffer, 
                        capacity);
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResize(
            ref DynamicBuffer<byte> byteBuffer,
            int newLength)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                this._objectHandle,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                if (newLength > list._capacity)
                {
                    TrySetCapacity(
                        ref byteBuffer,
                        (int)math.ceil(newLength * GrowFactor));
                    VirtualObjectManager.TryGetObjectValuePtr(
                        ref byteBuffer,
                        this._objectHandle,
                        out listPtr);
                    list = ref *(VirtualList<T>*)listPtr;
                }
                list._length = newLength;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(
            ref DynamicBuffer<byte> byteBuffer,
            T value)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                this._objectHandle,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                int newLength = list._length + 1;
                if (newLength > list._capacity)
                {
                    TrySetCapacity(
                        ref byteBuffer,
                        (int)math.ceil(newLength * GrowFactor));
                    VirtualObjectManager.TryGetObjectValuePtr(
                        ref byteBuffer,
                        this._objectHandle,
                        out listPtr);
                    list = ref *(VirtualList<T>*)listPtr;
                }
                T* listData = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
                listData[list._length] = value;
                list._length = newLength;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryInsertAt(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            T value)
        {
            if (index >= 0)
            {
                if (VirtualObjectManager.TryGetObjectValuePtr(
                    ref byteBuffer,
                    this._objectHandle,
                    out byte* listPtr))
                {
                    ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                    if (index < list.Length)
                    {
                        int newLength = list._length + 1;
                        if (newLength > list._capacity)
                        {
                            TrySetCapacity(
                                ref byteBuffer,
                                (int)math.ceil(newLength * GrowFactor));
                            VirtualObjectManager.TryGetObjectValuePtr(
                                ref byteBuffer,
                                this._objectHandle,
                                out listPtr);
                            list = ref *(VirtualList<T>*)listPtr;
                        }
                        int sizeOfVList = UnsafeUtility.SizeOf<VirtualList<T>>();
                        int sizeOfListDataType = UnsafeUtility.SizeOf<T>();
                        byte* dataStartPtr = listPtr + (long)(sizeOfVList + (sizeOfListDataType * index));
                        byte* dataDestinationPtr = dataStartPtr + (long)(sizeOfListDataType);
                        int dataSize = (list._length - index) * sizeOfListDataType;
                        UnsafeUtility.MemCpy(dataDestinationPtr, dataStartPtr, dataSize);
                        *(T*)dataStartPtr = value;
                        list._length = newLength;
                        return true;
                    }
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveAt(
            ref DynamicBuffer<byte> byteBuffer,
            int index)
        {
            if (index >= 0)
            {
                if (VirtualObjectManager.TryGetObjectValuePtr(
                    ref byteBuffer,
                    this._objectHandle,
                    out byte* listPtr))
                {
                    ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                    if (index < list._length)
                    {
                        if (index < list._length - 1)
                        {
                            int sizeOfVList = UnsafeUtility.SizeOf<VirtualList<T>>();
                            int sizeOfListDataType = UnsafeUtility.SizeOf<T>();
                            byte* dataDestinationPtr = listPtr + (long)(sizeOfVList + (sizeOfListDataType * index));
                            byte* dataStartPtr = dataDestinationPtr + (long)(sizeOfListDataType);
                            int dataSize = (list._length - index) * sizeOfListDataType;
                            UnsafeUtility.MemCpy(dataDestinationPtr, dataStartPtr, dataSize);
                        }
                        list._length -= 1;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}