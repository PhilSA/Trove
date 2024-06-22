using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using static Trove.ObjectHandles.ValueObjectManager;

namespace Trove.ObjectHandles
{
    public struct ObjectHandle
    {
        public readonly int Index;
        public readonly int Version;

        public ObjectHandle(int index, int version)
        {
            Index = index;
            Version = version;
        }
    }

    public unsafe struct VirtualObjectHandle
    {
        public readonly int MetadataByteIndex;
        public readonly int Version;

        public VirtualObjectHandle(int metadataIndex, int version)
        {
            MetadataByteIndex = metadataIndex;
            Version = version;
        }
    }

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

    public unsafe static class ValueObjectManager
    {

        public struct VirtualObjectMetadata
        {
            public int ByteIndex;
            public int Version;
            public int Size;
        }

        private const float ObjectsCapacityGrowFactor = 2f;

        private const int ByteIndex_ObjectMetadataCapacity = 0;
        private const int ByteIndex_ObjectMetadataCount = ByteIndex_ObjectMetadataCapacity + sizeof(int);
        private const int ByteIndex_ObjectMetadatasStart = ByteIndex_ObjectMetadataCount + sizeof(int);

        public static void Initialize<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<ObjectData<T>> elementsBuffer,
            int initialElementsCapacity)
            where T : unmanaged
        {
            freeIndexRangesBuffer.Clear();
            elementsBuffer.Clear();

            freeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = 0,
                EndExclusive = initialElementsCapacity,
            });

            elementsBuffer.Resize(initialElementsCapacity, NativeArrayOptions.ClearMemory);
        }

        public static void Initialize(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            int objectsCapacity,
            int objectDataBytesCapacity)
        {
            dataFreeIndexRangesBuffer.Clear();
            metaDataFreeIndexRangesBuffer.Clear();
            elementsByteBuffer.Clear();

            int bufferSize = ByteIndex_ObjectMetadatasStart + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectsCapacity) + objectDataBytesCapacity;
            elementsByteBuffer.Resize(bufferSize, NativeArrayOptions.ClearMemory);

            // Write element buffer internal data
            byte* bufferPtr = elementsByteBuffer.GetUnsafePtr();
            SetObjectMetadatasCapacity(bufferPtr, objectsCapacity);
            SetObjectMetadatasCount(bufferPtr, 0);

            GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
            dataFreeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = objectDatasStartIndex,
                EndExclusive = elementsByteBuffer.Length,
            });
            metaDataFreeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = ByteIndex_ObjectMetadatasStart,
                EndExclusive = objectDatasStartIndex,
            });
        }

        public static void Initialize<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<ObjectData<T>> elementsBuffer,
            int initialElementsCapacity)
            where T : unmanaged
        {
            freeIndexRangesBuffer.Clear();
            elementsBuffer.Clear();

            freeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = 0,
                EndExclusive = initialElementsCapacity,
            });

            elementsBuffer.Resize(initialElementsCapacity, NativeArrayOptions.ClearMemory);
        }

        public static void Initialize(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            int objectsCapacity,
            int objectDataBytesCapacity)
        {
            dataFreeIndexRangesBuffer.Clear();
            metaDataFreeIndexRangesBuffer.Clear();
            elementsByteBuffer.Clear();

            int bufferSize = ByteIndex_ObjectMetadatasStart + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectsCapacity) + objectDataBytesCapacity;
            elementsByteBuffer.Resize(bufferSize, NativeArrayOptions.ClearMemory);

            // Write element buffer internal data
            byte* bufferPtr = elementsByteBuffer.Ptr;
            SetObjectMetadatasCapacity(bufferPtr, objectsCapacity);
            SetObjectMetadatasCount(bufferPtr, 0);

            GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
            dataFreeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = objectDatasStartIndex,
                EndExclusive = elementsByteBuffer.Length,
            });
            metaDataFreeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = ByteIndex_ObjectMetadatasStart,
                EndExclusive = objectDatasStartIndex,
            });
        }

        public static void Initialize<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            int initialElementsCapacity)
            where T : unmanaged
        {
            freeIndexRangesBuffer.Clear();
            elementsBuffer.Clear();

            freeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = 0,
                EndExclusive = initialElementsCapacity,
            });

            elementsBuffer.Resize(initialElementsCapacity, NativeArrayOptions.ClearMemory);
        }

        public static void Initialize(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            int objectsCapacity,
            int objectDataBytesCapacity)
        {
            dataFreeIndexRangesBuffer.Clear();
            metaDataFreeIndexRangesBuffer.Clear();
            elementsByteBuffer.Clear();

            int bufferSize = ByteIndex_ObjectMetadatasStart + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectsCapacity) + objectDataBytesCapacity;
            elementsByteBuffer.Resize(bufferSize, NativeArrayOptions.ClearMemory);

            // Write element buffer internal data
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            SetObjectMetadatasCapacity(bufferPtr, objectsCapacity);
            SetObjectMetadatasCount(bufferPtr, 0);

            GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
            dataFreeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = objectDatasStartIndex,
                EndExclusive = elementsByteBuffer.Length,
            });
            metaDataFreeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = ByteIndex_ObjectMetadatasStart,
                EndExclusive = objectDatasStartIndex,
            });
        }

        public static ObjectHandle CreateObject<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<ObjectData<T>> elementsBuffer,
            T objectValue)
            where T : unmanaged
        {
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, 1, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
                // Increase buffer capacity
                int prevLength = elementsBuffer.Length;
                int newLength = (int)math.ceil(elementsBuffer.Length * ObjectsCapacityGrowFactor);
                elementsBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                GetExpandedFreeRange(ref freeIndexRangesBuffer, prevLength, elementsBuffer.Length,
                    out freeIndexRange, out indexOfFreeRange);
            }

            ConsumeFreeRange(freeIndexRange, 1, out bool isFullyConsumed, out int consumedStartIndex);
            if (isFullyConsumed)
            {
                if (indexOfFreeRange >= 0) // If the range was already stored, remove it
                {
                    freeIndexRangesBuffer.RemoveAt(indexOfFreeRange);
                }
            }
            else
            {
                if (indexOfFreeRange >= 0) // If the range was already stored, overwrite it
                {
                    freeIndexRangesBuffer[indexOfFreeRange] = freeIndexRange;
                }
                else // If the range wasn't stored, add it
                {
                    freeIndexRangesBuffer.Add(freeIndexRange);
                }
            }

            // Write object and bump version
            ObjectData<T> value = elementsBuffer[consumedStartIndex];
            value.Version++;
            value.Value = objectValue;
            elementsBuffer[consumedStartIndex] = value;

            return new ObjectHandle(consumedStartIndex, value.Version);
        }

        public static VirtualObjectHandle CreateObject(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            byte* objectValuePtr,
            int objectSize)
        {
            byte* bufferPtr;

            // Metadatas
            int metadataIndex;
            {
                if (!FindFreeIndexRange(ref metaDataFreeIndexRangesBuffer, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
                {
                    bufferPtr = elementsByteBuffer.GetUnsafePtr();

                    // Increase buffer capacity for expanded metadatas
                    int prevElementsBufferLength = elementsByteBuffer.Length;
                    GetObjectDatasStartIndex(bufferPtr, out int prevObjectDatasStartIndex);
                    GetObjectMetadatasCapacity(bufferPtr, out int prevMetadatasCapacity);
                    int newMetadatasCapacity = (int)math.ceil(prevMetadatasCapacity * ObjectsCapacityGrowFactor);
                    SetObjectMetadatasCapacity(bufferPtr, newMetadatasCapacity);
                    int metadatasCapacityDiffInBytes = (newMetadatasCapacity - prevMetadatasCapacity) * UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                    int newLength = elementsByteBuffer.Length + metadatasCapacityDiffInBytes;
                    elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                    bufferPtr = elementsByteBuffer.GetUnsafePtr();
                    GetObjectDatasStartIndex(bufferPtr, out int newObjectDatasStartIndex);

                    // Move object data
                    byte* destPtr = bufferPtr + (long)newObjectDatasStartIndex;
                    byte* startPtr = bufferPtr + (long)prevMetadatasCapacity;
                    UnsafeUtility.MemCpy(destPtr, startPtr, (prevElementsBufferLength - prevObjectDatasStartIndex));
                    ShiftFreeRanges(ref dataFreeIndexRangesBuffer, metadatasCapacityDiffInBytes);
                    ShiftMetadataByteIndexes(bufferPtr, metadatasCapacityDiffInBytes, newObjectDatasStartIndex);

                    GetExpandedFreeRange(ref metaDataFreeIndexRangesBuffer, prevObjectDatasStartIndex, newObjectDatasStartIndex,
                        out freeIndexRange, out indexOfFreeRange);
                }

                ConsumeFreeRange(freeIndexRange, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out bool isFullyConsumed, out int consumedStartIndex);
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
                if (!FindFreeIndexRange(ref dataFreeIndexRangesBuffer, objectSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
                {
                    bufferPtr = elementsByteBuffer.GetUnsafePtr();

                    // Increase buffer capacity for expanded object data
                    GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
                    int prevDatasByteCapacity = elementsByteBuffer.Length - objectDatasStartIndex;
                    int newDatasByteCapacity = (int)math.ceil(prevDatasByteCapacity * ObjectsCapacityGrowFactor);
                    int newLength = (int)math.ceil(elementsByteBuffer.Length + (newDatasByteCapacity - prevDatasByteCapacity));
                    elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                    GetExpandedFreeRange(ref dataFreeIndexRangesBuffer, objectDatasStartIndex, elementsByteBuffer.Length,
                        out freeIndexRange, out indexOfFreeRange);
                }

                ConsumeFreeRange(freeIndexRange, objectSize, out bool isFullyConsumed, out int consumedStartIndex);
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
            bufferPtr = elementsByteBuffer.GetUnsafePtr();
            ReadValue(bufferPtr, metadataIndex, out VirtualObjectMetadata objectMetadata);
            objectMetadata.Version++;
            objectMetadata.Size = objectSize;
            objectMetadata.ByteIndex = dataStartIndex;
            WriteValue(bufferPtr, metadataIndex, objectMetadata);

            // Write object
            WriteValue(bufferPtr, dataStartIndex, objectValuePtr, objectSize);

            return new VirtualObjectHandle(objectMetadata.ByteIndex, objectMetadata.Version);
        }

        public static ObjectHandle CreateObject<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<ObjectData<T>> elementsBuffer,
            T objectValue)
            where T : unmanaged
        {
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, 1, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
                // Increase buffer capacity
                int prevLength = elementsBuffer.Length;
                int newLength = (int)math.ceil(elementsBuffer.Length * ObjectsCapacityGrowFactor);
                elementsBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                GetExpandedFreeRange(ref freeIndexRangesBuffer, prevLength, elementsBuffer.Length,
                    out freeIndexRange, out indexOfFreeRange);
            }

            ConsumeFreeRange(freeIndexRange, 1, out bool isFullyConsumed, out int consumedStartIndex);
            if (isFullyConsumed)
            {
                if (indexOfFreeRange >= 0) // If the range was already stored, remove it
                {
                    freeIndexRangesBuffer.RemoveAt(indexOfFreeRange);
                }
            }
            else
            {
                if (indexOfFreeRange >= 0) // If the range was already stored, overwrite it
                {
                    freeIndexRangesBuffer[indexOfFreeRange] = freeIndexRange;
                }
                else // If the range wasn't stored, add it
                {
                    freeIndexRangesBuffer.Add(freeIndexRange);
                }
            }

            // Write object and bump version
            ObjectData<T> value = elementsBuffer[consumedStartIndex];
            value.Version++;
            value.Value = objectValue;
            elementsBuffer[consumedStartIndex] = value;

            return new ObjectHandle(consumedStartIndex, value.Version);
        }

        public static VirtualObjectHandle CreateObject(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            byte* objectValuePtr,
            int objectSize)
        {
            byte* bufferPtr;

            // Metadatas
            int metadataIndex;
            {
                if (!FindFreeIndexRange(ref metaDataFreeIndexRangesBuffer, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
                {
                    bufferPtr = elementsByteBuffer.Ptr;

                    // Increase buffer capacity for expanded metadatas
                    int prevElementsBufferLength = elementsByteBuffer.Length;
                    GetObjectDatasStartIndex(bufferPtr, out int prevObjectDatasStartIndex);
                    GetObjectMetadatasCapacity(bufferPtr, out int prevMetadatasCapacity);
                    int newMetadatasCapacity = (int)math.ceil(prevMetadatasCapacity * ObjectsCapacityGrowFactor);
                    SetObjectMetadatasCapacity(bufferPtr, newMetadatasCapacity);
                    int metadatasCapacityDiffInBytes = (newMetadatasCapacity - prevMetadatasCapacity) * UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                    int newLength = elementsByteBuffer.Length + metadatasCapacityDiffInBytes;
                    elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                    bufferPtr = elementsByteBuffer.Ptr;
                    GetObjectDatasStartIndex(bufferPtr, out int newObjectDatasStartIndex);

                    // Move object data
                    byte* destPtr = bufferPtr + (long)newObjectDatasStartIndex;
                    byte* startPtr = bufferPtr + (long)prevMetadatasCapacity;
                    UnsafeUtility.MemCpy(destPtr, startPtr, (prevElementsBufferLength - prevObjectDatasStartIndex));
                    ShiftFreeRanges(ref dataFreeIndexRangesBuffer, metadatasCapacityDiffInBytes);
                    ShiftMetadataByteIndexes(bufferPtr, metadatasCapacityDiffInBytes, newObjectDatasStartIndex);

                    GetExpandedFreeRange(ref metaDataFreeIndexRangesBuffer, prevObjectDatasStartIndex, newObjectDatasStartIndex,
                        out freeIndexRange, out indexOfFreeRange);
                }

                ConsumeFreeRange(freeIndexRange, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out bool isFullyConsumed, out int consumedStartIndex);
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
                if (!FindFreeIndexRange(ref dataFreeIndexRangesBuffer, objectSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
                {
                    bufferPtr = elementsByteBuffer.Ptr;

                    // Increase buffer capacity for expanded object data
                    GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
                    int prevDatasByteCapacity = elementsByteBuffer.Length - objectDatasStartIndex;
                    int newDatasByteCapacity = (int)math.ceil(prevDatasByteCapacity * ObjectsCapacityGrowFactor);
                    int newLength = (int)math.ceil(elementsByteBuffer.Length + (newDatasByteCapacity - prevDatasByteCapacity));
                    elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                    GetExpandedFreeRange(ref dataFreeIndexRangesBuffer, objectDatasStartIndex, elementsByteBuffer.Length,
                        out freeIndexRange, out indexOfFreeRange);
                }

                ConsumeFreeRange(freeIndexRange, objectSize, out bool isFullyConsumed, out int consumedStartIndex);
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
            bufferPtr = elementsByteBuffer.Ptr;
            ReadValue(bufferPtr, metadataIndex, out VirtualObjectMetadata objectMetadata);
            objectMetadata.Version++;
            objectMetadata.Size = objectSize;
            objectMetadata.ByteIndex = dataStartIndex;
            WriteValue(bufferPtr, metadataIndex, objectMetadata);

            // Write object
            WriteValue(bufferPtr, dataStartIndex, objectValuePtr, objectSize);

            return new VirtualObjectHandle(objectMetadata.ByteIndex, objectMetadata.Version);
        }

        public static ObjectHandle CreateObject<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            T objectValue)
            where T : unmanaged
        {
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, 1, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
                // Increase buffer capacity
                int prevLength = elementsBuffer.Length;
                int newLength = (int)math.ceil(elementsBuffer.Length * ObjectsCapacityGrowFactor);
                elementsBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                GetExpandedFreeRange(ref freeIndexRangesBuffer, prevLength, elementsBuffer.Length,
                    out freeIndexRange, out indexOfFreeRange);
            }

            ConsumeFreeRange(freeIndexRange, 1, out bool isFullyConsumed, out int consumedStartIndex);
            if (isFullyConsumed)
            {
                if (indexOfFreeRange >= 0) // If the range was already stored, remove it
                {
                    freeIndexRangesBuffer.RemoveAt(indexOfFreeRange);
                }
            }
            else
            {
                if (indexOfFreeRange >= 0) // If the range was already stored, overwrite it
                {
                    freeIndexRangesBuffer[indexOfFreeRange] = freeIndexRange;
                }
                else // If the range wasn't stored, add it
                {
                    freeIndexRangesBuffer.Add(freeIndexRange);
                }
            }

            // Write object and bump version
            ObjectData<T> value = elementsBuffer[consumedStartIndex];
            value.Version++;
            value.Value = objectValue;
            elementsBuffer[consumedStartIndex] = value;

            return new ObjectHandle(consumedStartIndex, value.Version);
        }

        public static VirtualObjectHandle CreateObject(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            byte* objectValuePtr,
            int objectSize)
        {
            byte* bufferPtr;

            // Metadatas
            int metadataIndex;
            {
                if (!FindFreeIndexRange(ref metaDataFreeIndexRangesBuffer, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
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

                    GetExpandedFreeRange(ref metaDataFreeIndexRangesBuffer, prevObjectDatasStartIndex, newObjectDatasStartIndex,
                        out freeIndexRange, out indexOfFreeRange);
                }

                ConsumeFreeRange(freeIndexRange, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out bool isFullyConsumed, out int consumedStartIndex);
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
                if (!FindFreeIndexRange(ref dataFreeIndexRangesBuffer, objectSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
                {
                    bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();

                    // Increase buffer capacity for expanded object data
                    GetObjectDatasStartIndex(bufferPtr, out int objectDatasStartIndex);
                    int prevDatasByteCapacity = elementsByteBuffer.Length - objectDatasStartIndex;
                    int newDatasByteCapacity = (int)math.ceil(prevDatasByteCapacity * ObjectsCapacityGrowFactor);
                    int newLength = (int)math.ceil(elementsByteBuffer.Length + (newDatasByteCapacity - prevDatasByteCapacity));
                    elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                    GetExpandedFreeRange(ref dataFreeIndexRangesBuffer, objectDatasStartIndex, elementsByteBuffer.Length,
                        out freeIndexRange, out indexOfFreeRange);
                }

                ConsumeFreeRange(freeIndexRange, objectSize, out bool isFullyConsumed, out int consumedStartIndex);
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
            WriteValue(bufferPtr, dataStartIndex, objectValuePtr, objectSize);

            return new VirtualObjectHandle(objectMetadata.ByteIndex, objectMetadata.Version);
        }

        public static void FreeObject<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged
        {
            bool indexValid = objectHandle.Index < elementsBuffer.Length;
            if (indexValid)
            {
                ObjectData<T> existingElement = elementsBuffer[objectHandle.Index];
                if (existingElement.Version == objectHandle.Version)
                {
                    // Bump version and clear value
                    existingElement.Version++;
                    existingElement.Value = default;
                    elementsBuffer[objectHandle.Index] = existingElement;

                    EvaluateRangeFreeing(ref freeIndexRangesBuffer, objectHandle.Index, 1, out RangeFreeingType rangeFreeingType, out int indexMatch);
                    switch (rangeFreeingType)
                    {
                        case RangeFreeingType.MergeFirst:
                            {
                                IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                                rangeElement.StartInclusive -= 1;
                                freeIndexRangesBuffer[indexMatch] = rangeElement;
                                break;
                            }
                        case RangeFreeingType.MergeLast:
                            {
                                IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                                rangeElement.EndExclusive += 1;
                                freeIndexRangesBuffer[indexMatch] = rangeElement;
                                break;
                            }
                        case RangeFreeingType.Insert:
                            {
                                freeIndexRangesBuffer.InsertRange(indexMatch, 1);
                                freeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                                {
                                    StartInclusive = objectHandle.Index,
                                    EndExclusive = objectHandle.Index + 1,
                                };
                                break;
                            }
                        case RangeFreeingType.Add:
                            {
                                freeIndexRangesBuffer.Add(new IndexRangeElement
                                {
                                    StartInclusive = objectHandle.Index,
                                    EndExclusive = objectHandle.Index + 1,
                                });
                                break;
                            }
                    }
                }
            }
        }

        public static void FreeObject(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
            byte* bufferPtr = elementsByteBuffer.GetUnsafePtr();
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
                        EvaluateRangeFreeing(ref metaDataFreeIndexRangesBuffer, objectHandle.MetadataByteIndex, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out RangeFreeingType rangeFreeingType, out int indexMatch);
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
                                    dataFreeIndexRangesBuffer.InsertRange(indexMatch, UnsafeUtility.SizeOf<VirtualObjectMetadata>());
                                    dataFreeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                                    {
                                        StartInclusive = objectHandle.MetadataByteIndex,
                                        EndExclusive = objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                                    };
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
                        EvaluateRangeFreeing(ref dataFreeIndexRangesBuffer, objectMetadata.ByteIndex, objectMetadata.Size, out RangeFreeingType rangeFreeingType, out int indexMatch);
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
                                    dataFreeIndexRangesBuffer.InsertRange(indexMatch, objectMetadata.Size);
                                    dataFreeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                                    {
                                        StartInclusive = objectMetadata.ByteIndex,
                                        EndExclusive = objectMetadata.ByteIndex + objectMetadata.Size,
                                    };
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

        public static void FreeObject<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged
        {
            bool indexValid = objectHandle.Index < elementsBuffer.Length;
            if (indexValid)
            {
                ObjectData<T> existingElement = elementsBuffer[objectHandle.Index];
                if (existingElement.Version == objectHandle.Version)
                {
                    // Bump version and clear value
                    existingElement.Version++;
                    existingElement.Value = default;
                    elementsBuffer[objectHandle.Index] = existingElement;

                    EvaluateRangeFreeing(ref freeIndexRangesBuffer, objectHandle.Index, 1, out RangeFreeingType rangeFreeingType, out int indexMatch);
                    switch (rangeFreeingType)
                    {
                        case RangeFreeingType.MergeFirst:
                            {
                                IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                                rangeElement.StartInclusive -= 1;
                                freeIndexRangesBuffer[indexMatch] = rangeElement;
                                break;
                            }
                        case RangeFreeingType.MergeLast:
                            {
                                IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                                rangeElement.EndExclusive += 1;
                                freeIndexRangesBuffer[indexMatch] = rangeElement;
                                break;
                            }
                        case RangeFreeingType.Insert:
                            {
                                freeIndexRangesBuffer.InsertRange(indexMatch, 1);
                                freeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                                {
                                    StartInclusive = objectHandle.Index,
                                    EndExclusive = objectHandle.Index + 1,
                                };
                                break;
                            }
                        case RangeFreeingType.Add:
                            {
                                freeIndexRangesBuffer.Add(new IndexRangeElement
                                {
                                    StartInclusive = objectHandle.Index,
                                    EndExclusive = objectHandle.Index + 1,
                                });
                                break;
                            }
                    }
                }
            }
        }

        public static void FreeObject(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
            byte* bufferPtr = elementsByteBuffer.Ptr;
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
                        EvaluateRangeFreeing(ref metaDataFreeIndexRangesBuffer, objectHandle.MetadataByteIndex, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out RangeFreeingType rangeFreeingType, out int indexMatch);
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
                                    dataFreeIndexRangesBuffer.InsertRange(indexMatch, UnsafeUtility.SizeOf<VirtualObjectMetadata>());
                                    dataFreeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                                    {
                                        StartInclusive = objectHandle.MetadataByteIndex,
                                        EndExclusive = objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                                    };
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
                        EvaluateRangeFreeing(ref dataFreeIndexRangesBuffer, objectMetadata.ByteIndex, objectMetadata.Size, out RangeFreeingType rangeFreeingType, out int indexMatch);
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
                                    dataFreeIndexRangesBuffer.InsertRange(indexMatch, objectMetadata.Size);
                                    dataFreeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                                    {
                                        StartInclusive = objectMetadata.ByteIndex,
                                        EndExclusive = objectMetadata.ByteIndex + objectMetadata.Size,
                                    };
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

        public static void FreeObject<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged
        {
            bool indexValid = objectHandle.Index < elementsBuffer.Length;
            if (indexValid)
            {
                ObjectData<T> existingElement = elementsBuffer[objectHandle.Index];
                if (existingElement.Version == objectHandle.Version)
                {
                    // Bump version and clear value
                    existingElement.Version++;
                    existingElement.Value = default;
                    elementsBuffer[objectHandle.Index] = existingElement;

                    EvaluateRangeFreeing(ref freeIndexRangesBuffer, objectHandle.Index, 1, out RangeFreeingType rangeFreeingType, out int indexMatch);
                    switch (rangeFreeingType)
                    {
                        case RangeFreeingType.MergeFirst:
                            {
                                IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                                rangeElement.StartInclusive -= 1;
                                freeIndexRangesBuffer[indexMatch] = rangeElement;
                                break;
                            }
                        case RangeFreeingType.MergeLast:
                            {
                                IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                                rangeElement.EndExclusive += 1;
                                freeIndexRangesBuffer[indexMatch] = rangeElement;
                                break;
                            }
                        case RangeFreeingType.Insert:
                            {
                                freeIndexRangesBuffer.Insert(indexMatch, new IndexRangeElement
                                {
                                    StartInclusive = objectHandle.Index,
                                    EndExclusive = objectHandle.Index + 1,
                                });
                                break;
                            }
                        case RangeFreeingType.Add:
                            {
                                freeIndexRangesBuffer.Add(new IndexRangeElement
                                {
                                    StartInclusive = objectHandle.Index,
                                    EndExclusive = objectHandle.Index + 1,
                                });
                                break;
                            }
                    }
                }
            }
        }

        public static void FreeObject(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metaDataFreeIndexRangesBuffer,
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
                        EvaluateRangeFreeing(ref metaDataFreeIndexRangesBuffer, objectHandle.MetadataByteIndex, UnsafeUtility.SizeOf<VirtualObjectMetadata>(), out RangeFreeingType rangeFreeingType, out int indexMatch);
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
                                    dataFreeIndexRangesBuffer.InsertRange(indexMatch, UnsafeUtility.SizeOf<VirtualObjectMetadata>());
                                    dataFreeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                                    {
                                        StartInclusive = objectHandle.MetadataByteIndex,
                                        EndExclusive = objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                                    };
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
                        EvaluateRangeFreeing(ref dataFreeIndexRangesBuffer, objectMetadata.ByteIndex, objectMetadata.Size, out RangeFreeingType rangeFreeingType, out int indexMatch);
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
                                    dataFreeIndexRangesBuffer.InsertRange(indexMatch, objectMetadata.Size);
                                    dataFreeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                                    {
                                        StartInclusive = objectMetadata.ByteIndex,
                                        EndExclusive = objectMetadata.ByteIndex + objectMetadata.Size,
                                    };
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

        public static bool TryGetObjectValue<T>(
            ref NativeList<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                ObjectData<T> objectValue = elementsBuffer[objectHandle.Index];
                if (objectValue.Version == objectHandle.Version)
                {
                    value = objectValue.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref NativeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            byte* bufferPtr = elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectHandle>() <= endIndexOfMetadatasExclusive)
            {
                ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
                    byte* objPtr = bufferPtr + (long)objectMetadata.ByteIndex;
                    value = *(T*)objPtr;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref UnsafeList<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                ObjectData<T> objectValue = elementsBuffer[objectHandle.Index];
                if (objectValue.Version == objectHandle.Version)
                {
                    value = objectValue.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref UnsafeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            byte* bufferPtr = elementsByteBuffer.Ptr;
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectHandle>() <= endIndexOfMetadatasExclusive)
            {
                ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
                    byte* objPtr = bufferPtr + (long)objectMetadata.ByteIndex;
                    value = *(T*)objPtr;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                ObjectData<T> objectValue = elementsBuffer[objectHandle.Index];
                if (objectValue.Version == objectHandle.Version)
                {
                    value = objectValue.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectHandle>() <= endIndexOfMetadatasExclusive)
            {
                ReadValue(bufferPtr, objectHandle.MetadataByteIndex, out VirtualObjectMetadata objectMetadata);
                if (objectMetadata.Version == objectHandle.Version)
                {
                    byte* objPtr = bufferPtr + (long)objectMetadata.ByteIndex;
                    value = *(T*)objPtr;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref NativeList<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                ObjectData<T> objectValue = elementsBuffer[objectHandle.Index];
                if (objectValue.Version == objectHandle.Version)
                {
                    objectValue.Value = value;
                    elementsBuffer[objectHandle.Index] = objectValue;
                    return true;
                }
            }

            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref NativeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            byte* bufferPtr = elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectHandle>() <= endIndexOfMetadatasExclusive)
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

        public static bool TrySetObjectValue<T>(
            ref UnsafeList<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                ObjectData<T> objectValue = elementsBuffer[objectHandle.Index];
                if (objectValue.Version == objectHandle.Version)
                {
                    objectValue.Value = value;
                    elementsBuffer[objectHandle.Index] = objectValue;
                    return true;
                }
            }

            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref UnsafeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            byte* bufferPtr = elementsByteBuffer.Ptr;
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectHandle>() <= endIndexOfMetadatasExclusive)
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

        public static bool TrySetObjectValue<T>(
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            ObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                ObjectData<T> objectValue = elementsBuffer[objectHandle.Index];
                if (objectValue.Version == objectHandle.Version)
                {
                    objectValue.Value = value;
                    elementsBuffer[objectHandle.Index] = objectValue;
                    return true;
                }
            }

            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            byte* bufferPtr = (byte*)elementsByteBuffer.GetUnsafePtr();
            GetObjectDatasStartIndex(bufferPtr, out int endIndexOfMetadatasExclusive);
            if (objectHandle.MetadataByteIndex + UnsafeUtility.SizeOf<VirtualObjectHandle>() <= endIndexOfMetadatasExclusive)
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

        public static void TrimCapacity<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<ObjectData<T>> elementsBuffer,
            int minCapacity)
            where T : unmanaged
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, 0, elementsBuffer.Length, out int lastUsedIndex);
            int newSize = math.max(0, math.max(minCapacity, lastUsedIndex + 1));
            elementsBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);

            // Clear ranges past new length
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if (tmpRange.StartInclusive >= elementsBuffer.Length)
                {
                    // Remove
                    freeIndexRangesBuffer.RemoveAt(i);
                }
                else if (tmpRange.EndExclusive > elementsBuffer.Length)
                {
                    // Trim
                    tmpRange.EndExclusive = elementsBuffer.Length;
                    freeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
            }
        }

        public static void TrimCapacity(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metadataFreeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            int minMetadatasCapacity,
            int minDataBytesCapacity)
        {
            byte* bufferPtr = elementsByteBuffer.GetUnsafePtr();
            int initialBufferLength = elementsByteBuffer.Length;
            GetObjectDatasStartIndex(bufferPtr, out int prevEndIndexOfMetadatasExclusive);

            // Metadatas
            int newSizeMetaDataBytes;
            {
                FindLastUsedIndex(ref metadataFreeIndexRangesBuffer, ByteIndex_ObjectMetadatasStart, prevEndIndexOfMetadatasExclusive, out int lastUsedIndex);
                newSizeMetaDataBytes = math.max(0, math.max(minMetadatasCapacity * UnsafeUtility.SizeOf<VirtualObjectMetadata>(), (lastUsedIndex - ByteIndex_ObjectMetadatasStart) + 1));
                int newEndIndexOfMetadatasExclusive = ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes;

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
                FindLastUsedIndex(ref dataFreeIndexRangesBuffer, prevEndIndexOfMetadatasExclusive, initialBufferLength, out int lastUsedIndex);
                newSizeDataBytes = math.max(0, math.max(minDataBytesCapacity, (lastUsedIndex - prevEndIndexOfMetadatasExclusive) + 1));
                int newEndOfDatasExclusive = ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes + newSizeDataBytes;

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
            elementsByteBuffer.Resize(ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes + newSizeDataBytes, NativeArrayOptions.ClearMemory);
        }

        public static void TrimCapacity<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<ObjectData<T>> elementsBuffer,
            int minCapacity)
            where T : unmanaged
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, 0, elementsBuffer.Length, out int lastUsedIndex);
            int newSize = math.max(0, math.max(minCapacity, lastUsedIndex + 1));
            elementsBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);

            // Clear ranges past new length
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if (tmpRange.StartInclusive >= elementsBuffer.Length)
                {
                    // Remove
                    freeIndexRangesBuffer.RemoveAt(i);
                }
                else if (tmpRange.EndExclusive > elementsBuffer.Length)
                {
                    // Trim
                    tmpRange.EndExclusive = elementsBuffer.Length;
                    freeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
            }
        }

        public static void TrimCapacity(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metadataFreeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            int minMetadatasCapacity,
            int minDataBytesCapacity)
        {
            byte* bufferPtr = elementsByteBuffer.Ptr;
            int initialBufferLength = elementsByteBuffer.Length;
            GetObjectDatasStartIndex(bufferPtr, out int prevEndIndexOfMetadatasExclusive);

            // Metadatas
            int newSizeMetaDataBytes;
            {
                FindLastUsedIndex(ref metadataFreeIndexRangesBuffer, ByteIndex_ObjectMetadatasStart, prevEndIndexOfMetadatasExclusive, out int lastUsedIndex);
                newSizeMetaDataBytes = math.max(0, math.max(minMetadatasCapacity * UnsafeUtility.SizeOf<VirtualObjectMetadata>(), (lastUsedIndex - ByteIndex_ObjectMetadatasStart) + 1));
                int newEndIndexOfMetadatasExclusive = ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes;

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
                FindLastUsedIndex(ref dataFreeIndexRangesBuffer, prevEndIndexOfMetadatasExclusive, initialBufferLength, out int lastUsedIndex);
                newSizeDataBytes = math.max(0, math.max(minDataBytesCapacity, (lastUsedIndex - prevEndIndexOfMetadatasExclusive) + 1));
                int newEndOfDatasExclusive = ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes + newSizeDataBytes;

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
            elementsByteBuffer.Resize(ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes + newSizeDataBytes, NativeArrayOptions.ClearMemory);
        }

        public static void TrimCapacity<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            int minCapacity)
            where T : unmanaged
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, 0, elementsBuffer.Length, out int lastUsedIndex);
            int newSize = math.max(0, math.max(minCapacity, lastUsedIndex + 1));
            elementsBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);

            // Clear ranges past new length
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if (tmpRange.StartInclusive >= elementsBuffer.Length)
                {
                    // Remove
                    freeIndexRangesBuffer.RemoveAt(i);
                }
                else if (tmpRange.EndExclusive > elementsBuffer.Length)
                {
                    // Trim
                    tmpRange.EndExclusive = elementsBuffer.Length;
                    freeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
            }
        }

        public static void TrimCapacity(
            ref NativeList<IndexRangeElement> dataFreeIndexRangesBuffer,
            ref NativeList<IndexRangeElement> metadataFreeIndexRangesBuffer,
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
                FindLastUsedIndex(ref metadataFreeIndexRangesBuffer, ByteIndex_ObjectMetadatasStart, prevEndIndexOfMetadatasExclusive, out int lastUsedIndex);
                newSizeMetaDataBytes = math.max(0, math.max(minMetadatasCapacity * UnsafeUtility.SizeOf<VirtualObjectMetadata>(), (lastUsedIndex - ByteIndex_ObjectMetadatasStart) + 1));
                int newEndIndexOfMetadatasExclusive = ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes;

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
                FindLastUsedIndex(ref dataFreeIndexRangesBuffer, prevEndIndexOfMetadatasExclusive, initialBufferLength, out int lastUsedIndex);
                newSizeDataBytes = math.max(0, math.max(minDataBytesCapacity, (lastUsedIndex - prevEndIndexOfMetadatasExclusive) + 1));
                int newEndOfDatasExclusive = ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes + newSizeDataBytes;

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
            elementsByteBuffer.Resize(ByteIndex_ObjectMetadatasStart + newSizeMetaDataBytes + newSizeDataBytes, NativeArrayOptions.ClearMemory);
        }

        private static bool FindFreeIndexRange<T>(ref T freeIndexRangesBuffer, int objectIndexesSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            for (int i = 0; i < freeIndexRangesBuffer.Length; i++)
            {
                IndexRangeElement indexRange = freeIndexRangesBuffer[i];
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
            for (int i = ByteIndex_ObjectMetadatasStart; i < metadatasEndIndexExclusive; i += UnsafeUtility.SizeOf<VirtualObjectMetadata>())
            {
                ReadValue(elementDataBufferPtr, i, out VirtualObjectMetadata metadata);
                metadata.ByteIndex += indexShift;
                WriteValue(elementDataBufferPtr, i, metadata);
            }
        }

        private static void GetExpandedFreeRange<T>(ref T freeIndexRangesBuffer, int previousEndIndexExclusive, int newEndIndexExclusive, 
            out IndexRangeElement freeIndexRange, out int indexOfFreeRange)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            // Add new free index range for the expanded capacity
            if (freeIndexRangesBuffer.Length > 0 &&
                freeIndexRangesBuffer[freeIndexRangesBuffer.Length - 1].EndExclusive == previousEndIndexExclusive)
            {
                // Expand the last range
                indexOfFreeRange = freeIndexRangesBuffer.Length - 1;
                freeIndexRange = freeIndexRangesBuffer[indexOfFreeRange];
                freeIndexRange.EndExclusive = newEndIndexExclusive;
            }
            else
            {
                // Create a new range
                indexOfFreeRange = -1;
                freeIndexRange = new IndexRangeElement
                {
                    StartInclusive = previousEndIndexExclusive,
                    EndExclusive = newEndIndexExclusive,
                };
            }
        }

        private static void ConsumeFreeRange(IndexRangeElement freeIndexRange, int objectIndexesSize,
            out bool isFullyConsumed, out int consumedStartIndex)
        {
            // Consume memory out of the found range
            consumedStartIndex = freeIndexRange.StartInclusive;
            freeIndexRange.StartInclusive += objectIndexesSize;

            Assert.IsTrue(freeIndexRange.StartInclusive <= freeIndexRange.EndExclusive);

            if (freeIndexRange.StartInclusive == freeIndexRange.EndExclusive)
            {
                isFullyConsumed = true;
            }
            isFullyConsumed = false;
        }

        public enum RangeFreeingType
        {
            MergeFirst,
            MergeLast,
            Insert,
            Add,
        }

        private static void EvaluateRangeFreeing<T>(ref T freeIndexRangesBuffer, int objectStartIndex, int objectIndexesSize,
            out RangeFreeingType rangeFreeingType, out int indexMatch)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            rangeFreeingType = RangeFreeingType.Add;
            indexMatch = -1;

            for (int i = 0; i < freeIndexRangesBuffer.Length; i++)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                // Assert no ranges overlap
                Assert.IsFalse(RangesOverlap(objectStartIndex, (objectStartIndex + objectIndexesSize), tmpRange.StartInclusive, tmpRange.EndExclusive));

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
        }

        private static bool FindLastUsedIndex<T>(ref T freeIndexRangesBuffer, int dataStartIndexInclusive, int dataEndIndexExclusive, out int lastUsedIndex)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            int evaluatedIndex = dataEndIndexExclusive - 1;
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if(evaluatedIndex < dataStartIndexInclusive)
                {
                    // If we're past the start index, we haven't found any used index
                    lastUsedIndex = -1;
                    return false;
                }
                else if (RangesOverlap(evaluatedIndex, evaluatedIndex + 1, tmpRange.StartInclusive, tmpRange.EndExclusive))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RangesOverlap(int aStartInclusive, int aEndExclusive, int bStartInclusive, int bEndExclusive)
        {
            return aStartInclusive < bEndExclusive && bStartInclusive < aEndExclusive;
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
            value = ByteIndex_ObjectMetadatasStart + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectMetadatasCapacity);
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