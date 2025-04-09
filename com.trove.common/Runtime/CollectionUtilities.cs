using System;
using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove
{
    public struct IndexRange
    {
        public int Start;
        public int Length;
    }

    public static class CollectionUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddWithGrowFactor<T>(ref this UnsafeList<T> list, T addedElement, float growFactor = 1.5f) where T : unmanaged
        {
            int initialLength = list.Length;
            if (initialLength + 1 >= list.Capacity)
            {
                int newCapacity = (int)math.ceil(list.Capacity * growFactor);
                newCapacity = math.max(initialLength + 1, newCapacity);
                list.SetCapacity(newCapacity);
            }

            list.Add(addedElement);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetFixedList32Capacity<T>() where T : unmanaged
        {
            return FixedList.Capacity<FixedBytes32Align8, T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetFixedList64Capacity<T>() where T : unmanaged
        {
            return FixedList.Capacity<FixedBytes64Align8, T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetFixedList128Capacity<T>() where T : unmanaged
        {
            return FixedList.Capacity<FixedBytes128Align8, T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetFixedList512Capacity<T>() where T : unmanaged
        {
            return FixedList.Capacity<FixedBytes512Align8, T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetFixedList4096Capacity<T>() where T : unmanaged
        {
            return FixedList.Capacity<FixedBytes4096Align8, T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetFixedListElementAsRef<T>(ref FixedList32Bytes<T> fixedList, int index)
            where T : unmanaged
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(fixedList.Buffer, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetFixedListElementAsRef<T>(ref FixedList64Bytes<T> fixedList, int index)
            where T : unmanaged
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(fixedList.Buffer, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetFixedListElementAsRef<T>(ref FixedList128Bytes<T> fixedList, int index)
            where T : unmanaged
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(fixedList.Buffer, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetFixedListElementAsRef<T>(ref FixedList512Bytes<T> fixedList, int index)
            where T : unmanaged
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(fixedList.Buffer, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetFixedListElementAsRef<T>(ref FixedList4096Bytes<T> fixedList, int index)
            where T : unmanaged
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(fixedList.Buffer, index);
        }

        public static unsafe void InsertRange<T>(this DynamicBuffer<T> buffer, int insertIndex, int insertLength)
            where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)insertIndex >= (uint)buffer.Length)
                throw new IndexOutOfRangeException($"Index {insertIndex} is out of range in DynamicBuffer of '{buffer.Length}' Length.");
#endif

            int initialLength = buffer.Length;
            buffer.ResizeUninitialized(initialLength + insertLength);
            int elemSize = UnsafeUtility.SizeOf<T>();
            byte* basePtr = (byte*)buffer.GetUnsafePtr();
            UnsafeUtility.MemMove(
                basePtr + ((insertIndex + insertLength) * elemSize),
                basePtr + (insertIndex * elemSize),
                (long)elemSize * (initialLength - insertIndex));
        }
    }
}