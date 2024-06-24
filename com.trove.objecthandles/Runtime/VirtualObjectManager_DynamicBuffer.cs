using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;

namespace Trove.ObjectHandles
{
    public unsafe static partial class VirtualObjectManager
    {
        public static void Initialize(
            ref DynamicBuffer<byte> elementsByteBuffer,
            int objectsCapacity,
            int objectDataBytesCapacity)
        {
            elementsByteBuffer.Clear();

            byte* byteArrayPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            int bufferSize = 
                ByteIndex_MetadatasStart + // system data
                (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectsCapacity) + // objects metadata
                (UnsafeUtility.SizeOf<VirtualList<IndexRangeElement>>() * 2) + (FreeRangesInitialCapacity * UnsafeUtility.SizeOf<IndexRangeElement>() * 2) + // free ranges data
                objectDataBytesCapacity; // objects data
            elementsByteBuffer.Resize(bufferSize, NativeArrayOptions.ClearMemory);

            // Write element buffer internal data
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            SetObjectMetadatasCapacityValue(bufferPtr, objectsCapacity);
            CalculateObjectDatasStartIndex(objectsCapacity, out int objectDatasStartIndex);
            SetObjectDatasStartIndexValue(bufferPtr, objectDatasStartIndex);

            IndexRangeElement metadataFreeRange = new IndexRangeElement
            {
                StartInclusive = ByteIndex_MetadatasStart,
                EndExclusive = objectDatasStartIndex,
            };
            IndexRangeElement dataFreeRange = new IndexRangeElement
            {
                StartInclusive = objectDatasStartIndex,
                EndExclusive = elementsByteBuffer.Length,
            };

            // Allocate system virtual lists for free ranges
            VirtualListHandle<IndexRangeElement> metadataFreeRangesListHandle = CreateSystemList<IndexRangeElement>(
                ref metadataFreeRange,
                ref dataFreeRange,
                ref elementsByteBuffer, 
                FreeRangesInitialCapacity);
            VirtualListHandle<IndexRangeElement> dataFreeRangesListHandle = CreateSystemList<IndexRangeElement>(
                ref metadataFreeRange,
                ref dataFreeRange,
                ref elementsByteBuffer,
                FreeRangesInitialCapacity);
            SetMetadataFreeRangesListHandle(bufferPtr, metadataFreeRangesListHandle);
            SetDataFreeRangesListHandle(bufferPtr, dataFreeRangesListHandle);

            bool addSuccess = metadataFreeRangesListHandle.TryAdd(ref elementsByteBuffer, metadataFreeRange);
            addSuccess = addSuccess && dataFreeRangesListHandle.TryAdd(ref elementsByteBuffer, dataFreeRange);
            Assert.IsTrue(addSuccess);
        }

        public static VirtualObjectHandle<T> CreateObject<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            T objectValue)
            where T : unmanaged
        {
            int objectSize = UnsafeUtility.SizeOf<T>();
            VirtualObjectHandle<T> returnHandle = AllocateObject<T>(
                ref elementsByteBuffer,
                objectSize,
                out byte* valueDestinationPtr);
            UnsafeUtility.CopyStructureToPtr(ref objectValue, valueDestinationPtr);
            return returnHandle;
        }

