using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove.ObjectHandles
{
    // TODO: handle object version

    public struct ObjectHandle
    {
        public readonly int Index;

        public ObjectHandle(int index)
        {
            Index = index;
        }
    }

    public unsafe struct VirtualObjectHandle
    {
        public readonly int ByteIndex;
        public readonly int Size;

        public VirtualObjectHandle(int byteIndex, int size)
        {
            ByteIndex = byteIndex;
            Size = size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T As<T>(NativeList<byte> elementsByteBuffer) where T : unmanaged
        {
            Assert.IsTrue(Size == UnsafeUtility.SizeOf<T>());
            byte* objPtr = elementsByteBuffer.GetUnsafePtr() + (long)ByteIndex;
            return *(T*)objPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AsRef<T>(NativeList<byte> elementsByteBuffer) where T : unmanaged
        {
            Assert.IsTrue(Size == UnsafeUtility.SizeOf<T>());
            byte* objPtr = elementsByteBuffer.GetUnsafePtr() + (long)ByteIndex;
            return ref *(T*)objPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T As<T>(UnsafeList<byte> elementsByteBuffer) where T : unmanaged
        {
            Assert.IsTrue(Size == UnsafeUtility.SizeOf<T>());
            byte* objPtr = elementsByteBuffer.Ptr + (long)ByteIndex;
            return *(T*)objPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AsRef<T>(UnsafeList<byte> elementsByteBuffer) where T : unmanaged
        {
            Assert.IsTrue(Size == UnsafeUtility.SizeOf<T>());
            byte* objPtr = elementsByteBuffer.Ptr + (long)ByteIndex;
            return ref *(T*)objPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T As<T>(DynamicBuffer<byte> elementsByteBuffer) where T : unmanaged
        {
            Assert.IsTrue(Size == UnsafeUtility.SizeOf<T>());
            byte* objPtr = (byte*)elementsByteBuffer.GetUnsafePtr() + (long)ByteIndex;
            return *(T*)objPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AsRef<T>(DynamicBuffer<byte> elementsByteBuffer) where T : unmanaged
        {
            Assert.IsTrue(Size == UnsafeUtility.SizeOf<T>());
            byte* objPtr = (byte*)elementsByteBuffer.GetUnsafePtr() + (long)ByteIndex;
            return ref *(T*)objPtr;
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

        public static void Initialize<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<T> elementsBuffer,
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
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<T> elementsBuffer,
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
            ref DynamicBuffer<T> elementsBuffer, 
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
            ref NativeList<T> elementsBuffer,
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

            if(isFullyConsumed)
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
            elementsBuffer[consumedStartIndex] = objectValue;

            return new ObjectHandle(consumedStartIndex);
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

            // Write object
            byte* destinationPtr = elementsByteBuffer.GetUnsafePtr() + (long)consumedStartIndex;
            UnsafeUtility.MemCpy(destinationPtr, objectValuePtr, objectSize);

            return new VirtualObjectHandle(consumedStartIndex, objectSize);
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

            // Write object
            elementsBuffer[consumedStartIndex] = objectValue;

            return new ObjectHandle(consumedStartIndex);
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

            // Write object
            elementsBuffer[consumedStartIndex] = objectValue;

            return new ObjectHandle(consumedStartIndex);
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
        }

        public static void FreeObject(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
        }

        public static void FreeObject<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<T> elementsBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged
        {
        }

        public static void FreeObject(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
        }

        public static void FreeObject<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<T> elementsBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged
        {
        }

        public static void FreeObject(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer,
            VirtualObjectHandle objectHandle)
        {
        }

        public static void TrimCapacity<T>(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<T> elementsBuffer)
            where T : unmanaged
        {
        }

        public static void TrimCapacity(
            ref NativeList<IndexRangeElement> freeIndexRangesBuffer,
            ref NativeList<byte> elementsByteBuffer)
        {
        }

        public static void TrimCapacity<T>(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<T> elementsBuffer)
            where T : unmanaged
        {
        }

        public static void TrimCapacity(
            ref UnsafeList<IndexRangeElement> freeIndexRangesBuffer,
            ref UnsafeList<byte> elementsByteBuffer)
        {
        }

        public static void TrimCapacity<T>(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<T> elementsBuffer)
            where T : unmanaged
        {
        }

        public static void TrimCapacity(
            ref DynamicBuffer<IndexRangeElement> freeIndexRangesBuffer,
            ref DynamicBuffer<byte> elementsByteBuffer)
        {
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
    }
}