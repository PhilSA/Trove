using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;

namespace Trove.ObjectHandles
{
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

    public unsafe static class VirtualObjectManager
    {
        public struct VirtualObjectMetadata
        {
            public int ByteIndex;
            public int Version;
            public int Size;
        }

        private const int FreeRangesInitialCapacity = 64;
        private const float ObjectsCapacityGrowFactor = 2f;

        private static int ByteIndex_ObjectMetadataCapacity = 0;
        private static int ByteIndex_ObjectMetadataCount = ByteIndex_ObjectMetadataCapacity + UnsafeUtility.SizeOf<int>();
        private static int ByteIndex_ObjectDataStartIndex = ByteIndex_ObjectMetadataCount + UnsafeUtility.SizeOf<int>();
        private static int ByteIndex_MetadataFreeRangesHandle = ByteIndex_ObjectDataStartIndex + UnsafeUtility.SizeOf<int>();
        private static int ByteIndex_ObjectDataFreeRangesHandle = ByteIndex_MetadataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualListHandle<IndexRangeElement>>();
        private static int ByteIndex_MetadatasStartIndex = ByteIndex_ObjectDataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualListHandle<IndexRangeElement>>();

        public static void Initialize(
            ref DynamicBuffer<byte> elementsByteBuffer,
            int objectsCapacity,
            int objectDataBytesCapacity)
        {
            elementsByteBuffer.Clear();

            byte* byteArrayPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            int bufferSize = 
                ByteIndex_MetadatasStartIndex + // system data
                (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectsCapacity) + // objects metadata
                ((FreeRangesInitialCapacity * UnsafeUtility.SizeOf<IndexRangeElement>()) * 2) + // free ranges data
                objectDataBytesCapacity; // objects data
            elementsByteBuffer.Resize(bufferSize, NativeArrayOptions.ClearMemory);

            // Write element buffer internal data
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            SetObjectMetadatasCapacity(bufferPtr, objectsCapacity);
            SetObjectMetadatasCount(bufferPtr, 0);

            GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
            IndexRangeElement dataFreeRange = new IndexRangeElement
            {
                StartInclusive = objectDatasStartIndex,
                EndExclusive = elementsByteBuffer.Length,
            };
            IndexRangeElement metadataFreeRange = new IndexRangeElement
            {
                StartInclusive = ByteIndex_MetadatasStartIndex,
                EndExclusive = objectDatasStartIndex,
            };

            // Allocate system virtual lists for free ranges
            VirtualListHandle<IndexRangeElement> metadataFreeRangesListHandle = CreateSystemList<IndexRangeElement>(
                ref dataFreeRange,
                ref metadataFreeRange,
                ref elementsByteBuffer, 
                FreeRangesInitialCapacity);
            VirtualListHandle<IndexRangeElement> dataFreeRangesListHandle = CreateSystemList<IndexRangeElement>(
                ref dataFreeRange,
                ref metadataFreeRange,
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
            VirtualObjectHandle<T> returnHandle = CreateObject<T>(
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
            VirtualObjectHandle<T> returnHandle = CreateObject<T>(
                ref elementsByteBuffer,
                objectSize,
                out byte* valueDestinationPtr);
            objectByteWriter.Write(valueDestinationPtr);
            return returnHandle;
        }

        public static VirtualObjectHandle<T> CreateObject<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            int objectSize,
            out byte* valueDestinationPtr)
            where T : unmanaged
        {
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
                        out IndexRangeElement freeRange, 
                        out int indexOfFreeRange))
                {
                    // Increase buffer capacity for expanded metadatas
                    int prevElementsBufferLength = elementsByteBuffer.Length;
                    GetObjectDatasStartIndex(bufferPtr, out int prevObjectDatasStartIndex);
                    GetObjectMetadatasCapacity(bufferPtr, out int prevMetadatasCapacity);
                    int newMetadatasCapacity = (int)math.ceil(prevMetadatasCapacity * ObjectsCapacityGrowFactor);
                    SetObjectMetadatasCapacity(bufferPtr, newMetadatasCapacity);
                    int metadatasCapacityDiffInBytes = (newMetadatasCapacity - prevMetadatasCapacity) * UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                    int newLength = elementsByteBuffer.Length + metadatasCapacityDiffInBytes;
                    elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                    bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
                    GetObjectDatasStartIndex(bufferPtr, out int newObjectDatasStartIndex);

                    // Move object data
                    byte* destPtr = bufferPtr + (long)newObjectDatasStartIndex;
                    byte* startPtr = bufferPtr + (long)prevMetadatasCapacity;
                    UnsafeUtility.MemCpy(destPtr, startPtr, (prevElementsBufferLength - prevObjectDatasStartIndex));
                    ShiftFreeRanges(
                        metadataRangesHandle,
                        ref elementsByteBuffer, 
                        metadatasCapacityDiffInBytes);
                    ShiftMetadataByteIndexes(bufferPtr, metadatasCapacityDiffInBytes, newObjectDatasStartIndex);

                    GetExpandedFreeRange(
                        metadataRangesHandle,
                        ref elementsByteBuffer, 
                        prevObjectDatasStartIndex, 
                        newObjectDatasStartIndex,
                        out freeRange, 
                        out indexOfFreeRange);
                }

