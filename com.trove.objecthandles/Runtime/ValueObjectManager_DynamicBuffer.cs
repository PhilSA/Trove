using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove.ObjectHandles
{
    public static partial class ValueObjectManager
    {
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

        public static ObjectHandle CreateObject<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            T value)
            where T : unmanaged
        {
            // Find a free bytes range that accomodates the object size
            if (!FindFreeRange(ref freeIndexRangesBuffer, out int indexOfFreeRange))
            {
                ResizeBufferAndExpandFreeRangesForObjectDataCapacityIncrease(ref freeIndexRangesBuffer, ref elementsBuffer);
            }

            ConsumeFromFreeRange(
                ref freeIndexRangesBuffer, 
                ref elementsBuffer, 
                indexOfFreeRange, 
                out int consumedStartIndex);

            // Bump version and write object
            ObjectData<T> objectData = elementsBuffer[consumedStartIndex];
            objectData.Version++;
            objectData.Value = value;
            elementsBuffer[consumedStartIndex] = objectData;

            return new ObjectHandle(consumedStartIndex, objectData.Version);
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

                    FreeRangeForIndex(ref freeIndexRangesBuffer, objectHandle.Index);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObjectValue<T>(
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            ObjectHandle<T> objectHandle,
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            ObjectHandle<T> objectHandle)
            where T : unmanaged
        {
            if (objectHandle.Index < elementsBuffer.Length)
            {
                ObjectData<T> objectValue = elementsBuffer[objectHandle.Index];
                if (objectValue.Version == objectHandle.Version)
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetObjectValue<T>(
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            ObjectHandle<T> objectHandle,
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

        public static void TrimCapacity<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            int minCapacity)
            where T : unmanaged
        {
            FindLastUsedIndex(ref freeIndexRangesBuffer, elementsBuffer.Length, out int lastUsedIndex);
            int newSize = math.max(0, math.max(minCapacity, lastUsedIndex + 1));
            elementsBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            elementsBuffer.Capacity = newSize;

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

        ///////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////

        internal static void FreeRangeForIndex(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            int objectIndex)
        {
            // Iterate ranges to determine which range to add the freed memory to (or where to insert new range)
            bool foundRangeInsertionPoint = false;
            for (int i = 0; i < freeIndexRangesBuffer.Length; i++)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                // Assert no ranges overlap
                Assert.IsFalse(ObjectManagerUtilities.RangesOverlap(objectIndex, (objectIndex + 1), tmpRange.StartInclusive, tmpRange.EndExclusive));

                // Merge at beginning
                if (tmpRange.StartInclusive == objectIndex + 1)
                {
                    tmpRange.StartInclusive -= 1;
                    freeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
                // Merge at end
                else if (tmpRange.EndExclusive == objectIndex)
                {
                    tmpRange.EndExclusive += 1;
                    freeIndexRangesBuffer[i] = tmpRange;
                    break;
                }
                // Insert
                else if (tmpRange.StartInclusive > objectIndex)
                {
                    freeIndexRangesBuffer.Insert(i, new IndexRangeElement
                    {
                        StartInclusive = objectIndex,
                        EndExclusive = objectIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                    });
                    break;
                }
            }

            if (!foundRangeInsertionPoint)
            {
                freeIndexRangesBuffer.Add(new IndexRangeElement
                {
                    StartInclusive = objectIndex,
                    EndExclusive = objectIndex + UnsafeUtility.SizeOf<VirtualObjectMetadata>(),
                });
            }
        }

        internal static void ConsumeFromFreeRange<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> elementsBuffer,
            int freeRangeIndex,
            out int consumedStartIndex)
            where T : unmanaged
        {
            IndexRangeElement freeRange = freeIndexRangesBuffer[freeRangeIndex];

            ObjectManagerUtilities.ConsumeFreeRange(ref freeRange, 1, out bool isFullyConsumed, out consumedStartIndex);
            if (isFullyConsumed)
            {
                freeIndexRangesBuffer.RemoveAt(freeRangeIndex);
            }
            else
            {
                freeIndexRangesBuffer[freeRangeIndex] = freeRange;
            }
        }

        internal static bool FindLastUsedIndex(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            int elementsLength,
            out int lastUsedIndex)
        {
            int evaluatedIndex = elementsLength - 1;
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

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

            // we haven't found any used index
            lastUsedIndex = -1;
            return false;
        }

        internal static bool FindFreeRange(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer, 
            out int indexOfFreeRange)
        {
            for (int i = 0; i < freeIndexRangesBuffer.Length; i++)
            {
                IndexRangeElement indexRange = freeIndexRangesBuffer[i];
                if (indexRange.EndExclusive - indexRange.StartInclusive >= 1)
                {
                    indexOfFreeRange = i;
                    return true;
                }
            }

            indexOfFreeRange = -1;
            return false;
        }

        internal static void ResizeBufferAndExpandFreeRangesForObjectDataCapacityIncrease<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> elementsBuffer)
            where T : unmanaged
        {
            int prevLength = elementsBuffer.Length;
            int newLength = (int)math.ceil(elementsBuffer.Length * ObjectsCapacityGrowFactor);
            elementsBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

            ExpandFreeRangesAfterResize(
                ref freeIndexRangesBuffer,
                ref elementsBuffer,
                prevLength,
                elementsBuffer.Length);
        }

        internal static void ExpandFreeRangesAfterResize<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<ObjectData<T>> bytesBuffer,
            int prevLength,
            int newLength)
            where T : unmanaged
        {
            // Add new free range for the expanded capacity
            if (freeIndexRangesBuffer.Length > 0)
            {
                int indexOfLastRange = freeIndexRangesBuffer.Length - 1;
                IndexRangeElement freeRange = freeIndexRangesBuffer[indexOfLastRange];

                if (freeRange.EndExclusive == prevLength)
                {
                    // Expand the last range
                    freeRange.EndExclusive = newLength;
                    freeIndexRangesBuffer[indexOfLastRange] = freeRange;
                    return;
                }
            }

            // Create a new range
            freeIndexRangesBuffer.Add(new IndexRangeElement
            {
                StartInclusive = prevLength,
                EndExclusive = newLength,
            });
        }
    }
}