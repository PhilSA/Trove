using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove.ObjectHandles
{
    public struct ObjectHandle<T> where T : unmanaged
    {
        internal readonly int Index;
        internal readonly int Version;

        internal ObjectHandle(ObjectHandle handle)
        {
            Index = handle.Index;
            Version = handle.Version;
        }

        public static implicit operator ObjectHandle(ObjectHandle<T> o) => new ObjectHandle(o.Index, o.Version);
    }

    public struct ObjectData<T> where T : unmanaged
    {
        public int Version;
        public T Value;
    }

    public struct ObjectHandle
    {
        internal readonly int Index;
        internal readonly int Version;

        internal ObjectHandle(int index, int version)
        {
            Index = index;
            Version = version;
        }
    }
    public struct ValueObjectManager : IComponentData
    {
        private const float ObjectsCapacityGrowFactor = 2f;

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
            if (!ObjectManagerUtilities.FindFreeIndexRange(ref freeIndexRangesBuffer, 1, out IndexRangeElement freeIndexRange, out int indexOfFreeRange))
            {
                int prevLength = elementsBuffer.Length;
                int newLength = (int)math.ceil(elementsBuffer.Length * ObjectsCapacityGrowFactor);
                elementsBuffer.Resize(newLength, NativeArrayOptions.ClearMemory);

                ObjectManagerUtilities.GetExpandedFreeRange(ref freeIndexRangesBuffer, prevLength, elementsBuffer.Length,
                    out freeIndexRange, out indexOfFreeRange);
            }

            ObjectManagerUtilities.ConsumeFreeRange(freeIndexRange, 1, out bool isFullyConsumed, out int consumedStartIndex);
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

                    ObjectManagerUtilities.EvaluateRangeFreeing(ref freeIndexRangesBuffer, objectHandle.Index, 1, out RangeFreeingType rangeFreeingType, out int indexMatch);
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
            ObjectManagerUtilities.FindLastUsedIndex(ref freeIndexRangesBuffer, 0, elementsBuffer.Length, out int lastUsedIndex);
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
    }
}