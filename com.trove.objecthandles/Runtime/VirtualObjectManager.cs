using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

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

        private const float ObjectsCapacityGrowFactor = 2f;

        private static int ByteIndex_ObjectMetadataCapacity = 0;
        private static int ByteIndex_ObjectMetadataCount = ByteIndex_ObjectMetadataCapacity + UnsafeUtility.SizeOf<int>();
        private static int ByteIndex_MetadataFreeRangesHandle = ByteIndex_ObjectMetadataCount + UnsafeUtility.SizeOf<int>();
        private static int ByteIndex_ObjectDataFreeRangesHandle = ByteIndex_MetadataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualObjectHandleRO<VirtualList<IndexRangeElement>>>();
        private static int ByteIndex_MetadatasStartIndex = ByteIndex_ObjectDataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualObjectHandleRO<VirtualList<IndexRangeElement>>>();

        public static void Initialize(
            ref DynamicBuffer<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref DynamicBuffer<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            int objectsCapacity,
            int objectDataBytesCapacity)
        {
            dataFreeIndexRangesBuffer.Clear();
            metaDataFreeIndexRangesBuffer.Clear();
            elementsByteBuffer.Clear();

            byte* byteArrayPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            int bufferSize = ByteIndex_MetadatasStartIndex + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectsCapacity) + objectDataBytesCapacity;
            elementsByteBuffer.Resize(bufferSize, NativeArrayOptions.ClearMemory);

            // Write element buffer internal data
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            SetObjectMetadatasCapacity(bufferPtr, objectsCapacity);
            SetObjectMetadatasCount(bufferPtr, 0);

            GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
            IndexRangeElement dataFreeRanges = new IndexRangeElement
            {
                StartInclusive = objectDatasStartIndex,
                EndExclusive = elementsByteBuffer.Length,
            };
            IndexRangeElement metadataFreeRanges = new IndexRangeElement
            {
                StartInclusive = ByteIndex_MetadatasStartIndex,
                EndExclusive = objectDatasStartIndex,
            };

            // TODO: allocate virtual lists for free ranges

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


            dataFreeIndexRangesBuffer.Add(dataFreeRanges);
            metaDataFreeIndexRangesBuffer.Add(metadataFreeRanges);
        }

        public static VirtualObjectHandle<T> CreateObject<T>(
            ref DynamicBuffer<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref DynamicBuffer<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            T objectValue)
            where T : unmanaged
        {
            int objectSize = UnsafeUtility.SizeOf<T>();
            VirtualObjectHandle<T> returnHandle = CreateObject<T>(
                ref dataFreeIndexRangesBuffer,
                ref metaDataFreeIndexRangesBuffer,
                ref elementsByteBuffer,
                objectSize,
                out byte* valueDestinationPtr);
            UnsafeUtility.CopyStructureToPtr(ref objectValue, valueDestinationPtr);
            return returnHandle;
        }

        public static VirtualObjectHandle<T> CreateObjectFromWriter<T>(
            ref DynamicBuffer<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref DynamicBuffer<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            T objectByteWriter)
            where T : unmanaged, IObjectByteWriter
        {
            int objectSize = objectByteWriter.GetByteSize();
            VirtualObjectHandle<T> returnHandle = CreateObject<T>(
                ref dataFreeIndexRangesBuffer,
                ref metaDataFreeIndexRangesBuffer,
                ref elementsByteBuffer,
                objectSize,
                out byte* valueDestinationPtr);
            objectByteWriter.Write(valueDestinationPtr);
            return returnHandle;
        }

        public static VirtualObjectHandle<T> CreateObject<T>(
            ref DynamicBuffer<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref DynamicBuffer<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            int objectSize,
            out byte* valueDestinationPtr)
            where T : unmanaged
        {
            byte* bufferPtr;

            // Metadatas
            int metadataIndex;
            {
                if (!ObjectManagerUtilities.FindFreeIndexRange(ref metaDataFreeIndexRangesBuffer, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
                {
                    bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();

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
                    ShiftFreeRanges(ref dataFreeIndexRangesBuffer, metadatasCapacityDiffInBytes);
                    ShiftMetadataByteIndexes(bufferPtr, metadatasCapacityDiffInBytes, newObjectDatasStartIndex);

                    ObjectManagerUtilities.GetExpandedFreeRange(ref metaDataFreeIndexRangesBuffer, prevObjectDatasStartIndex, newObjectDatasStartIndex,
                        out freeIndexRange, out indexOfFreeRange);
                }

                ObjectManagerUtilities.ConsumeFreeRange(freeIndexRange, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out bool isFullyConsumed, out int consumedStartIndex);
                if (isFullyConsumed)
                {
                    if (indexOfFreeRange >= 0) // If the range was already stored, remove it
                    {
                        metaDataFreeIndexRangesBuffer.RemoveAt(indexOfFreeRange);
                    }
                }
                else
                {
                    if (indexOfFreeRange >= 0) // If the range was already stored, overwrite it
                    {
                        metaDataFreeIndexRangesBuffer[indexOfFreeRange] = freeIndexRange;
                    }
                    else // If the range wasn't stored, add it
                    {
                        metaDataFreeIndexRangesBuffer.Add(freeIndexRange);
                    }
                }

                metadataIndex = consumedStartIndex;
            }

            // Datas
            int dataStartIndex;
            {
                if (!ObjectManagerUtilities.FindFreeIndexRange(ref dataFreeIndexRangesBuffer, objectSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
                {
                    bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();

                    // Increase buffer capacity for expanded object data
                    GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
                    int prevDatasByteCapacity = elementsByteBuffer.Length - objectDatasStartIndex;
                    int newDatasByteCapacity = (int)math.ceil(prevDatasByteCapacity * ObjectsCapacityGrowFactor);
                    int newLength = (int)math.ceil(elementsByteBuffer.Length + (newDatasByteCapacity - prevDatasByteCapacity));
                    elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                    ObjectManagerUtilities.GetExpandedFreeRange(ref dataFreeIndexRangesBuffer, objectDatasStartIndex, elementsByteBuffer.Length,
                        out freeIndexRange, out indexOfFreeRange);
                }

                ObjectManagerUtilities.ConsumeFreeRange(freeIndexRange, objectSize, out bool isFullyConsumed, out int consumedStartIndex);
                if (isFullyConsumed)
                {
                    if (indexOfFreeRange >= 0) // If the range was already stored, remove it
                    {
                        dataFreeIndexRangesBuffer.RemoveAt(indexOfFreeRange);
                    }
                }
                else
                {
                    if (indexOfFreeRange >= 0) // If the range was already stored, overwrite it
                    {
                        dataFreeIndexRangesBuffer[indexOfFreeRange] = freeIndexRange;
                    }
                    else // If the range wasn't stored, add it
                    {
                        dataFreeIndexRangesBuffer.Add(freeIndexRange);
                    }
                }

                dataStartIndex = consumedStartIndex;
            }

            // Update metadata
            bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            ReadValue(bufferPtr, metadataIndex, out VirtualObjectMetadata objectMetadata);
            objectMetadata.Version++;
            objectMetadata.Size = objectSize;
            objectMetadata.ByteIndex = dataStartIndex;
            WriteValue(bufferPtr, metadataIndex, objectMetadata);

            // Write object
            valueDestinationPtr = bufferPtr + (long)dataStartIndex;

            return new VirtualObjectHandle<T>(new VirtualObjectHandle(objectMetadata.ByteIndex, objectMetadata.Version));
        }

        public static void FreeObject<T>(
            ref DynamicBuffer<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref DynamicBuffer<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandleRO<T> objectHandle)
            where T : unmanaged
        {
            FreeObject(
                ref dataFreeIndexRangesBuffer,
                ref metaDataFreeIndexRangesBuffer,
                ref elementsByteBuffer,
                new VirtualObjectHandle(objectHandle.MetadataByteIndex, objectHandle.Version));
        }

         public static void FreeObject(
            ref DynamicBuffer<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref DynamicBuffer<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
            bool metadataIndexValid = objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>() <= endIndexOfMetadatasExclusive;
            if (metadataIndexValid)
            {
                ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
                    // Update metadata
                    objectMetadata.Version++;
                    objectMetadata.Size = 0;
                    objectMetadata.ByteIndex = -1;
                    WriteValue(bufferPtr, objectHandle.MetadataByteIndex, objectMetadata);

                    // Free metadata
                    {
                        ObjectManagerUtilities.EvaluateRangeFreeing(ref metaDataFreeIndexRangesBuffer, objectHandle.MetadataByteIndex, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out RangeFreeingType rangeFreeingType, out int indexMatch);
                        switch (rangeFreeingType)
                        {
                            case RangeFreeingType.MergeFirst:
                                {
                                    IndexRangeElement rangeElement = dataFreeIndexRangesBuffer[indexMatch];
                                    rangeElement.StartInclusive -= UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                                    dataFreeIndexRangesBuffer[indexMatch] = rangeElement;
                                    break;
                                }
                            case RangeFreeingType.MergeLast:
                                {
                                    IndexRangeElement rangeElement = dataFreeIndexRangesBuffer[indexMatch];
                                    rangeElement.EndExclusive += UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                                    dataFreeIndexRangesBuffer[indexMatch] = rangeElement;
                                    break;
                                }
                            case RangeFreeingType.Insert:
                                {
                                    dataFreeIndexRangesBuffer.Insert(indexMatch, new IndexRangeElement
                                    {
                                        StartInclusive = objectHandle.MetadataByteIndex,
                                        EndExclusive = objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                                    });
                                    break;
                                }
                            case RangeFreeingType.Add:
                                {
                                    dataFreeIndexRangesBuffer.Add(new IndexRangeElement
                                    {
                                        StartInclusive = objectHandle.MetadataByteIndex,
                                        EndExclusive = objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                                    });
                                    break;
                                }
                        }
                    }

                    // Free data
                    {
                        ObjectManagerUtilities.EvaluateRangeFreeing(ref dataFreeIndexRangesBuffer, objectMetadata.ByteIndex, objectMetadata.Size, out RangeFreeingType rangeFreeingType, out int indexMatch);
                        switch (rangeFreeingType)
                        {
                            case RangeFreeingType.MergeFirst:
                                {
                                    IndexRangeElement rangeElement = dataFreeIndexRangesBuffer[indexMatch];
                                    rangeElement.StartInclusive -= objectMetadata.Size;
                                    dataFreeIndexRangesBuffer[indexMatch] = rangeElement;
                                    break;
                                }
                            case RangeFreeingType.MergeLast:
                                {
                                    IndexRangeElement rangeElement = dataFreeIndexRangesBuffer[indexMatch];
                                    rangeElement.EndExclusive += objectMetadata.Size;
                                    dataFreeIndexRangesBuffer[indexMatch] = rangeElement;
                                    break;
                                }
                            case RangeFreeingType.Insert:
                                {
                                    dataFreeIndexRangesBuffer.Insert(indexMatch, new IndexRangeElement
                                    {
                                        StartInclusive = objectMetadata.ByteIndex,
                                        EndExclusive = objectMetadata.ByteIndex + objectMetadata.Size,
                                    });
                                    break;
                                }
                            case RangeFreeingType.Add:
                                {
                                    dataFreeIndexRangesBuffer.Add(new IndexRangeElement
                                    {
                                        StartInclusive = objectMetadata.ByteIndex,
                                        EndExclusive = objectMetadata.ByteIndex + objectMetadata.Size,
                                    });
                                    break;
                                }
                        }
                    }
                }
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
                ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
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
                ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
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
                ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
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
            ref DynamicBuffer<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref DynamicBuffer<IndexRangeElement> metadataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            int minMetadatasCapacity,
            int minDataBytesCapacity)
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            int initialBufferLength = elementsByteBuffer.Length;
            GetObjectDatasStartIndex(bufferPtr, out int prevEndIndexOfMetadatasExclusive);

            // Metadatas
            int newSizeMetaDataBytes;
            {
                ObjectManagerUtilities.FindLastUsedIndex(ref metadataFreeIndexRangesBuffer, ByteIndex_MetadatasStartIndex, prevEndIndexOfMetadatasExclusive, out int lastUsedIndex);
                newSizeMetaDataBytes = math.max(0, math.max(minMetadatasCapacity * UnsafeUtility.SizeOf<VirtualObjectMetadata>(), (lastUsedIndex - ByteIndex_MetadatasStartIndex) + 1));
                int newEndIndexOfMetadatasExclusive = ByteIndex_MetadatasStartIndex + newSizeMetaDataBytes;

                // Clear ranges past new length
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

                // Shift data back
                int sizeDatas = elementsByteBuffer.Length - prevEndIndexOfMetadatasExclusive;
                int metadatasCapacityDiffInBytes = newEndIndexOfMetadatasExclusive - prevEndIndexOfMetadatasExclusive;
                byte* destPtr = bufferPtr + (long)newEndIndexOfMetadatasExclusive;
                byte* startPtr = bufferPtr + (long)prevEndIndexOfMetadatasExclusive;
                UnsafeUtility.MemCpy(destPtr, startPtr, sizeDatas);
                ShiftFreeRanges(ref dataFreeIndexRangesBuffer, metadatasCapacityDiffInBytes);
                ShiftMetadataByteIndexes(bufferPtr, metadatasCapacityDiffInBytes, newEndIndexOfMetadatasExclusive);
            }

            // Datas
            int newSizeDataBytes;
            {
                ObjectManagerUtilities.FindLastUsedIndex(ref dataFreeIndexRangesBuffer, prevEndIndexOfMetadatasExclusive, initialBufferLength, out int lastUsedIndex);
                newSizeDataBytes = math.max(0, math.max(minDataBytesCapacity, (lastUsedIndex - prevEndIndexOfMetadatasExclusive) + 1));
                int newEndOfDatasExclusive = ByteIndex_MetadatasStartIndex + newSizeMetaDataBytes + newSizeDataBytes;

                // Clear ranges past new length
                for (int i = dataFreeIndexRangesBuffer.Length - 1; i >= 0; i--)
                {
                    IndexRangeElement tmpRange = dataFreeIndexRangesBuffer[i];

                    if (tmpRange.StartInclusive >= newEndOfDatasExclusive)
                    {
                        // Remove
                        dataFreeIndexRangesBuffer.RemoveAt(i);
                    }
                    else if (tmpRange.EndExclusive > newEndOfDatasExclusive)
                    {
                        // Trim
                        tmpRange.EndExclusive = newEndOfDatasExclusive;
                        dataFreeIndexRangesBuffer[i] = tmpRange;
                        break;
                    }
                }
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

        private static void ShiftFreeRanges<T>(ref T freeIndexRangesBuffer, int indexShift)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            for (int i = 0; i < freeIndexRangesBuffer.Length; i++)
            {
                IndexRangeElement freeRange = freeIndexRangesBuffer[i];
                freeRange.StartInclusive += indexShift;
                freeRange.EndExclusive += indexShift;
                freeIndexRangesBuffer[i] = freeRange;
            }
        }

        private static void ShiftMetadataByteIndexes(byte* elementDataBufferPtr, int indexShift, int metadatasEndIndexExclusive)
        {
            for (int i = ByteIndex_MetadatasStartIndex; i < metadatasEndIndexExclusive; i += UnsafeUtility.SizeOf<VirtualObjectMetadata>())
            {
                ReadValue(elementDataBufferPtr, i, out VirtualObjectMetadata metadata);
                metadata.ByteIndex += indexShift;
                WriteValue(elementDataBufferPtr, i, metadata);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadValue<T>(byte* byteArrayPtr, int byteIndex, out T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.CopyPtrToStructure(startPtr, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteValue<T>(byte* byteArrayPtr, int byteIndex, T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.AsRef<T>(startPtr) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteValue<T>(byte* byteArrayPtr, ref int byteIndex, T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.AsRef<T>(startPtr) = value;
            byteIndex += UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteValue(byte* byteArrayPtr, int byteIndex, byte* value, int valueSize)
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.MemCpy(startPtr, value, valueSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectMetadatasCapacity(byte* byteArrayPtr, out int value)
        {
            ReadValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCapacity, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectMetadatasCount(byte* byteArrayPtr, out int value)
        {
            ReadValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCount, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // TODO: ObjectDatasStartIndex could be cached as internal buffer data and updated whenever there's a resize of metadatas
        private static void GetObjectDatasStartIndex(byte* byteArrayPtr, out int value)
        {
            GetObjectMetadatasCapacity(byteArrayPtr, out int objectMetadatasCapacity);
            value = ByteIndex_MetadatasStartIndex + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectMetadatasCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetObjectMetadatasCapacity(byte* byteArrayPtr, int value)
        {
            WriteValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCapacity, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetObjectMetadatasCount(byte* byteArrayPtr, int value)
        {
            WriteValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCount, value);
        }
    }
}