        public static VirtualObjectHandle<T> CreateObjectFromWriter<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            T objectByteWriter)
            where T : unmanaged, IObjectByteWriter
        {
            int objectSize = objectByteWriter.GetByteSize();
            VirtualObjectHandle<T> returnHandle = AllocateObject<T>(
                ref elementsByteBuffer,
                objectSize,
                out byte* valueDestinationPtr);
            objectByteWriter.Write(valueDestinationPtr);
            return returnHandle;
        }

        public static VirtualObjectHandle<T> AllocateObject<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            int objectSize,
            out byte* valueDestinationPtr)
            where T : unmanaged
        {
            bool success;
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetMetadataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> metadataRangesHandle);
            GetDataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> dataRangesHandle);

            // Metadatas
            int metadataIndex;
            {
                if (!FindFreeRange(
                        metadataRangesHandle,
                        ref elementsByteBuffer, 
                        UnsafeUtility.SizeOf<VirtualObjectMetadata>(), 
                        out int indexOfFreeRange))
                {
                    ResizeBufferAndExpandFreeRangesForMetadataCapacityIncrease(
                        metadataRangesHandle,
                        dataRangesHandle,
                        ref elementsByteBuffer,
                        out int prevObjectDatasStartIndex,
                        out int newObjectDatasStartIndex);

                    success = metadataRangesHandle.TryGetLength(ref elementsByteBuffer, out int freeRangesLength);
                    Assert.IsTrue(success);
                    indexOfFreeRange = freeRangesLength - 1;
                }

                ConsumeFromFreeRange(
                    metadataRangesHandle,
                    ref elementsByteBuffer,
                    indexOfFreeRange,
                    UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                    out metadataIndex);
            }

            bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();

            // Datas
            int dataStartIndex;
            {
                if (!FindFreeRange(
                        dataRangesHandle,
                        ref elementsByteBuffer,
                        objectSize, 
                        out int indexOfFreeRange))
                {
                    ResizeBufferAndExpandFreeRangesForObjectDataCapacityIncrease(
                        dataRangesHandle,
                        ref elementsByteBuffer);

                    success = dataRangesHandle.TryGetLength(ref elementsByteBuffer, out int freeRangesLength);
                    Assert.IsTrue(success);
                    indexOfFreeRange = freeRangesLength - 1;
                }

                ConsumeFromFreeRange(
                    dataRangesHandle,
                    ref elementsByteBuffer,
                    indexOfFreeRange,
                    objectSize,
                    out dataStartIndex);
            }

            // Update metadata
            bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            ByteArrayUtilities.ReadValue(bufferPtr, metadataIndex, out VirtualObjectMetadata objectMetadata);
            objectMetadata.Version++;
            objectMetadata.Size = objectSize;
            objectMetadata.ByteIndex = dataStartIndex;
            ByteArrayUtilities.WriteValue(bufferPtr, metadataIndex, objectMetadata);

            // Write object
            valueDestinationPtr = bufferPtr + (long)dataStartIndex;

            return new VirtualObjectHandle<T>(new VirtualObjectHandle(metadataIndex, objectMetadata.Version));
        }

        public static void FreeObject(
           ref DynamicBuffer<byte> elementsByteBuffer,
           VirtualObjectHandle objectHandle)
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndexValue(bufferPtr, out int endIndexOfMetadatasExclusive);
            bool metadataIndexValid = objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>() <= endIndexOfMetadatasExclusive;
            if (metadataIndexValid)
            {
                ByteArrayUtilities.ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
                    GetMetadataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> metadataRangesHandle);
                    GetDataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> dataRangesHandle);

                    // Free metadata
                    {
                        FreeRangeForStartIndexAndSize(
                            metadataRangesHandle,
                            ref elementsByteBuffer, 
                            objectHandle.MetadataByteIndex, 
                            UnsafeUtility.SizeOf<VirtualObjectMetadata>());
                    }

                    // Free data
                    {
                        FreeRangeForStartIndexAndSize(
                            dataRangesHandle,
                            ref elementsByteBuffer,
                            objectMetadata.ByteIndex, 
                            objectMetadata.Size);
                    }

                    // Update metadata
                    objectMetadata.Version++;
                    objectMetadata.Size = 0;
                    objectMetadata.ByteIndex = 0;
                    ByteArrayUtilities.WriteValue(bufferPtr, objectHandle.MetadataByteIndex, objectMetadata);
                }
            }
        }

        internal static void ReallocateObject(
            VirtualObjectHandle handle,
            ref DynamicBuffer<byte> byteBuffer,
            int newSize)
        {
            bool success;
            byte* bufferPtr = (byte*)byteBuffer.GetUnsafePtr();

            ByteArrayUtilities.ReadValue(bufferPtr, handle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
            int oldSize = objectMetadata.Size;
            int oldByteIndexStart = objectMetadata.ByteIndex;

            if (newSize != oldSize)
            {
                GetDataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> dataRangesHandle);

                // Find a new place to allocate data
                if (!FindFreeRange(
                        dataRangesHandle,
                        ref byteBuffer,
                        newSize,
                        out int indexOfFreeRange))
                {
                    ResizeBufferAndExpandFreeRangesForObjectDataCapacityIncrease(
                        dataRangesHandle, 
                        ref byteBuffer);

                    success = dataRangesHandle.TryGetLength(ref byteBuffer, out int freeRangesLength);
                    Assert.IsTrue(success);
                    indexOfFreeRange = freeRangesLength - 1;
                }

                ConsumeFromFreeRange(
                    dataRangesHandle,
                    ref byteBuffer,
                    indexOfFreeRange,
                    newSize,
                    out int newDataByteIndex);

                // Copy data over to new location
                byte* oldDataPtr = bufferPtr + (long)oldByteIndexStart;
                byte* newDataPtr = bufferPtr + (long)newDataByteIndex;
                UnsafeUtility.MemCpy(newDataPtr, oldDataPtr, oldSize);

                // Update metadata byteindex and size (but not version, because this still represents the same object)
                objectMetadata.ByteIndex = newDataByteIndex;
                objectMetadata.Size = newSize;
                ByteArrayUtilities.WriteValue(bufferPtr, handle.MetadataByteIndex, objectMetadata);

                // Free old object data range
                FreeRangeForStartIndexAndSize(
                    dataRangesHandle,
                    ref byteBuffer,
                    oldByteIndexStart,
                    oldSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObjectValue<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle<T> objectHandle,
            out T value)
            where T : unmanaged
        {
            if (TryGetObjectValuePtr(
                ref elementsByteBuffer,
                new VirtualObjectHandle<T>(new VirtualObjectHandle(objectHandle.MetadataByteIndex, objectHandle.Version)),
                out byte* valuePtr))
            {
                value = *(T*)valuePtr;
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetObjectValuePtr<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle<T> objectHandle,
            out byte* valuePtr)
            where T : unmanaged
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndexValue(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>() <= endIndexOfMetadatasExclusive)
            {
                ByteArrayUtilities.ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
                    valuePtr = bufferPtr + (long)objectMetadata.ByteIndex;
                    return true;
                }
            }

            valuePtr = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle<T> objectHandle)
            where T : unmanaged
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndexValue(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>() <= endIndexOfMetadatasExclusive)
            {
                ByteArrayUtilities.ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetObjectValue<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle<T> objectHandle,
            T value)
            where T : unmanaged
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndexValue(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>() <= endIndexOfMetadatasExclusive)
            {
                ByteArrayUtilities.ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
                    byte* objPtr = bufferPtr + (long)objectMetadata.ByteIndex;
                    *(T*)objPtr = value;
                    return true;
                }
            }

            return false;
        }

        public static void TrimCapacity(
            ref DynamicBuffer<byte> elementsByteBuffer,
            int minMetadatasCapacity,
            int minDataBytesCapacity)
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetMetadataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> metadataRangesHandle);
            GetDataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> dataRangesHandle);
            int initialBufferLength = elementsByteBuffer.Length;
            GetObjectDatasStartIndexValue(bufferPtr, out int prevEndIndexOfMetadatasExclusive);

            // Metadatas
            int newSizeMetaDataBytes;
            {
                FindLastUsedIndex(
                    metadataRangesHandle,
                    ref elementsByteBuffer, 
                    ByteIndex_MetadatasStart, 
                    prevEndIndexOfMetadatasExclusive, 
                    out int lastUsedIndex);

                Assert.AreEqual(0, ((lastUsedIndex - ByteIndex_MetadatasStart) + 1) % 3);
                int lastUsedMetadataObjectIndex = ((lastUsedIndex - ByteIndex_MetadatasStart) + 1) / 3;
                int newMetadatasCapacity = math.max(3, math.max(minMetadatasCapacity, lastUsedMetadataObjectIndex));
                newSizeMetaDataBytes = newMetadatasCapacity * UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                int newEndIndexOfMetadatasExclusive = ByteIndex_MetadatasStart + newSizeMetaDataBytes;
                int metadatasCapacityDiffInBytes = newEndIndexOfMetadatasExclusive - prevEndIndexOfMetadatasExclusive;

                ClearRangesPastEndIndex(
                    metadataRangesHandle,
                    ref elementsByteBuffer,
                    newEndIndexOfMetadatasExclusive);

                // Shift data back
                ShiftFreeRanges(
                    metadataRangesHandle,
                    ref elementsByteBuffer,
                    metadatasCapacityDiffInBytes);
                ShiftMetadataByteIndexes(bufferPtr, metadatasCapacityDiffInBytes, lastUsedMetadataObjectIndex);

                bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
                SetObjectMetadatasCapacityValue(bufferPtr, newMetadatasCapacity);
                CalculateObjectDatasStartIndex(newMetadatasCapacity, out int objectDatasStartIndex);
                SetObjectDatasStartIndexValue(bufferPtr, objectDatasStartIndex);

                int sizeDatas = elementsByteBuffer.Length - prevEndIndexOfMetadatasExclusive;
                byte* destPtr = bufferPtr + (long)newEndIndexOfMetadatasExclusive;
                byte* startPtr = bufferPtr + (long)prevEndIndexOfMetadatasExclusive;
                UnsafeUtility.MemCpy(destPtr, startPtr, sizeDatas);
            }

            // Datas
            int newSizeDataBytes;
            {
                FindLastUsedIndex(
                    dataRangesHandle,
                    ref elementsByteBuffer,
                    prevEndIndexOfMetadatasExclusive, 
                    initialBufferLength, 
                    out int lastUsedIndex);
                newSizeDataBytes = math.max(0, math.max(minDataBytesCapacity, (lastUsedIndex - prevEndIndexOfMetadatasExclusive) + 1));
                int newEndOfDatasExclusive = ByteIndex_MetadatasStart + newSizeMetaDataBytes + newSizeDataBytes;

                ClearRangesPastEndIndex(
                    dataRangesHandle,
                    ref elementsByteBuffer,
                    newEndOfDatasExclusive);
            }

            // Resize from datas
            int newSize = ByteIndex_MetadatasStart + newSizeMetaDataBytes + newSizeDataBytes;
            elementsByteBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            elementsByteBuffer.Capacity = newSize;
        }

        public static VirtualObjectManager.MemoryInfo GetMemoryInfo(ref DynamicBuffer<byte> bytesBuffer)
        {
            byte* bufferPtr = (byte*)bytesBuffer.GetUnsafePtr();

            VirtualObjectManager.MemoryInfo memoryInfo = new VirtualObjectManager.MemoryInfo();

            memoryInfo.MetadatasStartIndex = ByteIndex_MetadatasStart;
            GetObjectMetadatasCapacityValue(bufferPtr, out memoryInfo.MetadatasCapacity);
            GetObjectDatasStartIndexValue(bufferPtr, out memoryInfo.DatasStartIndex);
            GetMetadataFreeRangesListHandle(bufferPtr, out memoryInfo.MetadataFreeRangesHandle);
            ByteArrayUtilities.ReadValue(bufferPtr, memoryInfo.MetadataFreeRangesHandle.MetadataByteIndex, out VirtualObjectMetadata metadataFreeRangesMetadata);
            memoryInfo.MetadataFreeRangesStartIndex = metadataFreeRangesMetadata.ByteIndex;
            memoryInfo.MetadataFreeRangesSize = metadataFreeRangesMetadata.Size;
            GetDataFreeRangesListHandle(bufferPtr, out memoryInfo.DataFreeRangesHandle);
            ByteArrayUtilities.ReadValue(bufferPtr, memoryInfo.DataFreeRangesHandle.MetadataByteIndex, out VirtualObjectMetadata dataFreeRangesMetadata);
            memoryInfo.DataFreeRangesStartIndex = dataFreeRangesMetadata.ByteIndex;
            memoryInfo.DataFreeRangesSize = dataFreeRangesMetadata.Size;

            return memoryInfo;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////

        internal static VirtualListHandle<T> CreateSystemList<T>(
            ref IndexRangeElement metaDataFreeIndexRange,
            ref IndexRangeElement dataFreeIndexRange,
            ref DynamicBuffer<byte> elementsByteBuffer,
            int capacity)
            where T : unmanaged
        {
            VirtualList<T> newList = new VirtualList<T>
            {
                _capacity = capacity,
                _length = 0
            };
            int totalListSize = newList.GetSizeBytes();

            // Consume free ranges
            int metadataIndex = metaDataFreeIndexRange.StartInclusive;
            int dataStartIndex = dataFreeIndexRange.StartInclusive;
            metaDataFreeIndexRange.StartInclusive += UnsafeUtility.SizeOf<VirtualObjectMetadata>();
            dataFreeIndexRange.StartInclusive += totalListSize;
            Assert.IsTrue(metaDataFreeIndexRange.EndExclusive >= metaDataFreeIndexRange.StartInclusive);
            Assert.IsTrue(dataFreeIndexRange.EndExclusive >= dataFreeIndexRange.StartInclusive);

            // Update metadata
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            ByteArrayUtilities.ReadValue(bufferPtr, metadataIndex, out VirtualObjectMetadata objectMetadata);
            objectMetadata.Version++;
            objectMetadata.Size = totalListSize;
            objectMetadata.ByteIndex = dataStartIndex;
            ByteArrayUtilities.WriteValue(bufferPtr, metadataIndex, objectMetadata);

            // Write object
            byte* valueDestinationPtr = bufferPtr + (long)dataStartIndex;
            UnsafeUtility.CopyStructureToPtr(ref newList, valueDestinationPtr);
            valueDestinationPtr += (long)UnsafeUtility.SizeOf<VirtualList<T>>();
            UnsafeUtility.MemClear(valueDestinationPtr, newList.GetDataCapacitySizeBytes());

            return new VirtualListHandle<T>(metadataIndex, objectMetadata.Version);
        }

        internal static void FreeRangeForStartIndexAndSize(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int objectStartIndex, 
            int objectSize)
        {
            GetObjectDatasStartIndexValue((byte*)bytesBuffer.GetUnsafePtr(), out int tmp);

            bool success = freeIndexRangesListHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> rangesUnsafeArray);
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
                    success = freeIndexRangesListHandle.TryInsertAt(ref bytesBuffer, i, new IndexRangeElement
                    {
                        StartInclusive = objectStartIndex,
                        EndExclusive = objectStartIndex + objectSize,
                    });
                    Assert.IsTrue(success);
                    return;
                }
            }

            // If we haven't found a match and returned yet, Add range
            success = freeIndexRangesListHandle.TryAdd(ref bytesBuffer, new IndexRangeElement
            {
                StartInclusive = objectStartIndex,
                EndExclusive = objectStartIndex + objectSize,
            });
            Assert.IsTrue(success);
        }

        internal static bool FindLastUsedIndex(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int dataStartIndexInclusive, 
            int dataEndIndexExclusive, 
            out int lastUsedIndex)
        {
            bool success = freeIndexRangesListHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> rangesUnsafeArray);
            Assert.IsTrue(success);

            int evaluatedIndex = dataEndIndexExclusive - 1;
            for (int i = rangesUnsafeArray.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = rangesUnsafeArray[i];

                Assert.IsTrue(evaluatedIndex >= 0);

                if (ObjectManagerUtilities.RangesOverlap(evaluatedIndex, evaluatedIndex + 1, tmpRange.StartInclusive, tmpRange.EndExclusive))
                {
                    // If the ranges overlap, that means this evaluated index is free.
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

            lastUsedIndex = -1;
            return false;
        }

        internal static bool FindFreeRange(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int objectIndexesSize,
            out int indexOfFreeRange)
        {
            bool success = freeIndexRangesListHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> rangesUnsafeArray);
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

        internal static void ExpandFreeRangesAfterResize(
            VirtualListHandle<IndexRangeElement> freeRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int previousEndIndexExclusive, 
            int newEndIndexExclusive)
        {
            bool success = freeRangesListHandle.TryGetLength(ref bytesBuffer, out int listLength);
            Assert.IsTrue(success);

            // Add new free range for the expanded capacity
            if (listLength > 0)
            {
                int indexOfLastRange = listLength - 1;
                success = freeRangesListHandle.TryGetElementAt(ref bytesBuffer, indexOfLastRange, out IndexRangeElement freeRange);
                Assert.IsTrue(success);

                if (freeRange.EndExclusive == previousEndIndexExclusive)
                {
                    // Expand the last range
                    freeRange.EndExclusive = newEndIndexExclusive;
                    success = freeRangesListHandle.TrySetElementAt(ref bytesBuffer, indexOfLastRange, freeRange);
                    Assert.IsTrue(success);
                    return;
                }
            }

            // Create a new range
            success = freeRangesListHandle.TryAdd(ref bytesBuffer, new IndexRangeElement
            {
                StartInclusive = previousEndIndexExclusive,
                EndExclusive = newEndIndexExclusive,
            });
            Assert.IsTrue(success);
        }

        internal static void ShiftFreeRanges(
            VirtualListHandle<IndexRangeElement> freeRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int indexShift)
        {
            bool success = freeRangesListHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> rangesUnsafeArray);
            Assert.IsTrue(success);

            for (int i = 0; i < rangesUnsafeArray.Length; i++)
            {
                IndexRangeElement freeRange = rangesUnsafeArray[i];
                freeRange.StartInclusive += indexShift;
                freeRange.EndExclusive += indexShift;
                rangesUnsafeArray[i] = freeRange;
            }
        }

        internal static void ConsumeFromFreeRange(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int freeRangeIndex,
            int objectSize,
            out int consumedStartIndex)
        {
            bool success;
            success = freeIndexRangesListHandle.TryGetElementAt(ref bytesBuffer, freeRangeIndex, out IndexRangeElement freeRange);
            Assert.IsTrue(success);

            consumedStartIndex = freeRange.StartInclusive;
            freeRange.StartInclusive += objectSize;

            Assert.IsTrue(freeRange.StartInclusive <= freeRange.EndExclusive);

            if (freeRange.StartInclusive == freeRange.EndExclusive)
            {
                success = freeIndexRangesListHandle.TryRemoveAt(ref bytesBuffer, freeRangeIndex);
                Assert.IsTrue(success);
            }
            else
            {
                success = freeIndexRangesListHandle.TrySetElementAt(ref bytesBuffer, freeRangeIndex, freeRange);
                Assert.IsTrue(success);
            }
        }

        internal static void ClearRangesPastEndIndex(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int newEndIndexExclusive)
        {
            bool success = freeIndexRangesListHandle.TryGetLength(ref bytesBuffer, out int initialLength);
            Assert.IsTrue(success);

            for (int i = initialLength - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesListHandle.GetElementAtUnsafe(ref bytesBuffer, i);

                if (tmpRange.StartInclusive >= newEndIndexExclusive)
                {
                    // Remove
                    success = freeIndexRangesListHandle.TryRemoveAt(ref bytesBuffer, i);
                    Assert.IsTrue(success);
                }
                else if (tmpRange.EndExclusive > newEndIndexExclusive)
                {
                    // Trim
                    tmpRange.EndExclusive = newEndIndexExclusive;
                    freeIndexRangesListHandle.SetElementAtUnsafe(ref bytesBuffer, i, tmpRange);
                    break;
                }
            }
        }

        internal static void ResizeBufferAndExpandFreeRangesForMetadataCapacityIncrease(
            VirtualListHandle<IndexRangeElement> metadataRangesHandle,
            VirtualListHandle<IndexRangeElement> dataRangesHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            out int prevObjectDatasStartIndex,
            out int newObjectDatasStartIndex)
        {
            byte* bufferPtr = (byte*)bytesBuffer.GetUnsafePtr();

            // Increase buffer capacity for expanded metadatas
            int prevElementsBufferLength = bytesBuffer.Length;
            GetObjectDatasStartIndexValue(bufferPtr, out prevObjectDatasStartIndex);
            GetObjectMetadatasCapacityValue(bufferPtr, out int prevMetadatasCapacity);
            int newMetadatasCapacity = (int)math.ceil(prevMetadatasCapacity * ObjectsCapacityGrowFactor);
            int metadatasCapacityDiffInBytes = (newMetadatasCapacity - prevMetadatasCapacity) * UnsafeUtility.SizeOf<VirtualObjectMetadata>();
            int newLength = bytesBuffer.Length + metadatasCapacityDiffInBytes;

            bytesBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);
            bufferPtr = (byte*)bytesBuffer.GetUnsafePtr();

            // Shift indexes
            ShiftFreeRanges(
                dataRangesHandle,
                ref bytesBuffer,
                metadatasCapacityDiffInBytes);
            ShiftMetadataByteIndexes(
                bufferPtr, 
                metadatasCapacityDiffInBytes, 
                prevMetadatasCapacity);

            // Note: must be done after the shift, because resolving freerange list objects relies of the ObjectDatasStartIndex
            CalculateObjectDatasStartIndex(newMetadatasCapacity, out newObjectDatasStartIndex);
            SetObjectMetadatasCapacityValue(bufferPtr, newMetadatasCapacity);
            SetObjectDatasStartIndexValue(bufferPtr, newObjectDatasStartIndex);

            // Move object data
            byte* destPtr = bufferPtr + (long)newObjectDatasStartIndex;
            byte* startPtr = bufferPtr + (long)prevObjectDatasStartIndex;
            int copiedDataSize = prevElementsBufferLength - prevObjectDatasStartIndex;
            UnsafeUtility.MemCpy(destPtr, startPtr, copiedDataSize);

            // Clear newly allocated metadatas 
            destPtr = bufferPtr + (long)prevObjectDatasStartIndex;
            int clearedDataSize = newObjectDatasStartIndex - prevObjectDatasStartIndex;
            UnsafeUtility.MemClear(destPtr, clearedDataSize);

            ExpandFreeRangesAfterResize(
                metadataRangesHandle,
                ref bytesBuffer,
                prevObjectDatasStartIndex,
                newObjectDatasStartIndex);
        }

        internal static void ResizeBufferAndExpandFreeRangesForObjectDataCapacityIncrease(
            VirtualListHandle<IndexRangeElement> dataRangesHandle, 
            ref DynamicBuffer<byte> bytesBuffer)
        {
            byte* bufferPtr = (byte*)bytesBuffer.GetUnsafePtr();
            GetObjectDatasStartIndexValue(bufferPtr, out int objectDatasStartIndex);
            int prevBufferLength = bytesBuffer.Length;
            int prevDatasByteCapacity = bytesBuffer.Length - objectDatasStartIndex;
            int newDatasByteCapacity = (int)math.ceil(prevDatasByteCapacity * ObjectsCapacityGrowFactor);
            int newLength = (int)math.ceil(bytesBuffer.Length + (newDatasByteCapacity - prevDatasByteCapacity));
            bytesBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

            ExpandFreeRangesAfterResize(
                dataRangesHandle,
                ref bytesBuffer,
                prevBufferLength,
                bytesBuffer.Length);
        }

        public unsafe static partial class Unsafe
        {
            /// <summary>
            /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
            /// This means if the object was freed or if the bytes buffer was trimmed since obtaining the object handle, 
            /// the returned value could be garbage data. For safety, call .Exists() before using this
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T GetObjectValueUnsafe<T>(
                ref DynamicBuffer<byte> elementsByteBuffer,
                VirtualObjectHandle<T> objectHandle)
                where T : unmanaged
            {
                byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
                ByteArrayUtilities.ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                return *(T*)(bufferPtr + (long)objectMetadata.ByteIndex);
            }

            /// <summary>
            /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
            /// This means if the object was freed or if the bytes buffer was trimmed since obtaining the object handle, 
            /// the returned value could be garbage data. For safety, call .Exists() before using this
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static byte* GetObjectValuePtrUnsafe<T>(
                ref DynamicBuffer<byte> elementsByteBuffer,
                VirtualObjectHandle<T> objectHandle)
                where T : unmanaged
            {
                byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
                ByteArrayUtilities.ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                return bufferPtr + (long)objectMetadata.ByteIndex;
            }

            /// <summary>
            /// Note: unsafe because we don't check if the metadata index is in bounds, and don't check for a version match.
            /// This means if the object was freed or if the bytes buffer was trimmed since obtaining the object handle, 
            /// this will potentially overwrite the value of another object. For safety, call .Exists() before using this
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void SetObjectValueUnsafe<T>(
                ref DynamicBuffer<byte> elementsByteBuffer,
                VirtualObjectHandle<T> objectHandle,
                T value)
                where T : unmanaged
            {
                byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
                ByteArrayUtilities.ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                byte* objPtr = bufferPtr + (long)objectMetadata.ByteIndex;
                *(T*)objPtr = value;
            }
        }
    }
}
