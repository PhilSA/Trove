using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Trove;
using Unity.Mathematics;

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
        public int GetListDataSizeBytes()
        {
            return UnsafeUtility.SizeOf<T>() * _capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSizeBytes()
        {
            return UnsafeUtility.SizeOf<VirtualList<T>>() + GetListDataSizeBytes();
        }

        public VirtualList(
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer,
            int capacity,
            out VirtualListHandle<T> handle)
        {
            _length = 0;
            _capacity = capacity;

            int objectSize = GetSizeBytes();
            VirtualObjectHandle<T> tmpHandle = VirtualObjectManager.CreateObject<T>(
                ref dataIndexRangeElements,
                ref metaDataIndexRangeElements,
                ref byteBuffer,
                objectSize,
                out byte* valueDestinationPtr);
            handle = new VirtualListHandle<T>(tmpHandle.MetadataByteIndex, tmpHandle.Version);

            UnsafeUtility.CopyStructureToPtr(ref this, valueDestinationPtr);
            valueDestinationPtr += (long)UnsafeUtility.SizeOf<VirtualList<T>>();
            UnsafeUtility.MemClear(valueDestinationPtr, GetListDataSizeBytes());
        }
    }

    public unsafe struct VirtualListHandle<T> where T : unmanaged
    {
        internal readonly int MetadataByteIndex;
        internal readonly int Version;

        public const float GrowFactor = 2f;

        internal VirtualListHandle(int index, int version)
        {
            MetadataByteIndex = index;
            Version = version;
        }

        internal VirtualListHandle(VirtualObjectHandleRO<VirtualList<T>> handle)
        {
            MetadataByteIndex = handle.MetadataByteIndex;
            Version = handle.Version;
        }

        public static implicit operator VirtualListHandle<T>(VirtualObjectHandleRO<VirtualList<T>> o) => new VirtualListHandle<T>(o);
        public static implicit operator VirtualObjectHandleRO<VirtualList<T>>(VirtualListHandle<T> o) => new VirtualObjectHandleRO<VirtualList<T>>(o.MetadataByteIndex, o.Version);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryClear(ref DynamicBuffer<byte> byteBuffer)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                ref byteBuffer,
                this,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                list._length = 0;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryElementAt(
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            out T value)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                ref byteBuffer,
                this,
                out byte* listPtr))
            {
                T* listData = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
                value = listData[index];
                return true;
            }
            value = default;
            return false;
        }

        public bool TryAsUnsafeListRO(
            ref DynamicBuffer<byte> byteBuffer,
            out UnsafeList<T>.ReadOnly unsafeList)
        {
            if (VirtualObjectManager.TryGetObjectValue(
                ref byteBuffer,
                this,
                out VirtualList<T> list))
            {
                byte* dataPtr = (byte*)byteBuffer.GetUnsafePtr() + (long)this.MetadataByteIndex + (long)UnsafeUtility.SizeOf<VirtualList<T>>();
                unsafeList = new UnsafeList<T>((T*)dataPtr, list.Length).AsReadOnly();
                return true;
            }
            unsafeList = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetCapacity(
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer,
            int capacity)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                ref byteBuffer,
                this,
                out byte* listPtr))
            {
                VirtualList<T> list = *(VirtualList<T>*)listPtr;
                if (list._capacity != capacity)
                {
                    new VirtualList<T>(
                        ref dataIndexRangeElements,
                        ref metaDataIndexRangeElements,
                        ref byteBuffer,
                        capacity,
                        out VirtualListHandle<T> newListHandle);

                    if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                        ref byteBuffer,
                        newListHandle,
                        out byte* newlistPtr))
                    {
                        // Copy old list contents to new
                        ref VirtualList<T> newList = ref *(VirtualList<T>*)newlistPtr;
                        int newLength = math.min(capacity, list._length);
                        newList._capacity = capacity;
                        newList._length = newLength;
                        int sizeOfVirtualList = UnsafeUtility.SizeOf<VirtualList<T>>();
                        byte* oldListDataPtr = listPtr + (long)sizeOfVirtualList;
                        byte* newListDataPtr = newlistPtr + (long)sizeOfVirtualList;
                        UnsafeUtility.MemCpy(newListDataPtr, oldListDataPtr, UnsafeUtility.SizeOf<T>() * newList._length);

                        // Free old list
                        VirtualObjectManager.FreeObject<VirtualList<T>>(
                            ref dataIndexRangeElements,
                            ref metaDataIndexRangeElements,
                            ref byteBuffer,
                            this);

                        // Replace handle with new one
                        this = newListHandle;
                        return true;
                    }
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResize(
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer,
            int newLength)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                ref byteBuffer,
                this,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                if (newLength > list._capacity)
                {
                    TrySetCapacity(
                        ref dataIndexRangeElements,
                        ref metaDataIndexRangeElements,
                        ref byteBuffer,
                        (int)math.ceil(newLength * GrowFactor));
                    VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                        ref byteBuffer,
                        this,
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
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer,
            T value)
        {
            if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                ref byteBuffer,
                this,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                int newLength = list._length + 1;
                if (newLength > list._capacity)
                {
                    TrySetCapacity(
                        ref dataIndexRangeElements,
                        ref metaDataIndexRangeElements,
                        ref byteBuffer,
                        (int)math.ceil(newLength * GrowFactor));
                    VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                        ref byteBuffer,
                        this,
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
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer,
            int index,
            T value)
        {
            if (index >= 0)
            {
                if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                    ref byteBuffer,
                    this,
                    out byte* listPtr))
                {
                    ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                    if (index < list.Length)
                    {
                        int newLength = list._length + 1;
                        if (newLength > list._capacity)
                        {
                            TrySetCapacity(
                                ref dataIndexRangeElements,
                                ref metaDataIndexRangeElements,
                                ref byteBuffer,
                                (int)math.ceil(newLength * GrowFactor));
                            VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                                ref byteBuffer,
                                this,
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
                if (VirtualObjectManager.TryGetObjectValuePtr<VirtualList<T>>(
                    ref byteBuffer,
                    this,
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