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
    public struct VirtualObjectHandleRO<T> where T : unmanaged
    {
        internal readonly int MetadataByteIndex;
        internal readonly int Version;

        internal VirtualObjectHandleRO(int index, int version)
        {
            MetadataByteIndex = index;
            Version = version;
        }

        internal VirtualObjectHandleRO(VirtualObjectHandle handle)
        {
            MetadataByteIndex = handle.MetadataByteIndex;
            Version = handle.Version;
        }

        public static implicit operator VirtualObjectHandleRO<T>(VirtualObjectHandle<T> o) => new VirtualObjectHandleRO<T>(o.MetadataByteIndex, o.Version);
    }

    public struct VirtualObjectHandle<T> where T : unmanaged
    {
        internal readonly int MetadataByteIndex;
        internal readonly int Version;

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

        internal VirtualObjectHandle(int metadataIndex, int version)
        {
            MetadataByteIndex = metadataIndex;
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
        private static int ByteIndex_ObjectDataFreeRangesHandle = ByteIndex_MetadataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualObjectHandleRO<VirtualList<IndexRangeElement>>>();
        private static int ByteIndex_MetadatasStartIndex = ByteIndex_ObjectDataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualObjectHandleRO<VirtualList<IndexRangeElement>>>();

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
                if (!FindFreeIndexRange(
                        metadataRangesHandle,
                        ref elementsByteBuffer, 
                        UnsafeUtility.SizeOf<VirtualObjectMetadata>(), 
                        out IndexRangeElement freeIndexRange, 
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
                        out freeIndexRange, 
                        out indexOfFreeRange);
                }

                ConsumeFromFreeRanges(
                    metadataRangesHandle,
                    ref elementsByteBuffer,
                    freeIndexRange,
                    indexOfFreeRange,
                    UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                    out metadataIndex);
            }

            bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();

            // Datas
            int dataStartIndex;
            {
                if (!FindFreeIndexRange(
                        dataRangesHandle,
                        ref elementsByteBuffer,
                        objectSize, 
                        out IndexRangeElement freeIndexRange, 
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
                        out freeIndexRange, 
                        out indexOfFreeRange);
                }

                ConsumeFromFreeRanges(
                    dataRangesHandle,
                    ref elementsByteBuffer,
                    freeIndexRange,
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

        public static void FreeObject<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandleRO<T> objectHandle)
            where T : unmanaged
        {
            FreeObject(
                ref elementsByteBuffer,
                new VirtualObjectHandle(objectHandle.MetadataByteIndex, objectHandle.Version));
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
                    // Update metadata
                    objectMetadata.Version++;
                    objectMetadata.Size = 0;
                    objectMetadata.ByteIndex = -1;
                    ByteArrayUtilities.WriteValue(bufferPtr, objectHandle.MetadataByteIndex, objectMetadata);

                    // Free metadata
                    {
                        FreeRangeForObject(
                            metadataRangesHandle,
                            ref elementsByteBuffer, 
                            objectHandle.MetadataByteIndex, 
                            UnsafeUtility.SizeOf<VirtualObjectMetadata>(), 
                            out RangeFreeingType rangeFreeingType, 
                            out int indexMatch);
                    }

                    // Free data
                    {
                        FreeRangeForObject(
                            dataRangesHandle,
                            ref elementsByteBuffer,
                            objectMetadata.ByteIndex, 
                            objectMetadata.Size, 
                            out RangeFreeingType rangeFreeingType, 
                            out int indexMatch);
                    }
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
                // - find a new place to allocate data (check if we can simply expand current range)
                int newDataByteIndex = ;
                bool dataByteIndexChanged = ;
                bool newSizeIsSmaller = newSize < oldSize;

                if (dataByteIndexChanged)
                {
                    // Copy data over to new location
                    byte* oldDataPtr = bufferPtr + (long)objectMetadataRef.ByteIndex;
                    byte* newDataPtr = bufferPtr + (long)newDataByteIndex;
                    UnsafeUtility.MemCpy(newDataPtr, oldDataPtr, oldSize);

                    // Make metadata byteindex point to new location
                    objectMetadataRef.ByteIndex = newDataByteIndex;

                    // - free old memory
                    k
                }
                else if (newSizeIsSmaller)
                {
                    // - free superfluous memory
                    g
                }

                // Update metadata size
                objectMetadataRef.Size = newSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObjectValue<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandleRO<T> objectHandle,
            out T value)
            where T : unmanaged
        {
            return TryGetObjectValue(
                ref elementsByteBuffer,
                new VirtualObjectHandle<T>(new VirtualObjectHandle(objectHandle.MetadataByteIndex, objectHandle.Version)),
                out value);
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
            VirtualObjectHandleRO<T> objectHandle,
            out byte* valuePtr)
            where T : unmanaged
        {
            return TryGetObjectValuePtr(
                ref elementsByteBuffer,
                new VirtualObjectHandle<T>(new VirtualObjectHandle(objectHandle.MetadataByteIndex, objectHandle.Version)),
                out valuePtr);
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
            VirtualObjectHandleRO<T> objectHandle)
            where T : unmanaged
        {
            return Exists(
                ref elementsByteBuffer,
                new VirtualObjectHandle<T>(new VirtualObjectHandle(objectHandle.MetadataByteIndex, objectHandle.Version)));
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

        private static void FreeRangeForObject(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int objectStartIndex, 
            int objectIndexesSize,
            out RangeFreeingType rangeFreeingType, out int indexMatch)
        {
            rangeFreeingType = RangeFreeingType.Add;
            indexMatch = -1;

            bool success = freeIndexRangesListHandle.TryAsUnsafeListRO(ref bytesBuffer, out UnsafeList<IndexRangeElement>.ReadOnly asUnsafeListRO);
            Assert.IsTrue(success);

            for (int i = 0; i < asUnsafeListRO.Length; i++)
            {
                IndexRangeElement tmpRange = asUnsafeListRO.ElementAt(i);

                // Assert no ranges overlap
                Assert.IsFalse(ObjectManagerUtilities.RangesOverlap(objectStartIndex, (objectStartIndex + objectIndexesSize), tmpRange.StartInclusive, tmpRange.EndExclusive));

                if (tmpRange.StartInclusive == objectStartIndex + objectIndexesSize)
                {
                    rangeFreeingType = RangeFreeingType.MergeFirst;
                    indexMatch = i;
                    break;
                }
                else if (tmpRange.EndExclusive == objectStartIndex)
                {
                    rangeFreeingType = RangeFreeingType.MergeLast;
                    indexMatch = i;
                    break;
                }
                else if (tmpRange.StartInclusive > objectStartIndex)
                {
                    rangeFreeingType = RangeFreeingType.Insert;
                    indexMatch = i;
                    break;
                }
            }


            switch (rangeFreeingType)
            {
                case RangeFreeingType.MergeFirst:
                    {
                        ref IndexRangeElement rangeElement = ref freeIndexRangesListHandle.TryGetRefElementAtUnsafe(ref bytesBuffer, indexMatch, out success);
                        rangeElement.StartInclusive -= UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                        break;
                    }
                case RangeFreeingType.MergeLast:
                    {
                        ref IndexRangeElement rangeElement = ref freeIndexRangesListHandle.TryGetRefElementAtUnsafe(ref bytesBuffer, indexMatch, out success);
                        rangeElement.EndExclusive += UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                        break;
                    }
                case RangeFreeingType.Insert:
                    {
                        freeIndexRangesListHandle.TryInsertAt(ref bytesBuffer, indexMatch, new IndexRangeElement
                        {
                            StartInclusive = objectStartIndex,
                            EndExclusive = objectStartIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                        });
                        break;
                    }
                case RangeFreeingType.Add:
                    {
                        freeIndexRangesListHandle.TryAdd(ref bytesBuffer, new IndexRangeElement
                        {
                            StartInclusive = objectStartIndex,
                            EndExclusive = objectStartIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                        });
                        break;
                    }
            }
        }

        private static bool FindLastUsedIndex(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int dataStartIndexInclusive, 
            int dataEndIndexExclusive, 
            out int lastUsedIndex)
        {
            bool success = freeIndexRangesListHandle.TryAsUnsafeListRO(ref bytesBuffer, out UnsafeList<IndexRangeElement>.ReadOnly asUnsafeListRO);
            Assert.IsTrue(success);

            int evaluatedIndex = dataEndIndexExclusive - 1;
            for (int i = asUnsafeListRO.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = asUnsafeListRO.ElementAt(i);

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

        private static bool FindFreeIndexRange(
            VirtualListHandle<IndexRangeElement> freeIndexRangesListHandle,
            ref DynamicBuffer<byte> bytesBuffer,
            int objectIndexesSize,
            out IndexRangeElement freeIndexRange,
            out int indexOfFreeRange)
        {
            bool success = freeIndexRangesListHandle.TryAsUnsafeListRO(ref bytesBuffer, out UnsafeList<IndexRangeElement>.ReadOnly asUnsafeListRO);
            Assert.IsTrue(success);

            for (int i = 0; i < asUnsafeListRO.Length; i++)
            {
                IndexRangeElement indexRange = asUnsafeListRO.ElementAt(i);
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
            out IndexRangeElement freeIndexRange, 
            out int indexOfFreeRange)
        {
            bool success = freeIndexRangesListHandle.TryGetLength(ref bytesBuffer, out int listLength);
            Assert.IsTrue(success);

            // Add new free index range for the expanded capacity
            if (listLength > 0)
            {
                indexOfFreeRange = listLength - 1;
                success = freeIndexRangesListHandle.TryGetElementAt(ref bytesBuffer, indexOfFreeRange, out freeIndexRange);
                Assert.IsTrue(success);

                if (freeIndexRange.EndExclusive == previousEndIndexExclusive)
                {
                    // Expand the last range
                    freeIndexRange.EndExclusive = newEndIndexExclusive;
                    return;
                }
            }

            // Create a new range
            indexOfFreeRange = -1;
            freeIndexRange = new IndexRangeElement
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
            bool success = freeIndexRangesListHandle.TryAsUnsafeList(ref bytesBuffer, out UnsafeList<IndexRangeElement> asUnsafeList);
            Assert.IsTrue(success);

            for (int i = 0; i < asUnsafeList.Length; i++)
            {
                IndexRangeElement freeRange = asUnsafeList[i];
                freeRange.StartInclusive += indexShift;
                freeRange.EndExclusive += indexShift;
                asUnsafeList[i] = freeRange;
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
            bool success = freeIndexRangesListHandle.TryGetLength(ref bytesBuffer, out int length);

            for (int i = metadataFreeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = metadataFreeIndexRangesBuffer[i];

                if (tmpRange.StartInclusive >= newEndIndexOfMetadatasExclusive)
                {
                    // Remove
                    metadataFreeIndexRangesBuffer.RemoveAt(i);
                }
                else if (tmpRange.EndExclusive > newEndIndexOfMetadatasExclusive)
                {
                    // Trim
                    tmpRange.EndExclusive = newEndIndexOfMetadatasExclusive;
                    metadataFreeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
            }
        }

    }
}