                ConsumeFromFreeRanges(
                    metadataRangesHandle,
                    ref elementsByteBuffer,
                    freeRange,
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
                        out IndexRangeElement freeRange, 
                        out int indexOfFreeRange))
                {
                    // Increase buffer capacity for expanded object data
                    GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
                    int prevDatasByteCapacity = elementsByteBuffer.Length - objectDatasStartIndex;
                    int newDatasByteCapacity = (int)math.ceil(prevDatasByteCapacity * ObjectsCapacityGrowFactor);
                    int newLength = (int)math.ceil(elementsByteBuffer.Length + (newDatasByteCapacity - prevDatasByteCapacity));
                    elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                    GetExpandedFreeRange(
                        dataRangesHandle,
                        ref elementsByteBuffer,
                        objectDatasStartIndex, 
                        elementsByteBuffer.Length,
                        out freeRange, 
                        out indexOfFreeRange);
                }

                ConsumeFromFreeRanges(
                    dataRangesHandle,
                    ref elementsByteBuffer,
                    freeRange,
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

            return new VirtualObjectHandle<T>(new VirtualObjectHandle(objectMetadata.ByteIndex, objectMetadata.Version));
        }

        public static void FreeObject(
           ref DynamicBuffer<byte> elementsByteBuffer,
           VirtualObjectHandle objectHandle)
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
            bool metadataIndexValid = objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>() <= endIndexOfMetadatasExclusive;
            if (metadataIndexValid)
            {
                GetMetadataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> metadataRangesHandle);
                GetDataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> dataRangesHandle);

                ByteArrayUtilities.ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
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
                    objectMetadata.ByteIndex = -1;
                    ByteArrayUtilities.WriteValue(bufferPtr, objectHandle.MetadataByteIndex, objectMetadata);
                }
            }
        }

        internal static void ReallocateObject(
            VirtualObjectHandle handle,
            ref DynamicBuffer<byte> byteBuffer,
            int newSize)
        {
            byte* bufferPtr = (byte*)byteBuffer.GetUnsafePtr();

            ref VirtualObjectMetadata objectMetadataRef = ref ByteArrayUtilities.ReadValueAsRef<VirtualObjectMetadata>(bufferPtr, handle.MetadataByteIndex);
            int oldSize = objectMetadataRef.Size;

            if (newSize != oldSize)
            {
                int oldByteIndexStart = objectMetadataRef.ByteIndex;
                GetDataFreeRangesListHandle(bufferPtr, out VirtualListHandle<IndexRangeElement> dataRangesHandle);

                // Find a new place to allocate data
                int newDataByteIndex;
                {
                    if (!FindFreeRange(
                            dataRangesHandle,
                            ref byteBuffer,
                            newSize,
                            out IndexRangeElement freeRange,
                            out int indexOfFreeRange))
                    {
                        // Increase buffer capacity for expanded object data
                        GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
                        int prevDatasByteCapacity = byteBuffer.Length - objectDatasStartIndex;
                        int newDatasByteCapacity = (int)math.ceil(prevDatasByteCapacity * ObjectsCapacityGrowFactor);
                        int newLength = (int)math.ceil(byteBuffer.Length + (newDatasByteCapacity - prevDatasByteCapacity));
                        byteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                        GetExpandedFreeRange(
                            dataRangesHandle,
                            ref byteBuffer,
                            objectDatasStartIndex,
                            byteBuffer.Length,
                            out freeRange,
                            out indexOfFreeRange);
                    }

                    ConsumeFromFreeRanges(
                        dataRangesHandle,
                        ref byteBuffer,
                        freeRange,
                        indexOfFreeRange,
                        newSize,
                        out newDataByteIndex);
                }

                // Copy data over to new location
                byte* oldDataPtr = bufferPtr + (long)objectMetadataRef.ByteIndex;
                byte* newDataPtr = bufferPtr + (long)newDataByteIndex;
                UnsafeUtility.MemCpy(newDataPtr, oldDataPtr, oldSize);

                // Update metadata byteindex and size (but not version, because this still represents the same object)
                objectMetadataRef.ByteIndex = newDataByteIndex;
                objectMetadataRef.Size = newSize;

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
            bool success = TryGetObjectValuePtr(
                ref elementsByteBuffer,
                new VirtualObjectHandle<T>(new VirtualObjectHandle(objectHandle.MetadataByteIndex, objectHandle.Version)),
                out byte* valuePtr);
            value = *(T*)valuePtr;
            return success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetObjectValuePtr<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle<T> objectHandle,
            out byte* valuePtr)
            where T : unmanaged
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
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
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
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
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
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
            GetObjectDatasStartIndex(bufferPtr, out int prevEndIndexOfMetadatasExclusive);

            // Metadatas
            int newSizeMetaDataBytes;
            {
                FindLastUsedIndex(
                    metadataRangesHandle,
                    ref elementsByteBuffer, 
                    ByteIndex_MetadatasStartIndex, 
                    prevEndIndexOfMetadatasExclusive, 
                    out int lastUsedIndex);
                newSizeMetaDataBytes = math.max(0, math.max(minMetadatasCapacity * UnsafeUtility.SizeOf<VirtualObjectMetadata>(), (lastUsedIndex - ByteIndex_MetadatasStartIndex) + 1));
                int newEndIndexOfMetadatasExclusive = ByteIndex_MetadatasStartIndex + newSizeMetaDataBytes;

                ClearRangesPastEndIndex(
                    metadataRangesHandle,
                    ref elementsByteBuffer,
                    newEndIndexOfMetadatasExclusive);

                // Shift data back
                int sizeDatas = elementsByteBuffer.Length - prevEndIndexOfMetadatasExclusive;
                int metadatasCapacityDiffInBytes = newEndIndexOfMetadatasExclusive - prevEndIndexOfMetadatasExclusive;
                byte* destPtr = bufferPtr + (long)newEndIndexOfMetadatasExclusive;
                byte* startPtr = bufferPtr + (long)prevEndIndexOfMetadatasExclusive;
                UnsafeUtility.MemCpy(destPtr, startPtr, sizeDatas);
                ShiftFreeRanges(
                    metadataRangesHandle,
                    ref elementsByteBuffer,
                    metadatasCapacityDiffInBytes);
                ShiftMetadataByteIndexes(bufferPtr, metadatasCapacityDiffInBytes, newEndIndexOfMetadatasExclusive);
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
                int newEndOfDatasExclusive = ByteIndex_MetadatasStartIndex + newSizeMetaDataBytes + newSizeDataBytes;

                ClearRangesPastEndIndex(
                    dataRangesHandle,
                    ref elementsByteBuffer,
                    newEndOfDatasExclusive);
            }

            // Resize from datas
            int newSize = ByteIndex_MetadatasStartIndex + newSizeMetaDataBytes + newSizeDataBytes;
            elementsByteBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            elementsByteBuffer.Capacity = newSize;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectMetadatasCapacity(byte* byteArrayPtr, out int value)
        {
            ByteArrayUtilities.ReadValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCapacity, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectMetadatasCount(byte* byteArrayPtr, out int value)
        {
            ByteArrayUtilities.ReadValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCount, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectDatasStartIndex(byte* byteArrayPtr, out int value)
        {
            ByteArrayUtilities.ReadValue<int>(byteArrayPtr, ByteIndex_ObjectDataStartIndex, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateAndSetObjectDatasStartIndex(byte* byteArrayPtr)
        {
            GetObjectMetadatasCapacity(byteArrayPtr, out int objectMetadatasCapacity);
            int startIndex = ByteIndex_MetadatasStartIndex + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectMetadatasCapacity);
            ByteArrayUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectDataStartIndex, startIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetObjectMetadatasCapacity(byte* byteArrayPtr, int value)
        {
            ByteArrayUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCapacity, value);
            CalculateAndSetObjectDatasStartIndex(byteArrayPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetObjectMetadatasCount(byte* byteArrayPtr, int value)
        {
            ByteArrayUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCount, value);
        }

        private static VirtualListHandle<T> CreateSystemList<T>(
            ref IndexRangeElement dataFreeIndexRange,
            ref IndexRangeElement metaDataFreeIndexRange,
            ref DynamicBuffer<byte> elementsByteBuffer,
            int capacity)
            where T : unmanaged
        {
            aa
            // TODO
            //Assert.IsTrue(dataFreeIndexRange.EndExclusive - dataFreeIndexRange.StartInclusive > capacity);
            //Assert.IsTrue(metaDataFreeIndexRange.EndExclusive - metaDataFreeIndexRange.StartInclusive > capacity);

            // TODO
            //ConsumeFreeRange(freeIndexRange, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out bool isFullyConsumed, out int consumedStartIndex);
            //if (isFullyConsumed)
            //{
            //    if (indexOfFreeRange >= 0) // If the range was already stored, remove it
            //    {
            //        metaDataFreeIndexRangesBuffer.RemoveAt(indexOfFreeRange);
            //    }
            //}
            //else
            //{
            //    if (indexOfFreeRange >= 0) // If the range was already stored, overwrite it
            //    {
            //        metaDataFreeIndexRangesBuffer[indexOfFreeRange] = freeIndexRange;
            //    }
            //    else // If the range wasn't stored, add it
            //    {
            //        metaDataFreeIndexRangesBuffer.Add(freeIndexRange);
            //    }
            //}
            //metadataIndex = consumedStartIndex;

            return default;
        }

        private static void GetDataFreeRangesListHandle(byte* byteArrayPtr, out VirtualListHandle<IndexRangeElement> value)
        {
            ByteArrayUtilities.ReadValue(byteArrayPtr, ByteIndex_ObjectDataFreeRangesHandle, out value);
        }

        private static void GetMetadataFreeRangesListHandle(byte* byteArrayPtr, out VirtualListHandle<IndexRangeElement> value)
        {
            ByteArrayUtilities.ReadValue(byteArrayPtr, ByteIndex_MetadataFreeRangesHandle, out value);
        }

        private static void SetDataFreeRangesListHandle(byte* byteArrayPtr, VirtualListHandle<IndexRangeElement> value)
        {
            ByteArrayUtilities.WriteValue(byteArrayPtr, ByteIndex_ObjectDataFreeRangesHandle, value);
        }

        private static void SetMetadataFreeRangesListHandle(byte* byteArrayPtr, VirtualListHandle<IndexRangeElement> value)
        {
            ByteArrayUtilities.WriteValue(byteArrayPtr, ByteIndex_MetadataFreeRangesHandle, value);
        }

        private static void FreeRangeForStartIndexAndSize(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int objectStartIndex, 
            int objectSize)
        {
            bool success = freeIndexRangesListHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> asUnsafeArray);
            Assert.IsTrue(success);

            // Iterate ranges to determine which range to add the freed memory to (or where to insert new range)
            bool foundRangeInsertionPoint = false;
            for (int i = 0; i < asUnsafeArray.Length; i++)
            {
                IndexRangeElement tmpRange = asUnsafeArray[i];

                // Assert no ranges overlap
                Assert.IsFalse(ObjectManagerUtilities.RangesOverlap(objectStartIndex, (objectStartIndex + objectSize), tmpRange.StartInclusive, tmpRange.EndExclusive));

                // Merge at beginning
                if (tmpRange.StartInclusive == objectStartIndex + objectSize)
                {
                    ref IndexRangeElement rangeElement = ref freeIndexRangesListHandle.TryGetUnsafeRefElementAt(ref bytesBuffer, i, out success);
                    rangeElement.StartInclusive -= objectSize;
                    break;
                }
                // Merge at end
                else if (tmpRange.EndExclusive == objectStartIndex)
                {
                    ref IndexRangeElement rangeElement = ref freeIndexRangesListHandle.TryGetUnsafeRefElementAt(ref bytesBuffer, i, out success);
                    rangeElement.EndExclusive += objectSize;
                    break;
                }
                // Insert
                else if (tmpRange.StartInclusive > objectStartIndex)
                {
                    freeIndexRangesListHandle.TryInsertAt(ref bytesBuffer, i, new IndexRangeElement
                    {
                        StartInclusive = objectStartIndex,
                        EndExclusive = objectStartIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                    });
                    break;
                }
            }

            if(!foundRangeInsertionPoint)
            {
                freeIndexRangesListHandle.TryAdd(ref bytesBuffer, new IndexRangeElement
                {
                    StartInclusive = objectStartIndex,
                    EndExclusive = objectStartIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                });
            }
        }

        private static bool FindLastUsedIndex(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int dataStartIndexInclusive, 
            int dataEndIndexExclusive, 
            out int lastUsedIndex)
        {
            bool success = freeIndexRangesListHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> asUnsafeArray);
            Assert.IsTrue(success);

            int evaluatedIndex = dataEndIndexExclusive - 1;
            for (int i = asUnsafeArray.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = asUnsafeArray[i];

                if (evaluatedIndex < dataStartIndexInclusive)
                {
                    // If we're past the start index, we haven't found any used index
                    lastUsedIndex = -1;
                    return false;
                }
                else if (ObjectManagerUtilities.RangesOverlap(evaluatedIndex, evaluatedIndex + 1, tmpRange.StartInclusive, tmpRange.EndExclusive))
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

        private static bool FindFreeRange(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int objectIndexesSize,
            out IndexRangeElement freeIndexRange,
            out int indexOfFreeRange)
        {
            bool success = freeIndexRangesListHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> asUnsafeArray);
            Assert.IsTrue(success);

            for (int i = 0; i < asUnsafeArray.Length; i++)
            {
                IndexRangeElement indexRange = asUnsafeArray[i];
                if (indexRange.EndExclusive - indexRange.StartInclusive >= objectIndexesSize)
                {
                    indexOfFreeRange = i;
                    freeIndexRange = indexRange;
                    return true;
                }
            }

            indexOfFreeRange = -1;
            freeIndexRange = default;
            return false;
        }

        private static void GetExpandedFreeRange(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int previousEndIndexExclusive, 
            int newEndIndexExclusive,
            out IndexRangeElement freeRange, 
            out int indexOfFreeRange)
        {
            bool success = freeIndexRangesListHandle.TryGetLength(ref bytesBuffer, out int listLength);
            Assert.IsTrue(success);

            // Add new free range for the expanded capacity
            if (listLength > 0)
            {
                indexOfFreeRange = listLength - 1;
                success = freeIndexRangesListHandle.TryGetElementAt(ref bytesBuffer, indexOfFreeRange, out freeRange);
                Assert.IsTrue(success);

                if (freeRange.EndExclusive == previousEndIndexExclusive)
                {
                    // Expand the last range
                    freeRange.EndExclusive = newEndIndexExclusive;
                    return;
                }
            }

            // Create a new range
            indexOfFreeRange = -1;
            freeRange = new IndexRangeElement
            {
                StartInclusive = previousEndIndexExclusive,
                EndExclusive = newEndIndexExclusive,
            };
        }
        private static void ShiftFreeRanges(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int indexShift)
        {
            bool success = freeIndexRangesListHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> asUnsafeArray);
            Assert.IsTrue(success);

            for (int i = 0; i < asUnsafeArray.Length; i++)
            {
                IndexRangeElement freeRange = asUnsafeArray[i];
                freeRange.StartInclusive += indexShift;
                freeRange.EndExclusive += indexShift;
                asUnsafeArray[i] = freeRange;
            }
        }

        private static void ShiftMetadataByteIndexes(
            byte* elementDataBufferPtr, 
            int indexShift, 
            int metadatasEndIndexExclusive)
        {
            for (int i = ByteIndex_MetadatasStartIndex; i < metadatasEndIndexExclusive; i += UnsafeUtility.SizeOf<VirtualObjectMetadata>())
            {
                ByteArrayUtilities.ReadValue(elementDataBufferPtr, i, out VirtualObjectMetadata metadata);
                metadata.ByteIndex += indexShift;
                ByteArrayUtilities.WriteValue(elementDataBufferPtr, i, metadata);
            }
        }

        private static void ConsumeFromFreeRanges(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            IndexRangeElement freeRange,
            int freeRangeIndex,
            int objectSize,
            out int consumedStartIndex)
        {
            ObjectManagerUtilities.ConsumeFreeRange(freeRange, objectSize, out bool isFullyConsumed, out consumedStartIndex);
            if (isFullyConsumed)
            {
                if (freeRangeIndex >= 0) // If the range was already stored, remove it
                {
                    freeIndexRangesListHandle.TryRemoveAt(ref bytesBuffer, freeRangeIndex);
                }
            }
            else
            {
                if (freeRangeIndex >= 0) // If the range was already stored, overwrite it
                {
                    freeIndexRangesListHandle.TrySetElementAt(ref bytesBuffer, freeRangeIndex, freeRange);
                }
                else // If the range wasn't stored, add it
                {
                    freeIndexRangesListHandle.TryAdd(ref bytesBuffer, freeRange);
                }
            }
        }

        private static void ClearRangesPastEndIndex(
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

        public static class Unsafe
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
