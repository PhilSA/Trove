using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;

namespace Trove.ObjectHandles
{
    public unsafe static partial class VirtualObjectManager
    {
        public struct MemoryInfo
        {
            public int DatasStartIndex;
            public UnsafeVirtualList<IndexRangeElement> DataFreeRanges;
        }

        private const int FreeRangesInitialCapacity = 16;
        private const float ObjectsCapacityGrowFactor = 2f;

        private static readonly int SizeOf_VirtualObjectHeader = UnsafeUtility.SizeOf<VirtualObjectHeader>();
        private static readonly int ByteIndex_ObjectIdCounter = 0; // ulong
        private static readonly int ByteIndex_FreeDataRangesList = ByteIndex_ObjectIdCounter + UnsafeUtility.SizeOf<ulong>(); // UnsafeVirtualList<IndexRangeElement>
        private static readonly int ByteIndex_ObjectDataStart = ByteIndex_FreeDataRangesList + UnsafeUtility.SizeOf<UnsafeVirtualList<IndexRangeElement>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IncrementAndGetObjectId<V>(ref V voView, out ulong objectId)
            where V : unmanaged, IVirtualObjectView
        {
            ByteArrayUtilities.ReadValue(voView.GetDataPtr(), ByteIndex_ObjectIdCounter, out objectId);
            objectId++;
            ByteArrayUtilities.WriteValue(voView.GetDataPtr(), ByteIndex_ObjectIdCounter, objectId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetFreeDataRangesList<V>(ref V voView, out UnsafeVirtualList<IndexRangeElement> value)
            where V : unmanaged, IVirtualObjectView
        {
            ByteArrayUtilities.ReadValue(voView.GetDataPtr(), ByteIndex_FreeDataRangesList, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetFreeDataRangesList<V>(ref V voView, UnsafeVirtualList<IndexRangeElement> value)
            where V : unmanaged, IVirtualObjectView
        {
            ByteArrayUtilities.WriteValue(voView.GetDataPtr(), ByteIndex_FreeDataRangesList, value);
        }

        public static void Initialize<V>(
            ref V voView,
            int objectDataBytesCapacity)
                where V : unmanaged, IVirtualObjectView
        {
            // Set vo byte size
            voView.Clear();
            int dataSize =
                ByteIndex_ObjectDataStart + // system data
                (SizeOf_VirtualObjectHeader + UnsafeUtility.SizeOf<UnsafeVirtualList<IndexRangeElement>>() + (FreeRangesInitialCapacity * UnsafeUtility.SizeOf<IndexRangeElement>())) + // free ranges data
                objectDataBytesCapacity; // objects data
            voView.Resize(dataSize, NativeArrayOptions.ClearMemory);

            // Allocate system virtual list for free ranges
            IndexRangeElement dataFreeRange = new IndexRangeElement
            {
                StartInclusive = ByteIndex_ObjectDataStart,
                EndExclusive = voView.GetLength(),
            };
            UnsafeVirtualList<IndexRangeElement> dataFreeRangesList = CreateSystemList<IndexRangeElement, V>(
                ref dataFreeRange,
                ref voView,
                FreeRangesInitialCapacity);

            // Add free data range to list
            bool addSuccess = dataFreeRangesList.TryAdd(ref voView, dataFreeRange);
            Assert.IsTrue(addSuccess);
            SetFreeDataRangesList(ref voView, dataFreeRangesList);
        }

        public static VirtualObjectHandle<T> CreateObject<T, V>(
            ref V voView,
            T objectValue)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            int objectSize = UnsafeUtility.SizeOf<T>();
            VirtualObjectHandle<T> returnHandle = AllocateObject(
                ref voView,
                objectSize,
                out T* valueDestinationPtr);
            *valueDestinationPtr = objectValue;
            return returnHandle;
        }

        public static VirtualObjectHandle<byte> CreateObjectFromWriter<T, V>(
            ref V voView,
            T objectByteWriter)
                where T : unmanaged, IObjectByteWriter
                where V : unmanaged, IVirtualObjectView
        {
            int objectSize = objectByteWriter.GetByteSize();
            VirtualObjectHandle<byte> returnHandle = AllocateObject(
                ref voView,
                objectSize,
                out byte* valueDestinationPtr);
            objectByteWriter.Write((byte*)valueDestinationPtr);
            return returnHandle;
        }

        public static VirtualObjectHandle<T> AllocateObject<T, V>(
            ref V voView,
            int objectSize,
            out T* valueDestinationPtr)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            int objectSizeWithHeader = objectSize + SizeOf_VirtualObjectHeader;

            GetFreeDataRangesList(ref voView, out UnsafeVirtualList<IndexRangeElement> dataFreeRangesList);

            // Find a start index to write the data to in free ranges, or create new memory if not
            if (!FindFreeRange(
                    dataFreeRangesList,
                    ref voView,
                    objectSizeWithHeader,
                    out int indexOfFreeRangeToConsumeFrom))
            {
                ResizeBufferAndExpandFreeRangesForObjectDataCapacityIncrease(
                    ref dataFreeRangesList,
                    ref voView);

                indexOfFreeRangeToConsumeFrom = dataFreeRangesList.Length - 1;
            }

            ConsumeFromFreeRange(
                ref dataFreeRangesList,
                ref voView,
                indexOfFreeRangeToConsumeFrom,
                objectSizeWithHeader,
                out int dataStartIndex);

            // Write object header
            int objectByteIndex = dataStartIndex;
            IncrementAndGetObjectId(ref voView, out ulong newObjectId);
            VirtualObjectHeader header = new VirtualObjectHeader(newObjectId, objectSize);
            ByteArrayUtilities.WriteValue(voView.GetDataPtr(), ref dataStartIndex, header);

            valueDestinationPtr = (T*)(voView.GetDataPtr() + (long)dataStartIndex);
            return new VirtualObjectHandle<T>(objectByteIndex, newObjectId);
        }

        public static void FreeObject<T, V>(
            ref V voView,
            VirtualObjectHandle<T> objectHandle)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            bool byteIdexValid = objectHandle.ByteIndex + SizeOf_VirtualObjectHeader <= voView.GetLength();
            if (byteIdexValid)
            {
                ByteArrayUtilities.ReadValue(voView.GetDataPtr(), objectHandle.ByteIndex, out VirtualObjectHeader voHeader);
                if (voHeader.IsSameObject(objectHandle))
                {
                    int objectSizeWithHeader = voHeader.Size + SizeOf_VirtualObjectHeader;

                    GetFreeDataRangesList(ref voView, out UnsafeVirtualList<IndexRangeElement> dataFreeRangesList);

                    // Free data
                    FreeRangeForStartIndexAndSize(
                        ref dataFreeRangesList,
                        ref voView,
                        objectHandle.ByteIndex,
                        objectSizeWithHeader);

                    // Clear
                    byte* objectdataPtr = voView.GetDataPtr() + (long)objectHandle.ByteIndex;
                    UnsafeUtility.MemClear(objectdataPtr, objectSizeWithHeader);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObjectValue<T, V>(
            ref V voView,
            VirtualObjectHandle<T> objectHandle,
            out T value)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            // TODO: should I keep this check?
            bool byteIdexValid = objectHandle.ByteIndex + SizeOf_VirtualObjectHeader + UnsafeUtility.SizeOf<T>() <= voView.GetLength();
            if (byteIdexValid)
            {
                byte* dataPtr = voView.GetDataPtr() + (long)objectHandle.ByteIndex;
                VirtualObjectHeader voHeader = *(VirtualObjectHeader*)dataPtr;
                if (voHeader.IsSameObject(objectHandle))
                {
                    dataPtr += (long)SizeOf_VirtualObjectHeader;
                    value = *(T*)dataPtr;
                    return true;
                }
            }

            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T TryGetObjectValueRef<T, V>(
            ref V voView,
            VirtualObjectHandle<T> objectHandle,
            out bool success)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            // TODO: should I keep this check?
            bool byteIdexValid = objectHandle.ByteIndex + SizeOf_VirtualObjectHeader + UnsafeUtility.SizeOf<T>() <= voView.GetLength();
            if (byteIdexValid)
            {
                byte* dataPtr = voView.GetDataPtr() + (long)objectHandle.ByteIndex;
                VirtualObjectHeader voHeader = *(VirtualObjectHeader*)dataPtr;
                if (voHeader.IsSameObject(objectHandle))
                {
                    success = true;

                    return ref *(T*)(dataPtr + (long)SizeOf_VirtualObjectHeader);
                }
            }

            success = false;
            return ref *(T*)voView.GetDataPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetObjectValuePtr<T, V>(
            ref V voView,
            VirtualObjectHandle<T> objectHandle,
            out T* valuePtr)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            // TODO: should I keep this check?
            bool byteIdexValid = objectHandle.ByteIndex + SizeOf_VirtualObjectHeader + UnsafeUtility.SizeOf<T>() <= voView.GetLength();
            if (byteIdexValid)
            {
                ByteArrayUtilities.ReadValue(voView.GetDataPtr(), objectHandle.ByteIndex, out VirtualObjectHeader voHeader);
                if (voHeader.IsSameObject(objectHandle))
                {
                    valuePtr = (T*)(voView.GetDataPtr() + (long)objectHandle.ByteIndex + (long)SizeOf_VirtualObjectHeader);
                    return true;
                }
            }

            valuePtr = (T*)voView.GetDataPtr();
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T, V>(
            ref V voView,
            VirtualObjectHandle<T> objectHandle)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            // TODO: should I keep this check?
            bool byteIdexValid = objectHandle.ByteIndex + SizeOf_VirtualObjectHeader + UnsafeUtility.SizeOf<T>() <= voView.GetLength();
            if (byteIdexValid)
            {
                ByteArrayUtilities.ReadValue(voView.GetDataPtr(), objectHandle.ByteIndex, out VirtualObjectHeader voHeader);
                if (voHeader.IsSameObject(objectHandle))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetObjectValue<T, V>(
            ref V voView,
            VirtualObjectHandle<T> objectHandle,
            T value)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            // TODO: should I keep this check?
            bool byteIdexValid = objectHandle.ByteIndex + SizeOf_VirtualObjectHeader + UnsafeUtility.SizeOf<T>() <= voView.GetLength();
            if (byteIdexValid)
            {
                ByteArrayUtilities.ReadValue(voView.GetDataPtr(), objectHandle.ByteIndex, out VirtualObjectHeader voHeader);
                if (voHeader.IsSameObject(objectHandle))
                {
                    byte* objPtr = voView.GetDataPtr() + (long)objectHandle.ByteIndex;
                    *(T*)objPtr = value;
                    return true;
                }
            }

            return false;
        }

        public static void TrimCapacity<V>(
            ref V voView,
            int minDataBytesCapacity)
                where V : unmanaged, IVirtualObjectView
        {
            GetFreeDataRangesList(ref voView, out UnsafeVirtualList<IndexRangeElement> dataFreeRangesList);
            int initialBufferLength = voView.GetLength();

            FindLastUsedIndex(
                dataFreeRangesList,
                ref voView,
                ByteIndex_ObjectDataStart,
                initialBufferLength,
                out int lastUsedIndex);
            int newSizeDataBytes = math.max(0, math.max(minDataBytesCapacity, (lastUsedIndex - ByteIndex_ObjectDataStart) + 1));
            int newLength = ByteIndex_ObjectDataStart + newSizeDataBytes;

            ClearRangesPastEndIndex(
                ref dataFreeRangesList,
                ref voView,
                newLength);

            // Resize from datas
            voView.Resize(newLength, NativeArrayOptions.ClearMemory);
            voView.SetCapacity(newLength);
        }

        public static VirtualObjectManager.MemoryInfo GetMemoryInfo<V>(ref V voView)
                where V : unmanaged, IVirtualObjectView
        {
            VirtualObjectManager.MemoryInfo memoryInfo = new VirtualObjectManager.MemoryInfo();
            memoryInfo.DatasStartIndex = ByteIndex_ObjectDataStart;
            GetFreeDataRangesList(ref voView, out memoryInfo.DataFreeRanges);
            return memoryInfo;
        }

        ////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////

        internal static UnsafeVirtualList<T> CreateSystemList<T, V>(
            ref IndexRangeElement dataFreeIndexRange,
            ref V voView,
            int capacity)
                where T : unmanaged
                where V : unmanaged, IVirtualObjectView
        {
            UnsafeVirtualList<T> newList = new UnsafeVirtualList<T>
            {
                _capacity = capacity,
                _length = 0
            };
            int listDataSize = newList.GetDataCapacitySizeBytes();
            int objectSizeWithHeader = listDataSize + SizeOf_VirtualObjectHeader;

            // Consume free range
            int dataStartIndex = dataFreeIndexRange.StartInclusive;
            dataFreeIndexRange.StartInclusive += objectSizeWithHeader;
            Assert.IsTrue(dataFreeIndexRange.EndExclusive >= dataFreeIndexRange.StartInclusive);

            // Assign handle manually
            IncrementAndGetObjectId(ref voView, out ulong newObjectId);
            newList._dataHandle = new VirtualObjectHandle<T>(dataStartIndex, newObjectId);

            // Write object
            ByteArrayUtilities.WriteValue(voView.GetDataPtr(), ref dataStartIndex, new VirtualObjectHeader(newObjectId, listDataSize));
            byte* valueDestinationPtr = voView.GetDataPtr() + (long)dataStartIndex;
            UnsafeUtility.MemClear(valueDestinationPtr, listDataSize);

            return newList;
        }

        internal static void FreeRangeForStartIndexAndSize<V>(
            ref UnsafeVirtualList<IndexRangeElement> freeDataRangesList,
            ref V voView,
            int objectStartIndex,
            int objectSize)
                where V : unmanaged, IVirtualObjectView
        {
            bool success = freeDataRangesList.TryAsUnsafeArrayView(ref voView, out UnsafeArrayView<IndexRangeElement> rangesUnsafeArray);
            Assert.IsTrue(success);

            // Iterate ranges to determine which range to add the freed memory to (or where to insert new range)
            for (int i = 0; i < rangesUnsafeArray.Length; i++)
            {
                IndexRangeElement tmpRange = rangesUnsafeArray[i];

                // Assert no ranges overlap
                Assert.IsFalse(ObjectManagerUtilities.RangesOverlap(objectStartIndex, (objectStartIndex + objectSize), tmpRange.StartInclusive, tmpRange.EndExclusive));

                // Merge at beginning
                if (tmpRange.StartInclusive == objectStartIndex + objectSize)
                {
                    tmpRange.StartInclusive -= objectSize;
                    rangesUnsafeArray[i] = tmpRange;
                    return;
                }
                // Merge at end
                else if (tmpRange.EndExclusive == objectStartIndex)
                {
                    tmpRange.EndExclusive += objectSize;
                    rangesUnsafeArray[i] = tmpRange;
                    return;
                }
                // Insert
                else if (tmpRange.StartInclusive > objectStartIndex)
                {
                    success = freeDataRangesList.TryInsertAt(ref voView, i, new IndexRangeElement
                    {
                        StartInclusive = objectStartIndex,
                        EndExclusive = objectStartIndex + objectSize,
                    });
                    Assert.IsTrue(success);
                    SetFreeDataRangesList(ref voView, freeDataRangesList);
                    return;
                }
            }

            // If we haven't found a match and returned yet, Add range
            success = freeDataRangesList.TryAdd(ref voView, new IndexRangeElement
            {
                StartInclusive = objectStartIndex,
                EndExclusive = objectStartIndex + objectSize,
            });
            Assert.IsTrue(success);
            SetFreeDataRangesList(ref voView, freeDataRangesList);
        }

        internal static bool FindLastUsedIndex<V>(
            UnsafeVirtualList<IndexRangeElement> dataFreeRangesList,
            ref V voView,
            int dataStartIndexInclusive,
            int dataEndIndexExclusive,
            out int lastUsedIndex)
                where V : unmanaged, IVirtualObjectView
        {
            bool success = dataFreeRangesList.TryAsUnsafeArrayView(ref voView, out UnsafeArrayView<IndexRangeElement> rangesUnsafeArray);
            Assert.IsTrue(success);

            int evaluatedIndex = dataEndIndexExclusive - 1;
            for (int i = rangesUnsafeArray.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = rangesUnsafeArray[i];
                if (ObjectManagerUtilities.RangeContains(tmpRange.StartInclusive, tmpRange.EndExclusive, evaluatedIndex))
                {
                    // If the ranges contains the index, that means this evaluated index is free.
                    // Continue checking from the start of that free range.
                    evaluatedIndex = tmpRange.StartInclusive - 1;
                }
                else
                {
                    // If the ranges don't overlap, that means the last used index is the iterated one
                    lastUsedIndex = evaluatedIndex;
                    return true;
                }
            }

            if (evaluatedIndex >= 0)
            {
                lastUsedIndex = evaluatedIndex;
                return true;
            }

            lastUsedIndex = -1;
            return false;
        }

        internal static bool FindFreeRange<V>(
            UnsafeVirtualList<IndexRangeElement> dataFreeRangesList,
            ref V voView,
            int objectIndexesSize,
            out int indexOfFreeRange)
                where V : unmanaged, IVirtualObjectView
        {
            bool success = dataFreeRangesList.TryAsUnsafeArrayView(ref voView, out UnsafeArrayView<IndexRangeElement> rangesUnsafeArray);
            Assert.IsTrue(success);

            for (int i = 0; i < rangesUnsafeArray.Length; i++)
            {
                IndexRangeElement indexRange = rangesUnsafeArray[i];
                if (indexRange.EndExclusive - indexRange.StartInclusive >= objectIndexesSize)
                {
                    indexOfFreeRange = i;
                    return true;
                }
            }

            indexOfFreeRange = -1;
            return false;
        }

        internal static void ConsumeFromFreeRange<V>(
            ref UnsafeVirtualList<IndexRangeElement> freeDataRangesList,
            ref V voView,
            int freeRangeIndex,
            int objectSize,
            out int consumedStartIndex)
                where V : unmanaged, IVirtualObjectView
        {
            bool success = freeDataRangesList.TryGetElementAt(ref voView, freeRangeIndex, out IndexRangeElement freeRange);
            Assert.IsTrue(success);

            consumedStartIndex = freeRange.StartInclusive;
            freeRange.StartInclusive += objectSize;

            Assert.IsTrue(freeRange.StartInclusive <= freeRange.EndExclusive);

            if (freeRange.StartInclusive == freeRange.EndExclusive)
            {
                success = freeDataRangesList.TryRemoveAt(ref voView, freeRangeIndex);
                Assert.IsTrue(success);
                SetFreeDataRangesList(ref voView, freeDataRangesList);
            }
            else
            {
                success = freeDataRangesList.TrySetElementAt(ref voView, freeRangeIndex, freeRange);
                Assert.IsTrue(success);
            }
        }

        internal static void ClearRangesPastEndIndex<V>(
            ref UnsafeVirtualList<IndexRangeElement> freeDataRangesList,
            ref V voView,
            int newEndIndexExclusive)
                where V : unmanaged, IVirtualObjectView
        {
            bool success = freeDataRangesList.TryAsUnsafeArrayView(ref voView, out UnsafeArrayView<IndexRangeElement> rangesUnsafeArray);
            Assert.IsTrue(success);

            for (int i = rangesUnsafeArray.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = rangesUnsafeArray[i];

                if (tmpRange.StartInclusive >= newEndIndexExclusive)
                {
                    // Remove
                    success = freeDataRangesList.TryRemoveAt(ref voView, i);
                    Assert.IsTrue(success);
                    SetFreeDataRangesList(ref voView, freeDataRangesList);
                }
                else if (tmpRange.EndExclusive > newEndIndexExclusive)
                {
                    // Trim
                    tmpRange.EndExclusive = newEndIndexExclusive;
                    rangesUnsafeArray[i] = tmpRange;
                    break;
                }
            }
        }

        internal static void ResizeBufferAndExpandFreeRangesForObjectDataCapacityIncrease<V>(
            ref UnsafeVirtualList<IndexRangeElement> dataFreeRangesList,
            ref V voView)
                where V : unmanaged, IVirtualObjectView
        {
            bool success;

            int prevBufferLength = voView.GetLength();
            int prevDatasByteCapacity = voView.GetLength() - ByteIndex_ObjectDataStart;
            int newDatasByteCapacity = (int)math.ceil(prevDatasByteCapacity * ObjectsCapacityGrowFactor);
            int newLength = (int)math.ceil(voView.GetLength() + (newDatasByteCapacity - prevDatasByteCapacity));
            voView.Resize(newLength, NativeArrayOptions.ClearMemory);

            // Add new free range for the expanded capacity
            if (dataFreeRangesList.Length > 0)
            {
                int indexOfLastRange = dataFreeRangesList.Length - 1;
                success = dataFreeRangesList.TryGetElementAt(ref voView, indexOfLastRange, out IndexRangeElement freeRange);
                Assert.IsTrue(success);

                // Expand the last range if it ended at the previous end of the buffer
                if (freeRange.EndExclusive == prevBufferLength)
                {
                    freeRange.EndExclusive = voView.GetLength();
                    success = dataFreeRangesList.TrySetElementAt(ref voView, indexOfLastRange, freeRange);
                    Assert.IsTrue(success);
                    return;
                }
            }

            // If we couldn't just expand the last range, add a new range
            success = dataFreeRangesList.TryAdd(ref voView, new IndexRangeElement
            {
                StartInclusive = prevBufferLength,
                EndExclusive = voView.GetLength(),
            });
            Assert.IsTrue(success);
            SetFreeDataRangesList(ref voView, dataFreeRangesList);
        }
    }
}