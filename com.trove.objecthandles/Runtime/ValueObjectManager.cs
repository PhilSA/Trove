using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine.UIElements;

namespace Trove.ObjectHandles
{
    public struct ObjectValue<T> where T : unmanaged
    {
        public int Version;
        public T Value;
    }

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
        public readonly int ByteIndex;
        public readonly int Size;
        public readonly int Id;
        internal int BufferVersion;

        public VirtualObjectHandle(int byteIndex, int size, int id, int bufferVersion)
        {
            ByteIndex = byteIndex;
            Size = size;
            Id = id;
            BufferVersion = bufferVersion;
        }
    }

    public struct IndexRangeElement
    {
        public int StartInclusive;
        public int EndExclusive;
    }

    public unsafe static class ValueObjectManager
    {
        private const float ObjectsCapacityGrowFactor = 2f;

        private const int ByteIndex_BufferVersion = 0;
        private const int ByteIndex_ObjectIdCounter = 4;
        private const int ByteIndex_ObjectDataBlockSize = 8;
        private const int RelativeByteIndex_ObjectIdCountInBlock = 0;
        private const int RelativeByteIndex_NextObjectIdBlockIndex = 4;
        private const int ByteIndex_ObjectIdList = 12;

        private const int ByteIndex_ObjectIdListBlockCapacity = 64;

        public static void Initialize<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<ObjectValue<T>> elementsBuffer,
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
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            int objectDataBlockBytesSize)
        {
            freeIndexRangesBuffer.Clear();
            elementsByteBuffer.Clear();

            freeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = 0,
                EndExclusive = objectDataBlockBytesSize,
            });

            int bufferSize = UnsafeUtility.SizeOf<int>() * 3;
            bufferSize += UnsafeUtility.SizeOf<int>() * ByteIndex_ObjectIdListBlockCapacity;
            bufferSize += objectDataBlockBytesSize;
            elementsByteBuffer.Resize(bufferSize, NativeArrayOptions.ClearMemory);

            // Write element buffer internal data
            byte* bufferPtr = elementsByteBuffer.GetUnsafePtr();
            WriteValue<int>(bufferPtr, ByteIndex_BufferVersion, 0);
            WriteValue<int>(bufferPtr, ByteIndex_ObjectIdCounter, 0);
            WriteValue<int>(bufferPtr, ByteIndex_ObjectDataBlockSize, objectDataBlockBytesSize);
            WriteValue<int>(bufferPtr, ByteIndex_ObjectIdCount, 0);
            WriteValue<int>(bufferPtr, ByteIndex_ObjectIdCapacity, initialObjectIdsCapacity);
        }

        public static void Initialize<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<ObjectValue<T>> elementsBuffer,
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
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            int initialElementBytesCapacity)
        {
            freeIndexRangesBuffer.Clear();
            elementsByteBuffer.Clear();

            freeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = 0,
                EndExclusive = initialElementBytesCapacity,
            });

            elementsByteBuffer.Resize(initialElementBytesCapacity, NativeArrayOptions.ClearMemory);
        }

        public static void Initialize<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectValue<T>> elementsBuffer,
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
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            int initialElementBytesCapacity)
        {
            freeIndexRangesBuffer.Clear();
            elementsByteBuffer.Clear();

            freeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = 0,
                EndExclusive = initialElementBytesCapacity,
            });

            elementsByteBuffer.Resize(initialElementBytesCapacity, NativeArrayOptions.ClearMemory);
        }

        public static ObjectHandle CreateObject<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<ObjectValue<T>> elementsBuffer,
            T objectValue)
            where T : unmanaged
        {
            // Find a free bytes range that accomodates the object size
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, 1, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
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
            ObjectValue<T> value = elementsBuffer[consumedStartIndex];
            value.Version++;
            value.Value = objectValue;
            elementsBuffer[consumedStartIndex] = value;

            return new ObjectHandle(consumedStartIndex, value.Version);
        }

        public static VirtualObjectHandle CreateObject(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            byte* objectValuePtr,
            int objectSize)
        {
            // Find a free bytes range that accomodates the object size
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, objectSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
                int prevLength = elementsByteBuffer.Length;
                int newLength = (int)math.ceil(elementsByteBuffer.Length * ObjectsCapacityGrowFactor);
                elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                GetExpandedFreeRange(ref freeIndexRangesBuffer, prevLength, elementsByteBuffer.Length,
                    out freeIndexRange, out indexOfFreeRange);
            }

            ConsumeFreeRange(freeIndexRange, objectSize, out bool isFullyConsumed, out int consumedStartIndex);

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

            // Read and bump version
            byte* bufferPtr = elementsByteBuffer.GetUnsafePtr();
            int writeIndex = consumedStartIndex;
            ReadValue<int>(bufferPtr, writeIndex, out int version);
            version++;
            WriteValue(bufferPtr, ref writeIndex, version);

            // Write object
            WriteValue(bufferPtr, writeIndex, objectValuePtr, objectSize);

            return new VirtualObjectHandle(consumedStartIndex, objectSize, version);
        }

        public static ObjectHandle CreateObject<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<T> elementsBuffer,
            T objectValue)
            where T : unmanaged
        {
            // Find a free bytes range that accomodates the object size
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, 1, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
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
            ObjectValue<T> value = elementsBuffer[consumedStartIndex];
            value.Version++;
            value.Value = objectValue;
            elementsBuffer[consumedStartIndex] = value;

            return new ObjectHandle(consumedStartIndex, value.Version);
        }

        public static VirtualObjectHandle CreateObject(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            byte* objectValuePtr,
            int objectSize)
        {
            // Find a free bytes range that accomodates the object size
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, objectSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
                int prevLength = elementsByteBuffer.Length;
                int newLength = (int)math.ceil(elementsByteBuffer.Length * ObjectsCapacityGrowFactor);
                elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                GetExpandedFreeRange(ref freeIndexRangesBuffer, prevLength, elementsByteBuffer.Length,
                    out freeIndexRange, out indexOfFreeRange);
            }

            ConsumeFreeRange(freeIndexRange, objectSize, out bool isFullyConsumed, out int consumedStartIndex);

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

            // Write object
            byte* destinationPtr = elementsByteBuffer.Ptr + (long)consumedStartIndex;
            UnsafeUtility.MemCpy(destinationPtr, objectValuePtr, objectSize);

            return new VirtualObjectHandle(consumedStartIndex, objectSize);
        }

        public static ObjectHandle CreateObject<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<T> elementsBuffer,
            T objectValue)
            where T : unmanaged
        {
            // Find a free bytes range that accomodates the object size
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, 1, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
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
            ObjectValue<T> value = elementsBuffer[consumedStartIndex];
            value.Version++;
            value.Value = objectValue;
            elementsBuffer[consumedStartIndex] = value;

            return new ObjectHandle(consumedStartIndex, value.Version);
        }

        public static VirtualObjectHandle CreateObject(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            byte* objectValuePtr,
            int objectSize)
        {
            // Find a free bytes range that accomodates the object size
            if (!FindFreeIndexRange(ref freeIndexRangesBuffer, objectSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
                int prevLength = elementsByteBuffer.Length;
                int newLength = (int)math.ceil(elementsByteBuffer.Length * ObjectsCapacityGrowFactor);
                elementsByteBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                GetExpandedFreeRange(ref freeIndexRangesBuffer, prevLength, elementsByteBuffer.Length,
                    out freeIndexRange, out indexOfFreeRange);
            }

            ConsumeFreeRange(freeIndexRange, objectSize, out bool isFullyConsumed, out int consumedStartIndex);

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

            // Write object
            byte* destinationPtr = (byte*)elementsByteBuffer.GetUnsafePtr() + (long)consumedStartIndex;
            UnsafeUtility.MemCpy(destinationPtr, objectValuePtr, objectSize);

            return new VirtualObjectHandle(consumedStartIndex, objectSize);
        }

        public static void FreeObject<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<T> elementsBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged
        {
            // TODO: check same version
            bool indexValid = objectHandle.Index < elementsBuffer.Length;
            Assert.IsTrue(indexValid);
            if (indexValid)
            {
                // Clear value
                elementsBuffer[objectHandle.Index] = default;

                // TODO: bump version

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

        public static void FreeObject(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
            // TODO: check same version
            bool indexValid = objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length;
            Assert.IsTrue(indexValid);
            if (indexValid)
            {
                // Clear value
                byte* destinationPtr = elementsByteBuffer.GetUnsafePtr() + (long)objectHandle.ByteIndex;
                UnsafeUtility.MemClear(destinationPtr, objectHandle.Size);

                // TODO: bump version

                EvaluateRangeFreeing(ref freeIndexRangesBuffer, objectHandle.ByteIndex, objectHandle.Size, out RangeFreeingType rangeFreeingType, out int indexMatch);
                switch (rangeFreeingType)
                {
                    case RangeFreeingType.MergeFirst:
                        {
                            IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                            rangeElement.StartInclusive -= objectHandle.Size;
                            freeIndexRangesBuffer[indexMatch] = rangeElement;
                            break;
                        }
                    case RangeFreeingType.MergeLast:
                        {
                            IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                            rangeElement.EndExclusive += objectHandle.Size;
                            freeIndexRangesBuffer[indexMatch] = rangeElement;
                            break;
                        }
                    case RangeFreeingType.Insert:
                        {
                            freeIndexRangesBuffer.InsertRange(indexMatch, objectHandle.Size);
                            freeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                            {
                                StartInclusive = objectHandle.ByteIndex,
                                EndExclusive = objectHandle.ByteIndex + objectHandle.Size,
                            };
                            break;
                        }
                    case RangeFreeingType.Add:
                        {
                            freeIndexRangesBuffer.Add(new IndexRangeElement
                            {
                                StartInclusive = objectHandle.ByteIndex,
                                EndExclusive = objectHandle.ByteIndex + objectHandle.Size,
                            });
                            break;
                        }
                }
            }
        }

        public static void FreeObject<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<T> elementsBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged
        {
            // TODO: check same version
            bool indexValid = objectHandle.Index < elementsBuffer.Length;
            Assert.IsTrue(indexValid);
            if (indexValid)
            {
                // Clear value
                elementsBuffer[objectHandle.Index] = default;

                // TODO: bump version

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

        public static void FreeObject(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
            // TODO: check same version
            bool indexValid = objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length;
            Assert.IsTrue(indexValid);
            if (indexValid)
            {
                // Clear value
                byte* destinationPtr = elementsByteBuffer.Ptr + (long)objectHandle.ByteIndex;
                UnsafeUtility.MemClear(destinationPtr, objectHandle.Size);

                // TODO: bump version

                EvaluateRangeFreeing(ref freeIndexRangesBuffer, objectHandle.ByteIndex, objectHandle.Size, out RangeFreeingType rangeFreeingType, out int indexMatch);
                switch (rangeFreeingType)
                {
                    case RangeFreeingType.MergeFirst:
                        {
                            IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                            rangeElement.StartInclusive -= objectHandle.Size;
                            freeIndexRangesBuffer[indexMatch] = rangeElement;
                            break;
                        }
                    case RangeFreeingType.MergeLast:
                        {
                            IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                            rangeElement.EndExclusive += objectHandle.Size;
                            freeIndexRangesBuffer[indexMatch] = rangeElement;
                            break;
                        }
                    case RangeFreeingType.Insert:
                        {
                            freeIndexRangesBuffer.InsertRange(indexMatch, objectHandle.Size);
                            freeIndexRangesBuffer[indexMatch] = new IndexRangeElement
                            {
                                StartInclusive = objectHandle.ByteIndex,
                                EndExclusive = objectHandle.ByteIndex + objectHandle.Size,
                            };
                            break;
                        }
                    case RangeFreeingType.Add:
                        {
                            freeIndexRangesBuffer.Add(new IndexRangeElement
                            {
                                StartInclusive = objectHandle.ByteIndex,
                                EndExclusive = objectHandle.ByteIndex + objectHandle.Size,
                            });
                            break;
                        }
                }
            }
        }

        public static void FreeObject<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<T> elementsBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged
        {
            // TODO: check same version
            bool indexValid = objectHandle.Index < elementsBuffer.Length;
            Assert.IsTrue(indexValid);
            if (indexValid)
            {
                // Clear value
                elementsBuffer[objectHandle.Index] = default;

                // TODO: bump version

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

        public static void FreeObject(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
            // TODO: check same version
            bool indexValid = objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length;
            Assert.IsTrue(indexValid);
            if (indexValid)
            {
                // Clear value
                byte* destinationPtr = (byte*)elementsByteBuffer.GetUnsafePtr() + (long)objectHandle.ByteIndex;
                UnsafeUtility.MemClear(destinationPtr, objectHandle.Size);

                // TODO: bump version

                EvaluateRangeFreeing(ref freeIndexRangesBuffer, objectHandle.ByteIndex, objectHandle.Size, out RangeFreeingType rangeFreeingType, out int indexMatch);
                switch (rangeFreeingType)
                {
                    case RangeFreeingType.MergeFirst:
                        {
                            IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                            rangeElement.StartInclusive -= objectHandle.Size;
                            freeIndexRangesBuffer[indexMatch] = rangeElement;
                            break;
                        }
                    case RangeFreeingType.MergeLast:
                        {
                            IndexRangeElement rangeElement = freeIndexRangesBuffer[indexMatch];
                            rangeElement.EndExclusive += objectHandle.Size;
                            freeIndexRangesBuffer[indexMatch] = rangeElement;
                            break;
                        }
                    case RangeFreeingType.Insert:
                        {
                            freeIndexRangesBuffer.Insert(indexMatch, new IndexRangeElement
                            {
                                StartInclusive = objectHandle.ByteIndex,
                                EndExclusive = objectHandle.ByteIndex + objectHandle.Size,
                            });
                            break;
                        }
                    case RangeFreeingType.Add:
                        {
                            freeIndexRangesBuffer.Add(new IndexRangeElement
                            {
                                StartInclusive = objectHandle.ByteIndex,
                                EndExclusive = objectHandle.ByteIndex + objectHandle.Size,
                            });
                            break;
                        }
                }
            }
        }

        public static bool TryGetObjectValue<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<T> elementsBuffer,
            ObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                // TODO; check version
                value = elementsBuffer[objectHandle.Index];
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length)
            {
                // TODO; check version
                byte* objPtr = elementsByteBuffer.GetUnsafePtr() + (long)objectHandle.ByteIndex;
                value = *(T*)objPtr;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<T> elementsBuffer,
            ObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                // TODO; check version
                value = elementsBuffer[objectHandle.Index];
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length)
            {
                // TODO; check version
                byte* objPtr = elementsByteBuffer.Ptr + (long)objectHandle.ByteIndex;
                value = *(T*)objPtr;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<T> elementsBuffer,
            ObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                // TODO; check version
                value = elementsBuffer[objectHandle.Index];
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetObjectValue<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            out T value)
            where T : unmanaged
        {
            if (objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length)
            {
                // TODO; check version
                byte* objPtr = (byte*)elementsByteBuffer.GetUnsafePtr() + (long)objectHandle.ByteIndex;
                value = *(T*)objPtr;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<T> elementsBuffer,
            ObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                // TODO; check version
                elementsBuffer[objectHandle.Index] = value;
                return true;
            }

            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length)
            {
                // TODO; check version
                byte* objPtr = elementsByteBuffer.GetUnsafePtr() + (long)objectHandle.ByteIndex;
                *(T*)objPtr = value;
                return true;
            }

            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<T> elementsBuffer,
            ObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                // TODO; check version
                elementsBuffer[objectHandle.Index] = value;
                return true;
            }

            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length)
            {
                // TODO; check version
                byte* objPtr = (byte*)elementsByteBuffer.Ptr + (long)objectHandle.ByteIndex;
                *(T*)objPtr = value;
                return true;
            }

            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<T> elementsBuffer,
            ObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                // TODO; check version
                elementsBuffer[objectHandle.Index] = value;
                return true;
            }

            return false;
        }

        public static bool TrySetObjectValue<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle,
            T value)
            where T : unmanaged
        {
            if (objectHandle.ByteIndex + objectHandle.Size <= elementsByteBuffer.Length)
            {
                // TODO; check version
                byte* objPtr = (byte*)elementsByteBuffer.GetUnsafePtr() + (long)objectHandle.ByteIndex;
                *(T*)objPtr = value;
                return true;
            }

            return false;
        }

        public static void TrimCapacity<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<T> elementsBuffer,
            int minCapacity)
            where T : unmanaged
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, elementsBuffer.Length, out int lastUsedIndex);
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
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            int minCapacity)
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, elementsByteBuffer.Length, out int lastUsedIndex);
            int newSize = math.max(0, math.max(minCapacity, lastUsedIndex + 1));
            elementsByteBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);

            // Clear ranges past new length
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if (tmpRange.StartInclusive >= elementsByteBuffer.Length)
                {
                    // Remove
                    freeIndexRangesBuffer.RemoveAt(i);
                }
                else if (tmpRange.EndExclusive > elementsByteBuffer.Length)
                {
                    // Trim
                    tmpRange.EndExclusive = elementsByteBuffer.Length;
                    freeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
            }
        }

        public static void TrimCapacity<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<T> elementsBuffer,
            int minCapacity)
            where T : unmanaged
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, elementsBuffer.Length, out int lastUsedIndex);
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
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            int minCapacity)
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, elementsByteBuffer.Length, out int lastUsedIndex);
            int newSize = math.max(0, math.max(minCapacity, lastUsedIndex + 1));
            elementsByteBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);

            // Clear ranges past new length
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if (tmpRange.StartInclusive >= elementsByteBuffer.Length)
                {
                    // Remove
                    freeIndexRangesBuffer.RemoveAt(i);
                }
                else if (tmpRange.EndExclusive > elementsByteBuffer.Length)
                {
                    // Trim
                    tmpRange.EndExclusive = elementsByteBuffer.Length;
                    freeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
            }
        }

        public static void TrimCapacity<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<T> elementsBuffer,
            int minCapacity)
            where T : unmanaged
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, elementsBuffer.Length, out int lastUsedIndex);
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
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            int minCapacity)
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, elementsByteBuffer.Length, out int lastUsedIndex);
            int newSize = math.max(0, math.max(minCapacity, lastUsedIndex + 1));
            elementsByteBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);

            // Clear ranges past new length
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if (tmpRange.StartInclusive >= elementsByteBuffer.Length)
                {
                    // Remove
                    freeIndexRangesBuffer.RemoveAt(i);
                }
                else if (tmpRange.EndExclusive > elementsByteBuffer.Length)
                {
                    // Trim
                    tmpRange.EndExclusive = elementsByteBuffer.Length;
                    freeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
            }
        }

        private static bool FindFreeIndexRange<T>(ref T freeIndexRangesBuffer, int objectSize, out IndexRangeElement freeIndexRange, out int indexOfFreeRange)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            for (int i = 0; i < freeIndexRangesBuffer.Length; i++)
            {
                IndexRangeElement indexRange = freeIndexRangesBuffer[i];
                if (indexRange.EndExclusive - indexRange.StartInclusive >= objectSize)
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

        private static void GetExpandedFreeRange<T>(ref T freeIndexRangesBuffer, int lengthBeforeResize, int lengthAfterResize,
            out IndexRangeElement freeIndexRange, out int indexOfFreeRange)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            // Add new free index range for the expanded capacity
            if (freeIndexRangesBuffer.Length > 0 &&
                freeIndexRangesBuffer[freeIndexRangesBuffer.Length - 1].EndExclusive == lengthBeforeResize)
            {
                // Expand the last range
                indexOfFreeRange = freeIndexRangesBuffer.Length - 1;
                freeIndexRange = freeIndexRangesBuffer[indexOfFreeRange];
                freeIndexRange.EndExclusive = lengthAfterResize;
            }
            else
            {
                // Create a new range
                indexOfFreeRange = -1;
                freeIndexRange = new IndexRangeElement
                {
                    StartInclusive = lengthBeforeResize,
                    EndExclusive = lengthAfterResize,
                };
            }
        }

        private static void ConsumeFreeRange(IndexRangeElement freeIndexRange, int objectSize,
            out bool isFullyConsumed, out int consumedStartIndex)
        {
            // Consume memory out of the found range
            consumedStartIndex = freeIndexRange.StartInclusive;
            freeIndexRange.StartInclusive += objectSize;

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

        private static void EvaluateRangeFreeing<T>(ref T freeIndexRangesBuffer, int objectStartIndex, int objectSize,
            out RangeFreeingType rangeFreeingType, out int indexMatch)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            rangeFreeingType = RangeFreeingType.Add;
            indexMatch = -1;

            for (int i = 0; i < freeIndexRangesBuffer.Length; i++)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                // Assert no ranges overlap
                Assert.IsFalse(RangesOverlap(objectStartIndex, (objectStartIndex + objectSize), tmpRange.StartInclusive, tmpRange.EndExclusive));

                if (tmpRange.StartInclusive == objectStartIndex + objectSize)
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

        private static void FindLastUsedIndex<T>(ref T freeIndexRangesBuffer, int elementsLength, out int lastUsedIndex)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            // 50 elements
            // Ranges:
            // 0-10 12-50
            // start... i49

            int evaluatedIndex = elementsLength - 1;
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if (RangesOverlap(evaluatedIndex, evaluatedIndex + 1, tmpRange.StartInclusive, tmpRange.EndExclusive))
                {
                    // If the ranges overlap, that means this evaluated index is free.
                    // Continue checking from the start of that free range.
                    evaluatedIndex = tmpRange.StartInclusive - 1;
                }
                else
                {
                    // If the ranges don't overlap, that means the last used index is the index at 
                    // the end of the free range
                    lastUsedIndex = tmpRange.EndExclusive;
                    return;
                }
            }

            lastUsedIndex = evaluatedIndex;
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
        public static void WriteValue<T>(byte* byteArrayPtr, int byteIndex, T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.AsRef<T>(startPtr) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteValue<T>(byte* byteArrayPtr, ref int byteIndex, T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.AsRef<T>(startPtr) = value;
            byteIndex += UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteValue(byte* byteArrayPtr, int byteIndex, byte* value, int valueSize)
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.MemCpy(startPtr, value, valueSize);
        }
    }
}