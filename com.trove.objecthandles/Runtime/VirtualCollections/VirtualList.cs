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
        private int _length;
        private int _capacity;

        public int Length => _length;
        public int Capacity => _capacity;

        public const float GrowFactor = 2f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetListDataByteSize()
        {
            return UnsafeUtility.SizeOf<T>() * _capacity;
        }

        public static VirtualObjectHandleRO<VirtualList<T>> Allocate(
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer,
            int capacity)
        {
            VirtualList<T> list = new VirtualList<T>
            {
                _length = 0,
                _capacity = capacity,
            };

            VirtualObjectHandleRO<VirtualList<T>> listHandle = ValueObjectManager.CreateObject(
                ref dataIndexRangeElements,
                ref metaDataIndexRangeElements,
                ref byteBuffer,
                list);

            // Clear list data memory
            byte* dataStartPtr = (byte*)byteBuffer.GetUnsafePtr() + (long)listHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualList<T>>();
            UnsafeUtility.MemClear(dataStartPtr, list.GetListDataByteSize());

            return listHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryClear(
            VirtualObjectHandleRO<VirtualList<T>> listHandle,
            ref DynamicBuffer<byte> byteBuffer)
        {
            if (ValueObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                listHandle,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                list._length = 0;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryElementAt(
            VirtualObjectHandleRO<VirtualList<T>> listHandle,
            ref DynamicBuffer<byte> byteBuffer, 
            int index,
            out T value)
        {
            if (ValueObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                listHandle,
                out byte* listPtr))
            {
                T* listData = (T*)(listPtr + (long)UnsafeUtility.SizeOf<VirtualList<T>>());
                value = listData[index];
                return true;
            }
            value = default;
            return false;
        }

        public static bool TryAsUnsafeListRO(
            VirtualObjectHandleRO<VirtualList<T>> listHandle,
            ref DynamicBuffer<byte> byteBuffer,
            out UnsafeList<T>.ReadOnly unsafeList)
        {
            if (ValueObjectManager.TryGetObjectValue(
                ref byteBuffer,
                listHandle,
                out VirtualList<T> list))
            {
                byte* dataPtr = (byte*)byteBuffer.GetUnsafePtr() + (long)listHandle.MetadataByteIndex + (long)UnsafeUtility.SizeOf<VirtualList<T>>();
                unsafeList = new UnsafeList<T>((T*)dataPtr, list.Length).AsReadOnly();
                return true;
            }
            unsafeList = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetCapacity(
            ref VirtualObjectHandleRO<VirtualList<T>> listHandle,
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer, 
            int capacity)
        {
            if (ValueObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                listHandle,
                out byte* listPtr))
            {
                VirtualList<T> list = *(VirtualList<T>*)listPtr;

                VirtualObjectHandleRO<VirtualList<T>> newListHandle = Allocate(
                    ref dataIndexRangeElements,
                    ref metaDataIndexRangeElements,
                    ref byteBuffer,
                    capacity);

                if (ValueObjectManager.TryGetObjectValuePtr(
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
                    ValueObjectManager.FreeObject(
                        ref dataIndexRangeElements,
                        ref metaDataIndexRangeElements,
                        ref byteBuffer,
                        listHandle);

                    listHandle = newListHandle;
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResize(
            ref VirtualObjectHandleRO<VirtualList<T>> listHandle,
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer, 
            int newLength)
        {
            if (ValueObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                listHandle,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                if (newLength > list._capacity)
                {
                    TrySetCapacity(
                        ref listHandle,
                        ref dataIndexRangeElements,
                        ref metaDataIndexRangeElements,
                        ref byteBuffer,
                        (int)math.ceil(newLength * GrowFactor));
                    ValueObjectManager.TryGetObjectValuePtr(
                        ref byteBuffer,
                        listHandle,
                        out listPtr);
                    list = ref *(VirtualList<T>*)listPtr;
                }
                list._length = newLength;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAdd(
            ref VirtualObjectHandleRO<VirtualList<T>> listHandle,
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer, 
            T value)
        {
            if (ValueObjectManager.TryGetObjectValuePtr(
                ref byteBuffer,
                listHandle,
                out byte* listPtr))
            {
                ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                int newLength = list._length + 1;
                if (newLength > list._capacity)
                {
                    TrySetCapacity(
                        ref listHandle,
                        ref dataIndexRangeElements,
                        ref metaDataIndexRangeElements,
                        ref byteBuffer,
                        (int)math.ceil(newLength * GrowFactor));
                    ValueObjectManager.TryGetObjectValuePtr(
                        ref byteBuffer,
                        listHandle,
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
        public static bool TryInsertAt(
            ref VirtualObjectHandleRO<VirtualList<T>> listHandle,
            ref DynamicBuffer<IndexRangeElement> dataIndexRangeElements,
            ref DynamicBuffer<IndexRangeElement> metaDataIndexRangeElements,
            ref DynamicBuffer<byte> byteBuffer, 
            int index,
            T value)
        {
            if (index >= 0)
            {
                if (ValueObjectManager.TryGetObjectValuePtr(
                    ref byteBuffer,
                    listHandle,
                    out byte* listPtr))
                {
                    ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                    if (index < list.Length)
                    {
                        int newLength = list._length + 1;
                        if (newLength > list._capacity)
                        {
                            TrySetCapacity(
                                ref listHandle,
                                ref dataIndexRangeElements,
                                ref metaDataIndexRangeElements,
                                ref byteBuffer,
                                (int)math.ceil(newLength * GrowFactor));
                            ValueObjectManager.TryGetObjectValuePtr(
                                ref byteBuffer,
                                listHandle,
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
        public static bool TryRemoveAt(
            VirtualObjectHandleRO<VirtualList<T>> listHandle,
            ref DynamicBuffer<byte> byteBuffer, 
            int index)
        {
            if (index >= 0)
            {
                if (ValueObjectManager.TryGetObjectValuePtr(
                    ref byteBuffer,
                    listHandle,
                    out byte* listPtr))
                {
                    ref VirtualList<T> list = ref *(VirtualList<T>*)listPtr;
                    if (index < list._length)
                    {
                        if(index < list._length - 1)
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