using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Assertions;

namespace Trove
{
    /*
     * TODO: Commented out because it needs to be reworked.
     * This should work similarly to the VersionedPool, but with free ranges allocation on top, It should have
     * objectHandles and versions. Otherwise, if we remove then add an object, we won't know the object index is now
     * pointing to a different object.
     */
    
    
    
    /*
        
    
    /// <summary>
    /// A type of object pool where we keep track of a list of free index ranges in order to determine which indexes
    /// are taken and which are available.
    /// This type of pool is a good choice if pool object allocation speed and pool iteration speed are important.
    /// </summary>
    public static class FreeRangesPool
    {
        /// <summary>
        /// NOTE: becomes invalid as soon as either collections are modified
        /// </summary>
        public unsafe struct UnsafeIterator<T> where T : unmanaged
        {
            private T* _datasPtr;
            private int _datasLength;
            private IndexRange* _freeIndexRangesPtr;
            private int _freeIndexRangesLength;
            private int _iteratedElementIndex;
            private int _iteratedFreeRangeIndex;

            public UnsafeIterator(T* datasPtr, int datasLength, IndexRange* freeIndexRangesPtr,
                int freeIndexRangesLength)
            {
                _datasPtr = datasPtr;
                _datasLength = datasLength;
                _freeIndexRangesPtr = freeIndexRangesPtr;
                _freeIndexRangesLength = freeIndexRangesLength;
                _iteratedElementIndex = 0;
                _iteratedFreeRangeIndex = 0;
            }

            public bool GetNextElement(out T element, out int elementIndex)
            {
                while (_iteratedElementIndex < _datasLength)
                {
                    IndexRange nextFreeRange = new IndexRange
                    {
                        Start = _datasLength,
                        Length = 0,
                    };
                    if (_iteratedFreeRangeIndex < _freeIndexRangesLength)
                    {
                        nextFreeRange = _freeIndexRangesPtr[_iteratedFreeRangeIndex];
                    }

                    // Remember index of evaluated element before increment
                    int evaluatedElementIndex = _iteratedElementIndex;

                    // Increment element index, and increment iterated free range if we reached the current range start.
                    // Also increment element index to skip the whole free range.
                    _iteratedElementIndex++;
                    if (_iteratedElementIndex >= nextFreeRange.Start)
                    {
                        _iteratedElementIndex += nextFreeRange.Length;
                        _iteratedFreeRangeIndex++;
                    }

                    // Return all elements before the start of the iterated free range
                    if (evaluatedElementIndex < nextFreeRange.Start)
                    {
                        element = _datasPtr[evaluatedElementIndex];
                        elementIndex = evaluatedElementIndex;
                        return true;
                    }
                }

                element = default;
                elementIndex = -1;
                return false;
            }
        }

        #region NativeList Pool

        public static void Init<T>(ref NativeList<T> dataBuffer, ref NativeList<IndexRange> freeIndexRanges,
            int capacity)
            where T : unmanaged
        {
            freeIndexRanges.Clear();
            freeIndexRanges.Add(new IndexRange { Start = 0, Length = capacity });

            dataBuffer.Clear();
            dataBuffer.Resize(capacity, NativeArrayOptions.ClearMemory);
        }

        /// <summary>
        /// Note: only works for increasing size
        /// </summary>
        public static unsafe void Resize<T>(ref NativeList<T> dataBuffer, ref NativeList<IndexRange> freeIndexRanges,
            int newCapacity)
            where T : unmanaged
        {
            if (newCapacity > dataBuffer.Length)
            {
                int initialCapacity = dataBuffer.Length;

                // Resize datas buffer
                dataBuffer.Resize(newCapacity, NativeArrayOptions.ClearMemory);

                if (freeIndexRanges.Length > 0)
                {
                    ref IndexRange lastFreeRange =
                        ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRanges.GetUnsafePtr(),
                            freeIndexRanges.Length - 1);

                    // Check if we can just expand last range (if it ended at the capacity before resize)
                    if (lastFreeRange.Start + lastFreeRange.Length == initialCapacity)
                    {
                        lastFreeRange.Length += newCapacity - initialCapacity;
                    }
                    // If not, add new range
                    else
                    {
                        freeIndexRanges.Add(new IndexRange
                            { Start = initialCapacity, Length = newCapacity - initialCapacity });
                    }
                }
                // If there were no free ranges left at all, add new one
                else
                {
                    freeIndexRanges.Add(new IndexRange
                        { Start = initialCapacity, Length = newCapacity - initialCapacity });
                }
            }
        }

        public static int CalculateElementsCount<T>(in NativeList<T> dataBuffer,
            ref NativeList<IndexRange> freeIndexRange)
            where T : unmanaged
        {
            int poolCapacity = dataBuffer.Length;
            int freeElementsCount = 0;
            for (int i = 0; i < freeIndexRange.Length; i++)
            {
                freeElementsCount += freeIndexRange[i].Length;
            }

            return poolCapacity - freeElementsCount;
        }

        public static void Add<T>(ref NativeList<T> dataBuffer,
            ref NativeList<IndexRange> freeIndexRanges, in T element, out int elementIndex,
            float growFactor = 1.5f)
            where T : unmanaged
        {
            AddRange(ref dataBuffer, ref freeIndexRanges, 1, out elementIndex, growFactor);
            dataBuffer[elementIndex] = element;
        }

        public static unsafe void AddRange<T>(ref NativeList<T> dataBuffer,
            ref NativeList<IndexRange> freeIndexRanges, int rangeLength, out int firstElementIndex,
            float growFactor = 1.5f)
            where T : unmanaged
        {
            IndexRange* freeIndexRangesPtr = freeIndexRanges.GetUnsafePtr();

            // Find a range that can accomodate the range
            for (int i = 0; i < freeIndexRanges.Length; i++)
            {
                ref IndexRange iteratedIndexRange =
                    ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRangesPtr, i);
                if (iteratedIndexRange.Length >= rangeLength)
                {
                    firstElementIndex = iteratedIndexRange.Start;

                    // Update free range and remove if no more space left
                    iteratedIndexRange.Start += rangeLength;
                    iteratedIndexRange.Length -= rangeLength;
                    if (iteratedIndexRange.Length == 0)
                    {
                        freeIndexRanges.RemoveAt(i);
                    }

                    return;
                }
            }

            // If reached this point, we haven't found a valid range. Resize pool to accomodate
            int newCapacity = math.max((int)math.ceil(dataBuffer.Length * growFactor),
                dataBuffer.Length + rangeLength);
            Resize(ref dataBuffer, ref freeIndexRanges, newCapacity);

            int lastRangeIndex = freeIndexRanges.Length - 1;
            ref IndexRange lastIndexRange =
                ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRanges.GetUnsafePtr(),
                    lastRangeIndex);

            Assert.IsTrue(lastIndexRange.Length >= rangeLength);

            firstElementIndex = lastIndexRange.Start;

            // Update free range and remove if no more space left
            lastIndexRange.Start += rangeLength;
            lastIndexRange.Length -= rangeLength;
            if (lastIndexRange.Length == 0)
            {
                freeIndexRanges.RemoveAt(lastRangeIndex);
            }
        }

        public static void Remove<T>(ref NativeList<T> dataBuffer, ref NativeList<IndexRange> freeIndexRanges,
            int elementIndex)
            where T : unmanaged
        {
            RemoveRange(ref dataBuffer, ref freeIndexRanges, elementIndex, 1, true);
        }

        /// <summary>
        /// NOTE: guaranteed to free the specified range even if includes already freed indexes
        /// </summary>
        public static unsafe void RemoveRange<T>(ref NativeList<T> dataBuffer,
            ref NativeList<IndexRange> freeIndexRanges, int rangeStartIndex, int rangeLength, bool clearData)
            where T : unmanaged
        {
            // Clamp freed range to pool range
            int rangeLastIndex = rangeStartIndex + rangeLength - 1;
            rangeLastIndex = math.min(dataBuffer.Length, rangeLastIndex);
            rangeStartIndex = math.max(rangeStartIndex, 0);
            rangeLength = rangeLastIndex - rangeStartIndex + 1;

            // Check valid index range
            if (rangeLastIndex < 0 ||
                rangeLength <= 0)
            {
                return;
            }

            IndexRange* freeIndexRangesPtr = freeIndexRanges.GetUnsafePtr();

            // Iterate free ranges to try and find a matching range to expand, or find a point where we can insert
            // a new range
            for (int i = 0; i < freeIndexRanges.Length; i++)
            {
                ref IndexRange iteratedIndexRange =
                    ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRangesPtr, i);

                // If the ranges overlap, or if the freed range ends right before the iterated range starts,
                // expand the range backward and check to merge with a previous
                if (MathUtilities.RangesOverlap(rangeStartIndex, rangeLength, iteratedIndexRange.Start,
                        iteratedIndexRange.Length) ||
                    rangeLastIndex == iteratedIndexRange.Start - 1)
                {
                    int iteratedRangeLastIndex = iteratedIndexRange.Start + iteratedIndexRange.Length - 1;
                    iteratedIndexRange.Start = math.min(iteratedIndexRange.Start, rangeStartIndex);
                    iteratedIndexRange.Length = iteratedRangeLastIndex - iteratedIndexRange.Start + 1;
                    // Check for merge if prev range ends right before the new expanded iterated range
                    if (i > 0)
                    {
                        IndexRange prevFreeIndexRange = freeIndexRanges[i - 1];
                        if (prevFreeIndexRange.Start + prevFreeIndexRange.Length == iteratedIndexRange.Start - 1)
                        {
                            iteratedIndexRange.Start = prevFreeIndexRange.Start;
                            iteratedIndexRange.Length = iteratedRangeLastIndex - iteratedIndexRange.Start + 1;
                            freeIndexRanges.RemoveAt(i - 1);
                            i--; // decrement index to compensate for element remove
                            // IMPORTANT: do not use iteratedIndexRange past this point
                        }
                    }

                    // Update range start so it starts after this expanded iterated range
                    rangeStartIndex = iteratedRangeLastIndex + 1;
                    rangeLength = rangeLastIndex - rangeStartIndex + 1;

                    // If the freed range is fully freed, break
                    if (rangeStartIndex > rangeLastIndex)
                    {
                        break;
                    }
                }
                // If the ranges don't touch and the iterated range is completely past the freed range
                else if (rangeLastIndex < iteratedIndexRange.Start - 1)
                {
                    // If the freed range is not fully freed, insert a new range and break
                    if (rangeStartIndex <= rangeLastIndex)
                    {
                        freeIndexRanges.InsertRange(i, 1);
                        freeIndexRanges[i] = new IndexRange
                        {
                            Start = rangeStartIndex,
                            Length = rangeLength,
                        };
                    }

                    break;
                }
            }

            if (clearData)
            {
                for (int i = rangeStartIndex; i <= rangeLastIndex; i++)
                {
                    dataBuffer[i] = default;
                }
            }
        }

        public static unsafe UnsafeIterator<T> GetIterator<T>(NativeList<T> dataBuffer,
            NativeList<IndexRange> freeIndexRanges)
            where T : unmanaged
        {
            return new UnsafeIterator<T>(dataBuffer.GetUnsafeReadOnlyPtr(), dataBuffer.Length,
                freeIndexRanges.GetUnsafeReadOnlyPtr(), freeIndexRanges.Length);
        }

        #endregion

        #region DynamicBuffer Pool

        public static void Init<T>(ref DynamicBuffer<T> dataBuffer, ref DynamicBuffer<IndexRange> freeIndexRanges,
            int capacity)
            where T : unmanaged
        {
            freeIndexRanges.Clear();
            freeIndexRanges.Add(new IndexRange { Start = 0, Length = capacity });

            dataBuffer.Clear();
            dataBuffer.Resize(capacity, NativeArrayOptions.ClearMemory);
        }

        /// <summary>
        /// Note: only works for increasing size
        /// </summary>
        public static unsafe void Resize<T>(ref DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRanges,
            int newCapacity)
            where T : unmanaged
        {
            if (newCapacity > dataBuffer.Length)
            {
                int initialCapacity = dataBuffer.Length;

                // Resize datas buffer
                dataBuffer.Resize(newCapacity, NativeArrayOptions.ClearMemory);

                if (freeIndexRanges.Length > 0)
                {
                    ref IndexRange lastFreeRange =
                        ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRanges.GetUnsafePtr(),
                            freeIndexRanges.Length - 1);

                    // Check if we can just expand last range (if it ended at the capacity before resize)
                    if (lastFreeRange.Start + lastFreeRange.Length == initialCapacity)
                    {
                        lastFreeRange.Length += newCapacity - initialCapacity;
                    }
                    // If not, add new range
                    else
                    {
                        freeIndexRanges.Add(new IndexRange
                            { Start = initialCapacity, Length = newCapacity - initialCapacity });
                    }
                }
                // If there were no free ranges left at all, add new one
                else
                {
                    freeIndexRanges.Add(new IndexRange
                        { Start = initialCapacity, Length = newCapacity - initialCapacity });
                }
            }
        }

        public static int CalculateElementsCount<T>(in DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRange)
            where T : unmanaged
        {
            int poolCapacity = dataBuffer.Length;
            int freeElementsCount = 0;
            for (int i = 0; i < freeIndexRange.Length; i++)
            {
                freeElementsCount += freeIndexRange[i].Length;
            }

            return poolCapacity - freeElementsCount;
        }

        public static void Add<T>(ref DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRanges, in T element, out int elementIndex, float growFactor = 1.5f)
            where T : unmanaged
        {
            AddRange(ref dataBuffer, ref freeIndexRanges, 1, out elementIndex, growFactor);
            dataBuffer[elementIndex] = element;
        }

        public static unsafe void AddRange<T>(ref DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRanges, int rangeLength, out int firstElementIndex,
            float growFactor = 1.5f)
            where T : unmanaged
        {
            IndexRange* freeIndexRangesPtr = (IndexRange*)freeIndexRanges.GetUnsafePtr();

            // Find a range that can accomodate the range
            for (int i = 0; i < freeIndexRanges.Length; i++)
            {
                ref IndexRange iteratedIndexRange =
                    ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRangesPtr, i);
                if (iteratedIndexRange.Length >= rangeLength)
                {
                    firstElementIndex = iteratedIndexRange.Start;

                    // Update free range and remove if no more space left
                    iteratedIndexRange.Start += rangeLength;
                    iteratedIndexRange.Length -= rangeLength;
                    if (iteratedIndexRange.Length == 0)
                    {
                        freeIndexRanges.RemoveAt(i);
                    }

                    return;
                }
            }

            // If reached this point, we haven't found a valid range. Resize pool to accomodate
            int newCapacity = math.max((int)math.ceil(dataBuffer.Length * growFactor),
                dataBuffer.Length + rangeLength);
            Resize(ref dataBuffer, ref freeIndexRanges, newCapacity);

            int lastRangeIndex = freeIndexRanges.Length - 1;
            ref IndexRange lastIndexRange =
                ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRanges.GetUnsafePtr(),
                    lastRangeIndex);

            Assert.IsTrue(lastIndexRange.Length >= rangeLength);

            firstElementIndex = lastIndexRange.Start;

            // Update free range and remove if no more space left
            lastIndexRange.Start += rangeLength;
            lastIndexRange.Length -= rangeLength;
            if (lastIndexRange.Length == 0)
            {
                freeIndexRanges.RemoveAt(lastRangeIndex);
            }
        }

        public static void Remove<T>(ref DynamicBuffer<T> dataBuffer, ref DynamicBuffer<IndexRange> freeIndexRanges,
            int elementIndex)
            where T : unmanaged
        {
            RemoveRange(ref dataBuffer, ref freeIndexRanges, elementIndex, 1, true);
        }

        /// <summary>
        /// NOTE: guaranteed to free the specified range even if includes already freed indexes
        /// </summary>
        public static unsafe void RemoveRange<T>(ref DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRanges, int rangeStartIndex, int rangeLength, bool clearData)
            where T : unmanaged
        {
            // Clamp freed range to pool range
            int rangeLastIndex = rangeStartIndex + rangeLength - 1;
            rangeLastIndex = math.min(dataBuffer.Length, rangeLastIndex);
            rangeStartIndex = math.max(rangeStartIndex, 0);
            rangeLength = rangeLastIndex - rangeStartIndex + 1;

            // Check valid index range
            if (rangeLastIndex < 0 ||
                rangeLength <= 0)
            {
                return;
            }

            IndexRange* freeIndexRangesPtr = (IndexRange*)freeIndexRanges.GetUnsafePtr();

            // Iterate free ranges to try and find a matching range to expand, or find a point where we can insert
            // a new range
            for (int i = 0; i < freeIndexRanges.Length; i++)
            {
                ref IndexRange iteratedIndexRange =
                    ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRangesPtr, i);

                // If the ranges overlap, or if the freed range ends right before the iterated range starts,
                // expand the range backward and check to merge with a previous
                if (MathUtilities.RangesOverlap(rangeStartIndex, rangeLength, iteratedIndexRange.Start,
                        iteratedIndexRange.Length) ||
                    rangeLastIndex == iteratedIndexRange.Start - 1)
                {
                    int iteratedRangeLastIndex = iteratedIndexRange.Start + iteratedIndexRange.Length - 1;
                    iteratedIndexRange.Start = math.min(iteratedIndexRange.Start, rangeStartIndex);
                    iteratedIndexRange.Length = iteratedRangeLastIndex - iteratedIndexRange.Start + 1;
                    // Check for merge if prev range ends right before the new expanded iterated range
                    if (i > 0)
                    {
                        IndexRange prevFreeIndexRange = freeIndexRanges[i - 1];
                        if (prevFreeIndexRange.Start + prevFreeIndexRange.Length == iteratedIndexRange.Start - 1)
                        {
                            iteratedIndexRange.Start = prevFreeIndexRange.Start;
                            iteratedIndexRange.Length = iteratedRangeLastIndex - iteratedIndexRange.Start + 1;
                            freeIndexRanges.RemoveAt(i - 1);
                            i--; // decrement index to compensate for element remove
                            // IMPORTANT: do not use iteratedIndexRange past this point
                        }
                    }

                    // Update range start so it starts after this expanded iterated range
                    rangeStartIndex = iteratedRangeLastIndex + 1;
                    rangeLength = rangeLastIndex - rangeStartIndex + 1;

                    // If the freed range is fully freed, break
                    if (rangeStartIndex > rangeLastIndex)
                    {
                        break;
                    }
                }
                // If the ranges don't touch and the iterated range is completely past the freed range
                else if (rangeLastIndex < iteratedIndexRange.Start - 1)
                {
                    // If the freed range is not fully freed, insert a new range and break
                    if (rangeStartIndex <= rangeLastIndex)
                    {
                        freeIndexRanges.InsertRange(i, 1);
                        freeIndexRanges[i] = new IndexRange
                        {
                            Start = rangeStartIndex,
                            Length = rangeLength,
                        };
                    }

                    break;
                }
            }

            if (clearData)
            {
                for (int i = rangeStartIndex; i <= rangeLastIndex; i++)
                {
                    dataBuffer[i] = default;
                }
            }
        }

        public static unsafe UnsafeIterator<T> GetIterator<T>(DynamicBuffer<T> dataBuffer,
            DynamicBuffer<IndexRange> freeIndexRanges)
            where T : unmanaged
        {
            return new UnsafeIterator<T>((T*)dataBuffer.GetUnsafeReadOnlyPtr(), dataBuffer.Length,
                (IndexRange*)freeIndexRanges.GetUnsafeReadOnlyPtr(), freeIndexRanges.Length);
        }

        #endregion

        #region SubLists
        /// <summary>
        /// A list that occupies a contiguous range of indexes in a pool. It if grows, it could be reallocated
        /// elsewhere in the pool.
        /// </summary>
        public struct SubList<T> where T : unmanaged
        {
            private SubList _subList;

            public int Length => _subList.Length;
            public int Capacity => _subList.Capacity;
            public bool IsCreated => _subList.IsCreated;

            #region NativeList PoolList

            public static SubList<T> Create(ref NativeList<T> dataBuffer,
                ref NativeList<IndexRange> freeIndexRanges,
                int capacity, float poolListGrowFactor = 1.5f, float poolGrowFactor = 1.5f)
            {
                return new SubList<T>()
                {
                    _subList = SubList.Create(ref dataBuffer, ref freeIndexRanges, capacity,
                        poolListGrowFactor,
                        poolGrowFactor),
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T GetElement(in SubList<T> subList, ref NativeList<T> dataBuffer,
                int index)
            {
                return SubList.GetElement(in subList._subList, ref dataBuffer, index);
            }

            public static void ResolveUnsafe(in SubList<T> subList, ref NativeList<T> dataBuffer,
                out UnsafeList<T> listData)
            {
                SubList.ResolveUnsafe(in subList._subList, ref dataBuffer,
                    out listData);
            }

            public static void EnsureCapacity(ref SubList<T> subList,
                ref NativeList<T> dataBuffer,
                ref NativeList<IndexRange> freeIndexRanges, int newCapacity)
            {
                SubList.EnsureCapacity(ref subList._subList, ref dataBuffer,
                    ref freeIndexRanges, newCapacity);
            }

            public static void Add(ref SubList<T> subList, ref NativeList<T> dataBuffer,
                ref NativeList<IndexRange> freeIndexRanges, T element)
            {
                SubList.Add(ref subList._subList, ref dataBuffer, ref freeIndexRanges,
                    element);
            }

            public static void RemoveAt(ref SubList<T> subList, ref NativeList<T> dataBuffer,
                int atIndex)
            {
                SubList.RemoveAt(ref subList._subList, ref dataBuffer, atIndex);
            }

            public static void RemoveAtSwapBack(ref SubList<T> subList,
                ref NativeList<T> dataBuffer, int atIndex)
            {
                SubList.RemoveAtSwapBack(ref subList._subList, ref dataBuffer,
                    atIndex);
            }

            public static void Free(ref SubList<T> subList, ref NativeList<T> dataBuffer,
                ref NativeList<IndexRange> freeIndexRanges)
            {
                SubList.Clear(ref subList._subList, ref dataBuffer,
                    ref freeIndexRanges);
            }

            #endregion

            #region DynamicBuffer PoolList

            public static SubList<T> Create(ref DynamicBuffer<T> dataBuffer,
                ref DynamicBuffer<IndexRange> freeIndexRanges,
                int capacity, float poolListGrowFactor = 1.5f, float poolGrowFactor = 1.5f)
            {
                return new SubList<T>()
                {
                    _subList = SubList.Create(ref dataBuffer, ref freeIndexRanges, capacity,
                        poolListGrowFactor,
                        poolGrowFactor),
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T GetElement(in SubList<T> subList, ref DynamicBuffer<T> dataBuffer,
                int index)
            {
                return SubList.GetElement(in subList._subList, ref dataBuffer, index);
            }

            public static void ResolveUnsafe(in SubList<T> subList,
                ref DynamicBuffer<T> dataBuffer,
                out UnsafeList<T> listData)
            {
                SubList.ResolveUnsafe(in subList._subList, ref dataBuffer,
                    out listData);
            }

            public static void EnsureCapacity(ref SubList<T> subList,
                ref DynamicBuffer<T> dataBuffer,
                ref DynamicBuffer<IndexRange> freeIndexRanges, int newCapacity)
            {
                SubList.EnsureCapacity(ref subList._subList, ref dataBuffer,
                    ref freeIndexRanges, newCapacity);
            }

            public static void Add(ref SubList<T> subList, ref DynamicBuffer<T> dataBuffer,
                ref DynamicBuffer<IndexRange> freeIndexRanges, T element)
            {
                SubList.Add(ref subList._subList, ref dataBuffer, ref freeIndexRanges,
                    element);
            }

            public static void RemoveAt(ref SubList<T> subList, ref DynamicBuffer<T> dataBuffer,
                int atIndex)
            {
                SubList.RemoveAt(ref subList._subList, ref dataBuffer, atIndex);
            }

            public static void RemoveAtSwapBack(ref SubList<T> subList,
                ref DynamicBuffer<T> dataBuffer, int atIndex)
            {
                SubList.RemoveAtSwapBack(ref subList._subList, ref dataBuffer,
                    atIndex);
            }

            public static void Free(ref SubList<T> subList, ref DynamicBuffer<T> dataBuffer,
                ref DynamicBuffer<IndexRange> freeIndexRanges)
            {
                SubList.Clear(ref subList._subList, ref dataBuffer,
                    ref freeIndexRanges);
            }

            #endregion

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool CheckIndexValid(in SubList<T> subList, int index)
            {
                return SubList.CheckIndexValid(in subList._subList, index);
            }
        }

        /// <summary>
        /// A list that occupies a contiguous range of indexes in a pool. It if grows, it could be reallocated
        /// elsewhere in the pool
        /// </summary>
        public struct SubList
        {
            private int _length;
            private float _growFactor;
            private float _poolGrowFactor;
            private IndexRange _rangeInPool;

            public int Length => _length;
            public int Capacity => _rangeInPool.Length;
            public bool IsCreated => _rangeInPool.Length > 0;

            #region NativeList PoolList

            public static SubList Create<T>(ref NativeList<T> dataBuffer,
                ref NativeList<IndexRange> freeIndexRanges,
                int capacity, float poolListGrowFactor = 1.5f, float poolGrowFactor = 1.5f)
                where T : unmanaged
            {
                if (capacity < 1)
                {
                    throw new ArgumentException("Capacity must be greater than 0.");
                }

                FreeRangesPool.AddRange(ref dataBuffer, ref freeIndexRanges, capacity,
                    out int firstElementIndex, poolGrowFactor);

                return new SubList
                {
                    _length = 0,
                    _growFactor = poolListGrowFactor,
                    _poolGrowFactor = poolGrowFactor,
                    _rangeInPool = new IndexRange
                    {
                        Start = firstElementIndex,
                        Length = capacity,
                    },
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T GetElement<T>(in SubList subList, ref NativeList<T> dataBuffer,
                int index)
                where T : unmanaged
            {
                if (CheckIndexValid(in subList, index))
                {
                    return dataBuffer[subList._rangeInPool.Start + index];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }

            public static unsafe void ResolveUnsafe<T>(in SubList subList,
                ref NativeList<T> dataBuffer,
                out UnsafeList<T> listData)
                where T : unmanaged
            {
                listData = new UnsafeList<T>(dataBuffer.GetUnsafePtr() + (long)subList._rangeInPool.Start,
                    subList.Length);
            }

            public static unsafe void EnsureCapacity<T>(ref SubList subList,
                ref NativeList<T> dataBuffer,
                ref NativeList<IndexRange> freeIndexRanges, int newCapacity)
                where T : unmanaged
            {
                if (newCapacity > subList.Capacity)
                {
                    IndexRange initialRangeInPool = subList._rangeInPool;

                    // First, free the original range
                    // NOTE: we do not clear the data, because we have to copy it to the new location
                    FreeRangesPool.RemoveRange(ref dataBuffer, ref freeIndexRanges, initialRangeInPool.Start,
                        initialRangeInPool.Length, false);

                    // Then, find a new range to accomodate the new capacity. This could potentially overlap the
                    // initial range
                    FreeRangesPool.AddRange(ref dataBuffer, ref freeIndexRanges, newCapacity,
                        out subList._rangeInPool.Start, subList._poolGrowFactor);
                    subList._rangeInPool.Length = newCapacity;

                    // Then copy the initial data to the new location, unless the data start index didn't change
                    if (subList._rangeInPool.Start != initialRangeInPool.Start)
                    {
                        T* dataBufferPtr = dataBuffer.GetUnsafePtr();
                        void* dst = UnsafeUtility.AddressOf(
                            ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr,
                                subList._rangeInPool.Start));
                        void* src = UnsafeUtility.AddressOf(
                            ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr, initialRangeInPool.Start));
                        UnsafeUtility.MemMove(
                            dst,
                            src,
                            UnsafeUtility.SizeOf<T>() * initialRangeInPool.Length);
                    }
                }
            }

            public static void Add<T>(ref SubList subList, ref NativeList<T> dataBuffer,
                ref NativeList<IndexRange> freeIndexRanges, T element)
                where T : unmanaged
            {
                // Check resize
                int newLength = subList.Length + 1;
                if (newLength > subList.Capacity)
                {
                    EnsureCapacity(
                        ref subList,
                        ref dataBuffer,
                        ref freeIndexRanges,
                        math.max((int)math.ceil(subList.Capacity * subList._growFactor),
                            newLength));
                }

                // Add element
                dataBuffer[subList._rangeInPool.Start + subList.Length] = element;
                subList._length++;
            }

            public static unsafe void RemoveAt<T>(ref SubList subList,
                ref NativeList<T> dataBuffer, int atIndex)
                where T : unmanaged
            {
                if (CheckIndexValid(in subList, atIndex))
                {
                    int elemsCountAfterRemovedElement = subList.Length - atIndex - 1;
                    if (elemsCountAfterRemovedElement > 0)
                    {
                        T* dataBufferPtr = dataBuffer.GetUnsafePtr();
                        void* dst = UnsafeUtility.AddressOf(
                            ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr,
                                subList._rangeInPool.Start + atIndex));
                        void* src = UnsafeUtility.AddressOf(
                            ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr,
                                subList._rangeInPool.Start + atIndex + 1));
                        UnsafeUtility.MemMove(
                            dst,
                            src,
                            UnsafeUtility.SizeOf<T>() * elemsCountAfterRemovedElement);
                    }

                    dataBuffer[subList._rangeInPool.Start + subList.Length - 1] = default;
                    subList._length--;
                }
            }

            public static void RemoveAtSwapBack<T>(ref SubList subList,
                ref NativeList<T> dataBuffer, int atIndex)
                where T : unmanaged
            {
                if (CheckIndexValid(in subList, atIndex))
                {
                    dataBuffer[subList._rangeInPool.Start + atIndex] =
                        dataBuffer[subList._rangeInPool.Start + subList.Length - 1];
                    dataBuffer[subList._rangeInPool.Start + subList.Length - 1] = default;
                    subList._length--;
                }
            }

            public static void Clear<T>(ref SubList subList, ref NativeList<T> dataBuffer,
                ref NativeList<IndexRange> freeIndexRanges)
                where T : unmanaged
            {
                FreeRangesPool.RemoveRange(ref dataBuffer, ref freeIndexRanges, subList._rangeInPool.Start,
                    subList._rangeInPool.Length, true);
            }

            #endregion

            #region DynamicBuffer PoolList

            public static SubList Create<T>(ref DynamicBuffer<T> dataBuffer,
                ref DynamicBuffer<IndexRange> freeIndexRanges,
                int capacity, float poolListGrowFactor = 1.5f, float poolGrowFactor = 1.5f)
                where T : unmanaged
            {
                if (capacity < 1)
                {
                    throw new ArgumentException("Capacity must be greater than 0.");
                }

                FreeRangesPool.AddRange(ref dataBuffer, ref freeIndexRanges, capacity,
                    out int firstElementIndex, poolGrowFactor);

                return new SubList
                {
                    _length = 0,
                    _growFactor = poolListGrowFactor,
                    _poolGrowFactor = poolGrowFactor,
                    _rangeInPool = new IndexRange
                    {
                        Start = firstElementIndex,
                        Length = capacity,
                    },
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T GetElement<T>(in SubList subList, ref DynamicBuffer<T> dataBuffer,
                int index)
                where T : unmanaged
            {
                if (CheckIndexValid(in subList, index))
                {
                    return dataBuffer[subList._rangeInPool.Start + index];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }

            public static unsafe void ResolveUnsafe<T>(in SubList subList,
                ref DynamicBuffer<T> dataBuffer,
                out UnsafeList<T> listData)
                where T : unmanaged
            {
                listData = new UnsafeList<T>(
                    (T*)dataBuffer.GetUnsafePtr() + (long)subList._rangeInPool.Start,
                    subList.Length);
            }

            public static unsafe void EnsureCapacity<T>(ref SubList subList,
                ref DynamicBuffer<T> dataBuffer,
                ref DynamicBuffer<IndexRange> freeIndexRanges, int newCapacity)
                where T : unmanaged
            {
                if (newCapacity > subList.Capacity)
                {
                    IndexRange initialRangeInPool = subList._rangeInPool;

                    // First, free the original range
                    // NOTE: we do not clear the data, because we have to copy it to the new location
                    FreeRangesPool.RemoveRange(ref dataBuffer, ref freeIndexRanges, initialRangeInPool.Start,
                        initialRangeInPool.Length, false);

                    // Then, find a new range to accomodate the new capacity. This could potentially overlap the
                    // initial range
                    FreeRangesPool.AddRange(ref dataBuffer, ref freeIndexRanges, newCapacity,
                        out subList._rangeInPool.Start, subList._poolGrowFactor);
                    subList._rangeInPool.Length = newCapacity;

                    // Then copy the initial data to the new location, unless the data start index didn't change
                    if (subList._rangeInPool.Start != initialRangeInPool.Start)
                    {
                        T* dataBufferPtr = (T*)dataBuffer.GetUnsafePtr();
                        void* dst = UnsafeUtility.AddressOf(
                            ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr,
                                subList._rangeInPool.Start));
                        void* src = UnsafeUtility.AddressOf(
                            ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr, initialRangeInPool.Start));
                        UnsafeUtility.MemMove(
                            dst,
                            src,
                            UnsafeUtility.SizeOf<T>() * initialRangeInPool.Length);
                    }
                }
            }

            public static void Add<T>(ref SubList subList, ref DynamicBuffer<T> dataBuffer,
                ref DynamicBuffer<IndexRange> freeIndexRanges, T element)
                where T : unmanaged
            {
                // Check resize
                int newLength = subList.Length + 1;
                if (newLength > subList.Capacity)
                {
                    EnsureCapacity(
                        ref subList,
                        ref dataBuffer,
                        ref freeIndexRanges,
                        math.max((int)math.ceil(subList.Capacity * subList._growFactor),
                            newLength));
                }

                // Add element
                dataBuffer[subList._rangeInPool.Start + subList.Length] = element;
                subList._length++;
            }

            public static unsafe void RemoveAt<T>(ref SubList subList,
                ref DynamicBuffer<T> dataBuffer, int atIndex)
                where T : unmanaged
            {
                if (CheckIndexValid(in subList, atIndex))
                {
                    int elemsCountAfterRemovedElement = subList.Length - atIndex - 1;
                    if (elemsCountAfterRemovedElement > 0)
                    {
                        T* dataBufferPtr = (T*)dataBuffer.GetUnsafePtr();
                        void* dst = UnsafeUtility.AddressOf(
                            ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr,
                                subList._rangeInPool.Start + atIndex));
                        void* src = UnsafeUtility.AddressOf(
                            ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr,
                                subList._rangeInPool.Start + atIndex + 1));
                        UnsafeUtility.MemMove(
                            dst,
                            src,
                            UnsafeUtility.SizeOf<T>() * elemsCountAfterRemovedElement);
                    }

                    dataBuffer[subList._rangeInPool.Start + subList.Length - 1] = default;
                    subList._length--;
                }
            }

            public static void RemoveAtSwapBack<T>(ref SubList subList,
                ref DynamicBuffer<T> dataBuffer, int atIndex)
                where T : unmanaged
            {
                if (CheckIndexValid(in subList, atIndex))
                {
                    dataBuffer[subList._rangeInPool.Start + atIndex] =
                        dataBuffer[subList._rangeInPool.Start + subList.Length - 1];
                    dataBuffer[subList._rangeInPool.Start + subList.Length - 1] = default;
                    subList._length--;
                }
            }

            public static void Clear<T>(ref SubList subList, ref DynamicBuffer<T> dataBuffer,
                ref DynamicBuffer<IndexRange> freeIndexRanges)
                where T : unmanaged
            {
                FreeRangesPool.RemoveRange(ref dataBuffer, ref freeIndexRanges, subList._rangeInPool.Start,
                    subList._rangeInPool.Length, true);
            }

            #endregion

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool CheckIndexValid(in SubList subList, int index)
            {
                return index >= 0 && index < subList._rangeInPool.Start + subList.Length;
            }
        }

        #endregion
    }
    
    */
}