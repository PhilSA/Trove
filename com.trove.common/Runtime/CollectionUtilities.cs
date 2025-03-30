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

    public interface ICompactMultiLinkedListElement
    {
        public int PrevElementIndex { get; set; }
    }

    /// <summary>
    /// A list that occupies a contiguous range of indexes in a pool. It if grows, it could be reallocated
    /// elsewhere in the pool
    /// </summary>
    public struct PoolList<T> where T : unmanaged
    {
        private int _length;
        private float _growFactor;
        private float _poolGrowFactor;
        private IndexRange _rangeInPool;

        public int Length => _length;
        public int Capacity => _rangeInPool.Length;
        public bool IsCreated => _rangeInPool.Length > 0;

        #region NativeList PoolList
        public static PoolList<T> Create(ref NativeList<T> dataBuffer, ref NativeList<IndexRange> freeIndexRanges,
            int capacity, float poolListGrowFactor = 1.5f, float poolGrowFactor = 1.5f)
        {
            if (capacity < 1)
            {
                throw new ArgumentException("Capacity must be greater than 0.");
            }

            CollectionUtilities.PoolAddRange(ref dataBuffer, ref freeIndexRanges, capacity,
                out int firstElementIndex, poolGrowFactor);

            return new PoolList<T>
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
        public static T GetElement(in PoolList<T> poolList, ref NativeList<T> dataBuffer, int index)
        {
            if (CheckIndexValid(in poolList, index))
            {
                return dataBuffer[poolList._rangeInPool.Start + index];
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public static unsafe void ResolveUnsafe(in PoolList<T> poolList, ref NativeList<T> dataBuffer,
            out UnsafeList<T> listData)
        {
            listData = new UnsafeList<T>(dataBuffer.GetUnsafePtr() + (long)poolList._rangeInPool.Start,
                poolList.Length);
        }

        public static unsafe void EnsureCapacity(ref PoolList<T> poolList, ref NativeList<T> dataBuffer,
            ref NativeList<IndexRange> freeIndexRanges, int newCapacity)
        {
            if (newCapacity > poolList.Capacity)
            {
                IndexRange initialRangeInPool = poolList._rangeInPool;

                // First, free the original range
                // NOTE: we do not clear the data, because we have to copy it to the new location
                CollectionUtilities.PoolRemoveRange(ref dataBuffer, ref freeIndexRanges, initialRangeInPool.Start,
                    initialRangeInPool.Length, false);

                // Then, find a new range to accomodate the new capacity. This could potentially overlap the
                // initial range
                CollectionUtilities.PoolAddRange(ref dataBuffer, ref freeIndexRanges, newCapacity,
                    out poolList._rangeInPool.Start, poolList._poolGrowFactor);
                poolList._rangeInPool.Length = newCapacity;

                // Then copy the initial data to the new location, unless the data start index didn't change
                if (poolList._rangeInPool.Start != initialRangeInPool.Start)
                {
                    T* dataBufferPtr = dataBuffer.GetUnsafePtr();
                    void* dst = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr, poolList._rangeInPool.Start));
                    void* src = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr, initialRangeInPool.Start));
                    UnsafeUtility.MemMove(
                        dst,
                        src,
                        UnsafeUtility.SizeOf<T>() * initialRangeInPool.Length);
                }
            }
        }

        public static void Add(ref PoolList<T> poolList, ref NativeList<T> dataBuffer,
            ref NativeList<IndexRange> freeIndexRanges, T element)
        {
            // Check resize
            int newLength = poolList.Length + 1;
            if (newLength > poolList.Capacity)
            {
                EnsureCapacity(
                    ref poolList, 
                    ref dataBuffer,
                    ref freeIndexRanges, 
                    math.max((int)math.ceil(poolList.Capacity * poolList._growFactor), newLength));
            }

            // Add element
            dataBuffer[poolList._rangeInPool.Start + poolList.Length] = element;
            poolList._length++;
        }

        public static unsafe void RemoveAt(ref PoolList<T> poolList, ref NativeList<T> dataBuffer, int atIndex)
        {
            if (CheckIndexValid(in poolList, atIndex))
            {
                int elemsCountAfterRemovedElement = poolList.Length - atIndex - 1;
                if (elemsCountAfterRemovedElement > 0)
                {
                    T* dataBufferPtr = dataBuffer.GetUnsafePtr();
                    void* dst = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr, poolList._rangeInPool.Start + atIndex));
                    void* src = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr,
                            poolList._rangeInPool.Start + atIndex + 1));
                    UnsafeUtility.MemMove(
                        dst,
                        src,
                        UnsafeUtility.SizeOf<T>() * elemsCountAfterRemovedElement);
                }

                dataBuffer[poolList._rangeInPool.Start + poolList.Length - 1] = default;
                poolList._length--;
            }
        }

        public static void RemoveAtSwapBack(ref PoolList<T> poolList, ref NativeList<T> dataBuffer, int atIndex)
        {
            if (CheckIndexValid(in poolList, atIndex))
            {
                dataBuffer[poolList._rangeInPool.Start + atIndex] =
                    dataBuffer[poolList._rangeInPool.Start + poolList.Length - 1];
                dataBuffer[poolList._rangeInPool.Start + poolList.Length - 1] = default;
                poolList._length--;
            }
        }

        public static void Free(ref PoolList<T> poolList, ref NativeList<T> dataBuffer,
            ref NativeList<IndexRange> freeIndexRanges)
        {
            CollectionUtilities.PoolRemoveRange(ref dataBuffer, ref freeIndexRanges, poolList._rangeInPool.Start,
                poolList._rangeInPool.Length, true);
        }
        #endregion

        #region DynamicBuffer PoolList
        public static PoolList<T> Create(ref DynamicBuffer<T> dataBuffer, ref DynamicBuffer<IndexRange> freeIndexRanges,
            int capacity, float poolListGrowFactor = 1.5f, float poolGrowFactor = 1.5f)
        {
            if (capacity < 1)
            {
                throw new ArgumentException("Capacity must be greater than 0.");
            }

            CollectionUtilities.PoolAddRange(ref dataBuffer, ref freeIndexRanges, capacity,
                out int firstElementIndex, poolGrowFactor);

            return new PoolList<T>
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
        public static T GetElement(in PoolList<T> poolList, ref DynamicBuffer<T> dataBuffer, int index)
        {
            if (CheckIndexValid(in poolList, index))
            {
                return dataBuffer[poolList._rangeInPool.Start + index];
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public static unsafe void ResolveUnsafe(in PoolList<T> poolList, ref DynamicBuffer<T> dataBuffer,
            out UnsafeList<T> listData)
        {
            listData = new UnsafeList<T>((T*)dataBuffer.GetUnsafePtr() + (long)poolList._rangeInPool.Start,
                poolList.Length);
        }

        public static unsafe void EnsureCapacity(ref PoolList<T> poolList, ref DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRanges, int newCapacity)
        {
            if (newCapacity > poolList.Capacity)
            {
                IndexRange initialRangeInPool = poolList._rangeInPool;

                // First, free the original range
                // NOTE: we do not clear the data, because we have to copy it to the new location
                CollectionUtilities.PoolRemoveRange(ref dataBuffer, ref freeIndexRanges, initialRangeInPool.Start,
                    initialRangeInPool.Length, false);

                // Then, find a new range to accomodate the new capacity. This could potentially overlap the
                // initial range
                CollectionUtilities.PoolAddRange(ref dataBuffer, ref freeIndexRanges, newCapacity,
                    out poolList._rangeInPool.Start, poolList._poolGrowFactor);
                poolList._rangeInPool.Length = newCapacity;

                // Then copy the initial data to the new location, unless the data start index didn't change
                if (poolList._rangeInPool.Start != initialRangeInPool.Start)
                {
                    T* dataBufferPtr = (T*)dataBuffer.GetUnsafePtr();
                    void* dst = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr, poolList._rangeInPool.Start));
                    void* src = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr, initialRangeInPool.Start));
                    UnsafeUtility.MemMove(
                        dst,
                        src,
                        UnsafeUtility.SizeOf<T>() * initialRangeInPool.Length);
                }
            }
        }

        public static void Add(ref PoolList<T> poolList, ref DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRanges, T element)
        {
            // Check resize
            int newLength = poolList.Length + 1;
            if (newLength > poolList.Capacity)
            {
                EnsureCapacity(
                    ref poolList, 
                    ref dataBuffer,
                    ref freeIndexRanges, 
                    math.max((int)math.ceil(poolList.Capacity * poolList._growFactor), newLength));
            }

            // Add element
            dataBuffer[poolList._rangeInPool.Start + poolList.Length] = element;
            poolList._length++;
        }

        public static unsafe void RemoveAt(ref PoolList<T> poolList, ref DynamicBuffer<T> dataBuffer, int atIndex)
        {
            if (CheckIndexValid(in poolList, atIndex))
            {
                int elemsCountAfterRemovedElement = poolList.Length - atIndex - 1;
                if (elemsCountAfterRemovedElement > 0)
                {
                    T* dataBufferPtr = (T*)dataBuffer.GetUnsafePtr();
                    void* dst = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr, poolList._rangeInPool.Start + atIndex));
                    void* src = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>(dataBufferPtr,
                            poolList._rangeInPool.Start + atIndex + 1));
                    UnsafeUtility.MemMove(
                        dst,
                        src,
                        UnsafeUtility.SizeOf<T>() * elemsCountAfterRemovedElement);
                }

                dataBuffer[poolList._rangeInPool.Start + poolList.Length - 1] = default;
                poolList._length--;
            }
        }

        public static void RemoveAtSwapBack(ref PoolList<T> poolList, ref DynamicBuffer<T> dataBuffer, int atIndex)
        {
            if (CheckIndexValid(in poolList, atIndex))
            {
                dataBuffer[poolList._rangeInPool.Start + atIndex] =
                    dataBuffer[poolList._rangeInPool.Start + poolList.Length - 1];
                dataBuffer[poolList._rangeInPool.Start + poolList.Length - 1] = default;
                poolList._length--;
            }
        }

        public static void Free(ref PoolList<T> poolList, ref DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRanges)
        {
            CollectionUtilities.PoolRemoveRange(ref dataBuffer, ref freeIndexRanges, poolList._rangeInPool.Start,
                poolList._rangeInPool.Length, true);
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckIndexValid(in PoolList<T> poolList, int index)
        {
            return index >= 0 && index < poolList._rangeInPool.Start + poolList.Length;
        }
    }

    /// <summary>
    /// NOTE: becomes invalid as soon as either collections are modified
    /// </summary>
    public unsafe struct UnsafePoolIterator<T> where T : unmanaged
    {
        private T* _datasPtr;
        private int _datasLength;
        private IndexRange* _freeIndexRangesPtr;
        private int _freeIndexRangesLength;
        private int _iteratedElementIndex;
        private int _iteratedFreeRangeIndex;
        
        public UnsafePoolIterator(T* datasPtr, int datasLength, IndexRange* freeIndexRangesPtr, int freeIndexRangesLength)
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

    /// <summary>
    /// Iterates a specified linked list in a dynamic buffer containing multiple linked lists.
    /// Also allows removing elements during iteration.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct CompactMultiLinkedListIterator<T> where T : unmanaged, ICompactMultiLinkedListElement
    {
        private int _iteratedElementIndex;
        private int _prevIteratedElementIndex;
        private T _iteratedElement;
        
        /// <summary>
        /// Create the iterator
        /// </summary>
        public CompactMultiLinkedListIterator(int linkedListLastIndex)
        {
            _iteratedElementIndex = linkedListLastIndex;
            _prevIteratedElementIndex = -1;
            _iteratedElement = default;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetNext(in DynamicBuffer<T> multiLinkedListsBuffer, out T element, out int elementIndex)
        {
            if (_iteratedElementIndex >= 0)
            {
                _iteratedElement = multiLinkedListsBuffer[_iteratedElementIndex];

                element = _iteratedElement;
                elementIndex = _iteratedElementIndex;
                
                // Move to next index but remember previous (used for removing)
                _prevIteratedElementIndex = _iteratedElementIndex;
                _iteratedElementIndex = _iteratedElement.PrevElementIndex;
                
                return true;
            }

            element = default;
            elementIndex = -1;
            return false;
        }

        /// <summary>
        /// Note: will update the last indexes in the linkedListLastIndexes following removal.
        /// Note: GetNext() must be called before this can be used.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveCurrentIteratedElementAndUpdateIndexes(
            ref DynamicBuffer<T> multiLinkedListsBuffer, 
            ref NativeArray<int> linkedListLastIndexes,
            out int firstUpdatedLastIndexIndex)
        {
            firstUpdatedLastIndexIndex = -1;
            int removedElementIndex = _prevIteratedElementIndex;

            if (removedElementIndex < 0)
            {
                return;
            }

            T removedElement = _iteratedElement;
            
            // Remove element
            multiLinkedListsBuffer.RemoveAt(removedElementIndex);

            // Iterate all last indexes and update them 
            for (int i = 0; i < linkedListLastIndexes.Length; i++)
            {
                int tmpLastIndex = linkedListLastIndexes[i];
                
                // If the iterated last index is greater than the removed index, decrement it
                if (tmpLastIndex > removedElementIndex)
                {
                    tmpLastIndex -= 1;
                    linkedListLastIndexes[i] = tmpLastIndex;
                    if (firstUpdatedLastIndexIndex < 0)
                    {
                        firstUpdatedLastIndexIndex = i;
                    }
                }
                // If the iterated last index is the one we removed, update it with the prev index of the removed element
                else if (tmpLastIndex == removedElementIndex)
                {
                    linkedListLastIndexes[i] = removedElement.PrevElementIndex;
                    if (firstUpdatedLastIndexIndex < 0)
                    {
                        firstUpdatedLastIndexIndex = i;
                    }
                }
            }

            // Iterate all buffer elements starting from the removed index to update their prev indexes
            for (int i = _iteratedElementIndex; i < multiLinkedListsBuffer.Length; i++)
            {
                T iteratedElement = multiLinkedListsBuffer[i];
                
                // If the prev index of this element is greater than the removed one, decrement it
                if (iteratedElement.PrevElementIndex > removedElementIndex)
                {
                    iteratedElement.PrevElementIndex -= 1;
                    multiLinkedListsBuffer[i] = iteratedElement;
                }
                // If the prev index of this element was the removed one, change its prev index to the removed one's
                // prev index.
                else if (iteratedElement.PrevElementIndex == removedElementIndex)
                {
                    iteratedElement.PrevElementIndex = removedElement.PrevElementIndex;
                    multiLinkedListsBuffer[i] = iteratedElement;
                }
            }
        }
    }
    
    public static class CollectionUtilities
    {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddToCompactMultiLinkedList<T>(ref DynamicBuffer<T> multiLinkedListBuffer,
            ref int listLastElementIndex, T addedElement)
            where T : unmanaged, ICompactMultiLinkedListElement
        {
            // Add element at the end of the buffer, and remember the previous element index
            int addIndex = multiLinkedListBuffer.Length;
            addedElement.PrevElementIndex = listLastElementIndex;
            multiLinkedListBuffer.Add(addedElement);

            // Update the last element index
            listLastElementIndex = addIndex;
        }

        #region NativeList Pool
        public static void PoolInit<T>(ref NativeList<T> dataBuffer, ref NativeList<IndexRange> freeIndexRanges,
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
        public static unsafe void PoolResize<T>(ref NativeList<T> dataBuffer, ref NativeList<IndexRange> freeIndexRanges,
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

        public static int PoolCalculateElementsCount<T>(in NativeList<T> dataBuffer,ref NativeList<IndexRange> freeIndexRange)
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

        public static void PoolAdd<T>(ref NativeList<T> dataBuffer,
            ref NativeList<IndexRange> freeIndexRanges, in T element, out int elementIndex,
            float growFactor = 1.5f)
            where T : unmanaged
        {
            PoolAddRange(ref dataBuffer, ref freeIndexRanges, 1, out elementIndex, growFactor);
            dataBuffer[elementIndex] = element;
        }

        public static unsafe void PoolAddRange<T>(ref NativeList<T> dataBuffer,
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
            PoolResize(ref dataBuffer, ref freeIndexRanges, newCapacity);

            int lastRangeIndex = freeIndexRanges.Length - 1;
            ref IndexRange lastIndexRange =
                ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRanges.GetUnsafePtr(),
                    lastRangeIndex);
 
            if (!(lastIndexRange.Length >= rangeLength))
            {
                int a = 0;
            }
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

        public static void PoolRemove<T>(ref NativeList<T> dataBuffer, ref NativeList<IndexRange> freeIndexRanges, 
            int elementIndex)
            where T : unmanaged
        {
            PoolRemoveRange(ref dataBuffer, ref freeIndexRanges, elementIndex, 1, true);
        }

        /// <summary>
        /// NOTE: guaranteed to free the specified range even if includes already freed indexes
        /// </summary>
        public static unsafe void PoolRemoveRange<T>(ref NativeList<T> dataBuffer,
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
                if (MathUtilities.RangesOverlap(rangeStartIndex, rangeLength, iteratedIndexRange.Start, iteratedIndexRange.Length) ||
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

        public static unsafe UnsafePoolIterator<T> GetPoolIterator<T>(NativeList<T> dataBuffer,
            NativeList<IndexRange> freeIndexRanges)
            where T : unmanaged
        {
            return new UnsafePoolIterator<T>(dataBuffer.GetUnsafeReadOnlyPtr(), dataBuffer.Length,
                freeIndexRanges.GetUnsafeReadOnlyPtr(), freeIndexRanges.Length);
        }
        #endregion
        
        #region DynamicBuffer Pool
        public static void PoolInit<T>(ref DynamicBuffer<T> dataBuffer, ref DynamicBuffer<IndexRange> freeIndexRanges,
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
        public static unsafe void PoolResize<T>(ref DynamicBuffer<T> dataBuffer, ref DynamicBuffer<IndexRange> freeIndexRanges,
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

        public static int PoolCalculateElementsCount<T>(in DynamicBuffer<T> dataBuffer,ref DynamicBuffer<IndexRange> freeIndexRange)
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

        public static void PoolAdd<T>(ref DynamicBuffer<T> dataBuffer,
            ref DynamicBuffer<IndexRange> freeIndexRanges, in T element, out int elementIndex, float growFactor = 1.5f)
            where T : unmanaged
        {
            PoolAddRange(ref dataBuffer, ref freeIndexRanges, 1, out elementIndex, growFactor);
            dataBuffer[elementIndex] = element;
        }

        public static unsafe void PoolAddRange<T>(ref DynamicBuffer<T> dataBuffer,
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
            PoolResize(ref dataBuffer, ref freeIndexRanges, newCapacity);

            int lastRangeIndex = freeIndexRanges.Length - 1;
            ref IndexRange lastIndexRange =
                ref UnsafeUtility.ArrayElementAsRef<IndexRange>(freeIndexRanges.GetUnsafePtr(),
                    lastRangeIndex);
 
            if (!(lastIndexRange.Length >= rangeLength))
            {
                int a = 0;
            }
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

        public static void PoolRemove<T>(ref DynamicBuffer<T> dataBuffer, ref DynamicBuffer<IndexRange> freeIndexRanges, 
            int elementIndex)
            where T : unmanaged
        {
            PoolRemoveRange(ref dataBuffer, ref freeIndexRanges, elementIndex, 1, true);
        }

        /// <summary>
        /// NOTE: guaranteed to free the specified range even if includes already freed indexes
        /// </summary>
        public static unsafe void PoolRemoveRange<T>(ref DynamicBuffer<T> dataBuffer,
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
                if (MathUtilities.RangesOverlap(rangeStartIndex, rangeLength, iteratedIndexRange.Start, iteratedIndexRange.Length) ||
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

        public static unsafe UnsafePoolIterator<T> GetPoolIterator<T>(DynamicBuffer<T> dataBuffer,
            DynamicBuffer<IndexRange> freeIndexRanges)
            where T : unmanaged
        {
            return new UnsafePoolIterator<T>((T*)dataBuffer.GetUnsafeReadOnlyPtr(), dataBuffer.Length,
                (IndexRange*)freeIndexRanges.GetUnsafeReadOnlyPtr(), freeIndexRanges.Length);
        }
        #endregion
    }
}