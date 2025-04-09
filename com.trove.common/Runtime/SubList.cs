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
     * The main use case for sub-lists is to provide a solution to the lack of nested collections on entities. Imagine
     * you have a `DynamicBuffer<Item>`, and each `Item` needs a list of `Effect`s, and `Item`s will gain and lose
     * `Effect`s during play. You could choose to give each `Item` an Entity that stores a `DynamicBuffer<Effect>`,
     * but then you have to pay the price of a buffer lookup for each item when accessing `Effect`s. You could choose
     * to store `Effect`s in a `FixedList` in `Item`, but the storage size of that `FixedList` would be limited, and
     * it would no doubt make iterating your `Item`s less efficient if you pick a worst-case-scenario `FixedList` size.
     *
     * With sub-lists, we can solve this problem without paying the price of buffer lookups and without the limitations
     * of FixedLists. Sub-lists use a single buffer in order to store multiple individual lists. In the use case above,
     * each `Item` buffer element would have a sub-list field, and this sub-list would allow them to store a list of
     * `Effect` in a separate `DynamicBuffer<Effect>` on the entity. The `DynamicBuffer<Effect>` would contain all the
     * `Effect`s of all the `Item`s, but special sub-list iterators would allow iterating only the `Effect`s of specific
     * `Item`s in that buffer (without having to check each `Effect` in the buffer to see which `Item` it belongs to).
     *
     * IMPORTANT: when using sub-lists, you must only ever add or remove elements to/from buffers using the sub-list APIs.
     *            You must also never change the data of sub-list interface properties in buffer elements.
     *
     * Different types of sub-lists are available, each with their pros and cons.
     */

    #region SubList

    public interface ISubListElement
    {
        public byte IsOccupied { get; set; }
    }
    
    /// <summary>
    /// Allows storing multiple growable lists in the same list/buffer.
    /// - Sub-list elements are contiguous in memory.
    /// - Sub-list element indexes can change when list grows
    /// - Great sub-list element add and remove performance.
    /// - The encompassing buffer will grow whenever a new sub-list is created in the buffer, or when an existing
    ///   sub-list needs to reallocate because it grew past capacity.
    ///
    /// This type of sub-list is best suited when sub-list element iteration performance is key, due to sub-list
    /// elements being contiguous in memory. Or when element add/remove performance is key.
    ///
    /// How it works:
    /// - Each sub-list reserves a range a contiguous indexes in the buffer, equivalent to capacity.
    /// - When length would exceed capacity, the sub-list capacity is resized and the sub-list elements are moved to
    ///   another indexes range that can accomodate the new capacity.
    /// - When resizing capacity like this, we first iterate the buffer in order to find an existing free range that
    ///   could accomodate the capacity. If an existing range is not found, we increase the buffer length to accomodate it.
    /// </summary>
    public struct SubList
    {
        public int Length;
        public int Capacity;
        public float GrowFactor;
        public int ElementsStartIndex;
        public byte IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckCreated(in SubList subList)
        {
            if (subList.IsCreated == 0)
            {
                throw new Exception($"Error: SubList is not created.");
            }
        }
        
        private static void Create<T>(ref SubList subList, ref DynamicBuffer<T> buffer, int initialCapacity, float growFactor = 1.5f)
            where T : unmanaged, ISubListElement
        {
            if (subList.IsCreated == 0)
            {
                subList.Length = 0;
                subList.Capacity = 0;
                subList.GrowFactor = growFactor;
                subList.ElementsStartIndex = -1;

                SetCapacity(ref subList, ref buffer, initialCapacity);
                
                subList.IsCreated = 1;
            }
        }

        public static void Add<T>(ref SubList subList, ref DynamicBuffer<T> buffer, T element)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);
            
            int newLength = subList.Length + 1;
            
            // Grow capacity
            if (newLength > buffer.Capacity)
            {
                int newCapacity = math.max(
                    (int)math.ceil(subList.Capacity * subList.GrowFactor), 
                    subList.Capacity + 1);
                SetCapacity(ref subList, ref buffer, newCapacity);
            }

            element.IsOccupied = 1;
            buffer[subList.ElementsStartIndex + subList.Length] = element;
            subList.Length = newLength;
        }

        public static unsafe bool TryRemoveAt<T>(ref SubList subList, ref DynamicBuffer<T> buffer, int indexInSubList)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);

            if (indexInSubList >= 0 && indexInSubList < subList.Length)
            {
                int elementIndexInBuffer = subList.ElementsStartIndex + indexInSubList;
                int nextElementsLength = subList.Length - indexInSubList - 1;
                void* src = UnsafeUtility.AddressOf(
                    ref UnsafeUtility.ArrayElementAsRef<T>((byte*)buffer.GetUnsafePtr(), elementIndexInBuffer + 1));
                void* dst = UnsafeUtility.AddressOf(
                    ref UnsafeUtility.ArrayElementAsRef<T>((byte*)buffer.GetUnsafePtr(), elementIndexInBuffer));
                UnsafeUtility.MemMove(dst, src, nextElementsLength);
                subList.Length--;
                Assert.IsTrue(subList.Length >= 0);
                return true;
            }
            
            return false;
        }

        public static bool TryRemoveAtSwapBack<T>(ref SubList subList, ref DynamicBuffer<T> buffer, int indexInSubList)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);

            if (indexInSubList >= 0 && indexInSubList < subList.Length)
            {
                int elementIndexInBuffer = subList.ElementsStartIndex + indexInSubList;
                int lastElementIndexInBuffer = subList.ElementsStartIndex + subList.Length - 1;
                buffer[elementIndexInBuffer] = buffer[lastElementIndexInBuffer];
                subList.Length--;
                Assert.IsTrue(subList.Length >= 0);
                return true;
            }
            
            return false;
        }

        public static void Clear<T>(ref SubList subList, ref DynamicBuffer<T> buffer)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);
            subList.Length = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGet<T>(ref SubList subList, ref DynamicBuffer<T> buffer, int indexInSubList, out T element)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);
            
            if (indexInSubList >= 0 && indexInSubList < subList.Length)
            {
                int indexInBuffer = subList.ElementsStartIndex + indexInSubList;
                if (indexInBuffer < buffer.Length)
                {
                    element = buffer[indexInBuffer];
                    return true;
                }
            }

            element = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySet<T>(ref SubList subList, ref DynamicBuffer<T> buffer, int indexInSubList, T element)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);
            
            if (indexInSubList >= 0 && indexInSubList < subList.Length)
            {
                int indexInBuffer = subList.ElementsStartIndex + indexInSubList;
                if (indexInBuffer < buffer.Length)
                {
                    element.IsOccupied = 1;
                    buffer[indexInBuffer] = element;
                    return true;
                }
            }

            element = default;
            return false;
        }

        public static unsafe bool SetCapacity<T>(ref SubList subList, ref DynamicBuffer<T> buffer, int newCapacity)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);
            
            int prevCapacity = subList.Capacity;
            
            // Grow
            if (newCapacity > prevCapacity)
            {
                // Search for available free range
                int freeRangeStart = -1;
                int freeRangeLength = 0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    T iteratedElement = buffer[i];
                    if (iteratedElement.IsOccupied == 0)
                    {
                        // Detect start of free range
                        if (freeRangeStart < 0)
                        {
                            freeRangeStart = i;
                        }

                        freeRangeLength++;

                        // Detect reached required capacity
                        if (freeRangeLength >= newCapacity)
                        {
                            break;
                        }
                    }
                    // Reset free range
                    else
                    {
                        freeRangeStart = -1;
                        freeRangeLength = 0;
                    }
                }

                // If haven't found a free range, resize buffer to create one
                if (freeRangeLength < newCapacity)
                {
                    int requiredLengthDiff = newCapacity - freeRangeLength;

                    if (freeRangeStart < 0)
                    {
                        freeRangeStart = buffer.Length;
                    }
                    
                    buffer.Resize(buffer.Length + requiredLengthDiff, NativeArrayOptions.ClearMemory);
                }
                
                // Copy sub-list over to new range,
                if (subList.ElementsStartIndex >= 0 && subList.Length > 0)
                {
                    void* src = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>((byte*)buffer.GetUnsafePtr(),
                            subList.ElementsStartIndex));
                    void* dst = UnsafeUtility.AddressOf(
                        ref UnsafeUtility.ArrayElementAsRef<T>((byte*)buffer.GetUnsafePtr(), freeRangeStart));
                    UnsafeUtility.MemMove(dst, src, subList.Length);
                
                    // Free previous range
                    UnsafeUtility.MemClear(src, sizeof(T) * subList.Length);
                }

                // Mark the rest of the new capacity indexes as occupied
                for (int i = freeRangeStart + subList.Length; i < freeRangeStart + newCapacity; i++)
                {
                    buffer[i] = new T { IsOccupied = 1 };
                }
                
                // Update sublist
                subList.Capacity = newCapacity;
                subList.ElementsStartIndex = freeRangeStart;
                
                return true;
            }
            // Shrink
            else if (newCapacity < prevCapacity)
            {
                // Can't shrink more than current length
                if (newCapacity > subList.Length)
                {
                    subList.Capacity = newCapacity;
                    return true;
                }
            }

            return false;
        }

        public static void Resize<T>(ref SubList subList, ref DynamicBuffer<T> buffer, int newLength)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);
            
            // Only allow growing
            if (newLength > subList.Length)
            {
                // Check grow capacity
                if (newLength > subList.Capacity)
                {
                    SetCapacity(ref subList, ref buffer, newLength);
                }
                
                subList.Length = newLength;
            }
        }
    }

    #endregion

    #region LinkedSubList

    /// <summary>
    /// Allows storing multiple growable lists in the same list/buffer.
    /// - Sub-list elements are not contiguous in memory.
    /// - Sub-list element indexes do not change after being added.
    /// - Medium sub-list element remove performance, due to only being able to remove an element by iterating
    ///   from the last element in the sub-list. Relatively poor sub-list element add performance due to having to
    ///   search for the lowest free index in the buffer.
    /// - The encompassing buffer will grow by a certain factor as elements are added, but it will never shrink unless
    ///   <Trim> is called. But even then, it cannot shrink smaller than the highest-index element in the buffer.
    ///
    /// This type of sub-list is best suited when keeping a handle to a specific element is needed, due to the
    /// unchanging element indexes. The resulting buffer is also likely to be more compact in size than with the
    /// regular <SubList>, but it depends on the scenario.
    /// </summary>
    public struct LinkedSubList
    {
        public struct ElementHandle
        {
            public int Index;
            public int Version;
        }

        // TODO: optional FreeRanges param? Or LinkedFreeRangesSubList
    }

    #endregion

    #region CompactLinkedSubList

    public interface ICompactLinkedSubListElement
    {
        public int NextElementIndex { get; set; }
        public byte IsCreated { get; set; }
        public byte IsPinnedFirstElement { get; set; }
    }

    /// <summary>
    /// 
    /// Allows storing multiple growable lists in the same list/buffer.
    /// - Sub-list elements are not contiguous in memory.
    /// - Sub-list element indexes can change when elements are added or removed.
    /// - Medium sub-list element add and remove performance due to having to iterate elements of the sub-list until the
    ///   last one (for add) or the removed one (for remove) are found.
    /// - The encompassing buffer will always have the just exact length needed to host all sub-list elements.
    ///
    /// This type of sub-list is best suited for when minimizing total list size is key, such as for netcode buffer
    /// serialization.
    ///
    /// How it works:
    /// - The buffer starts with a series of "pinned" elements representing the first element of each sub-list. These
    ///   are "pinned" in the sense that they are never actually removed from the buffer; they are just marked as "not created"
    ///   when removed. The purpose of this is that it guarantees that removing an element from a sub-list will never
    ///   change the index of the first element of any other sub-list. Sub-lists can therefore always rely on their
    ///   first element index to be their "anchor point" in the buffer of elements.
    /// - Each element remembers their "NextElementIndex". This is updated by Add/Remove operations.
    /// - Iterating the sub-list elements therefore means getting the first element, and following the
    ///   "NextElementIndex"s.
    /// - When a sub-list adds a first element, it first checks if there are any available free pinned first element
    ///   slots in the buffer. If so, it stores its first element there. If not, it will insert its first element at the
    ///   end of all the other pinned first elements. This will cause all the element "NextElementIndex" in the buffer
    ///   to be patched following the insertion.
    /// - Removing a sub-list's first element therefore frees up a "pinned first element" slot for the next sub-list
    ///   that adds its first element. So the buffer has a minimum length corresponding to the max amount of different
    ///   sub-lists that had a first element added at the same time.
    ///
    /// </summary>
    public struct CompactLinkedSubList
    {
        public int FirstElementIndex;
        public int Length;
        public byte IsCreated;

        public struct Iterator<T>
            where T : unmanaged, ICompactLinkedSubListElement
        {
            internal int PrevPrevIteratedIndex;
            internal int PrevIteratedIndex;
            internal int IteratedIndex;

            public bool GetNext(ref DynamicBuffer<T> buffer, out T element, out int elementIndex)
            {
                if (IteratedIndex >= 0 && IteratedIndex < buffer.Length)
                {
                    element = buffer[IteratedIndex];
                    elementIndex = IteratedIndex;

                    PrevPrevIteratedIndex = PrevIteratedIndex;
                    PrevIteratedIndex = IteratedIndex;
                    IteratedIndex = element.NextElementIndex;
                    return true;
                }

                elementIndex = -1;
                element = default;
                return false;
            }

            public void RemoveCurrentElement(ref CompactLinkedSubList subList, ref DynamicBuffer<T> buffer)
            {
                int removedElementPrevIndex = PrevPrevIteratedIndex;
                int removedElementIndex = PrevIteratedIndex;

                if (removedElementIndex >= 0)
                {
                    Assert.IsTrue(buffer.Length > removedElementPrevIndex);
                    Assert.IsTrue(buffer.Length > removedElementIndex);

                    T removedElement = buffer[removedElementIndex];
                    int removedElementNextIndex = removedElement.NextElementIndex;

                    Assert.IsTrue(buffer.Length > removedElementNextIndex);
                    Assert.AreEqual(1, removedElement.IsCreated);

                    // Removing a pinned first element
                    if (removedElement.IsPinnedFirstElement == 1)
                    {
                        // If there was a next element, this next element becomes the first
                        if (removedElementNextIndex >= 0)
                        {
                            T nextElement = buffer[removedElementNextIndex];
                            removedElement = nextElement;
                            removedElement.IsPinnedFirstElement = 1;

                            // Remove next element and patch indexes
                            buffer.RemoveAt(removedElementNextIndex);
                            PatchNextIndexes(ref buffer, removedElementNextIndex, -1);

                            // Make the iterated index the next element index
                            IteratedIndex = removedElement.NextElementIndex;
                        }
                        // If there was no next element, mark not created, and update sub-list
                        else
                        {
                            removedElement.IsCreated = 0;
                            removedElement.NextElementIndex = -1;
                            subList.FirstElementIndex = -1;
                        }

                        // Overwrite first element data. First elements are never removed, because removing one
                        // first element must never change the index of another first element.
                        buffer[removedElementIndex] = removedElement;
                    }
                    // Removing a regular element
                    else
                    {
                        // Make the previous element's NextIndex be the removed element's NextIndex
                        if (removedElementPrevIndex >= 0)
                        {
                            T prevElement = buffer[removedElementPrevIndex];
                            prevElement.NextElementIndex = removedElementNextIndex;
                            buffer[removedElementPrevIndex] = prevElement;
                        }

                        // Remove and patch indexes
                        buffer.RemoveAt(removedElementIndex);
                        PatchNextIndexes(ref buffer, removedElementIndex, -1);

                        // Make the iterated index the next element index
                        IteratedIndex = removedElementNextIndex;
                    }

                    subList.Length--;

                    Assert.IsTrue(subList.Length >= 0);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AutoInitialze()
        {
            if (IsCreated == 0)
            {
                FirstElementIndex = -1;
                Length = 0;
                IsCreated = 1;
            }
        }

        public static Iterator<T> GetIterator<T>(ref CompactLinkedSubList subList)
            where T : unmanaged, ICompactLinkedSubListElement
        {
            subList.AutoInitialze();

            return new Iterator<T>
            {
                PrevPrevIteratedIndex = -1,
                PrevIteratedIndex = -1,
                IteratedIndex = subList.FirstElementIndex,
            };
        }

        public static void Add<T>(
            ref CompactLinkedSubList subList,
            ref DynamicBuffer<T> buffer,
            T element)
            where T : unmanaged, ICompactLinkedSubListElement
        {
            subList.AutoInitialze();

            // Handle adding the first element at the start of the buffer
            if (subList.FirstElementIndex < 0)
            {
                Assert.AreEqual(0, subList.Length);

                int firstElementInsertionIndex = -1;
                int lastPinnedFirstElementIndex = 0;

                // If there are any pinned first elements added, look for a free one
                for (int i = 0; i < buffer.Length; i++)
                {
                    T iteratedElement = buffer[i];

                    // Stop looking as soon as we are not iterating pinned first elements anymore.
                    if (iteratedElement.IsPinnedFirstElement == 0)
                    {
                        break;
                    }

                    lastPinnedFirstElementIndex = i;
                    if (iteratedElement.IsCreated == 0)
                    {
                        firstElementInsertionIndex = i;
                        break;
                    }
                }

                // If we haven't found a free first element index, allocate new one at the end of the current first elements
                if (firstElementInsertionIndex < 0)
                {
                    firstElementInsertionIndex = lastPinnedFirstElementIndex;
                }

                // Insert the first element of the sub-list
                element.IsCreated = 1;
                element.IsPinnedFirstElement = 1;
                element.NextElementIndex = -1;
                buffer.Insert(firstElementInsertionIndex, element);

                // Patch all "NextElementIndex"s of the buffer if they were affected by the insert
                PatchNextIndexes(ref buffer, firstElementInsertionIndex, 1);

                // Update sub-list
                subList.FirstElementIndex = firstElementInsertionIndex;
                subList.Length = 1;
                return;
            }

            // If this isn't the first element of the sub-list, iterate sub-list elements until we find the last one.
            int lastElementIndex = -1;
            T lastElement = default;
            Iterator<T> iterator = GetIterator<T>(ref subList);
            while (iterator.GetNext(ref buffer, out T iteratedElement, out int iteratedElementIndex))
            {
                // If reached last one, remember it
                if (iteratedElement.NextElementIndex < 0)
                {
                    lastElementIndex = iteratedElementIndex;
                    lastElement = iteratedElement;
                    break;
                }
            }

            Assert.IsTrue(lastElementIndex >= 0);

            // Add element at the end of the buffer
            int newElementIndex = buffer.Length;
            element.IsCreated = 1;
            element.IsPinnedFirstElement = 0;
            element.NextElementIndex = -1;
            buffer.Add(element);

            // Patch NextIndex of previous last element
            lastElement.NextElementIndex = newElementIndex;
            buffer[lastElementIndex] = lastElement;

            // Update sub-list
            subList.Length++;
        }

        public static void Clear<T>(ref CompactLinkedSubList subList, ref DynamicBuffer<T> buffer)
            where T : unmanaged, ICompactLinkedSubListElement
        {
            subList.AutoInitialze();

            Iterator<T> iterator = GetIterator<T>(ref subList);
            while (iterator.GetNext(ref buffer, out T iteratedElement, out int iteratedElementIndex))
            {
                iterator.RemoveCurrentElement(ref subList, ref buffer);
            }

            Assert.AreEqual(0, subList.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAt<T>(ref CompactLinkedSubList subList, ref DynamicBuffer<T> buffer,
            int indexInSubList, out T element, out int elementIndexInBuffer)
            where T : unmanaged, ICompactLinkedSubListElement
        {
            subList.AutoInitialze();

            int indexCounter = 0;
            Iterator<T> iterator = GetIterator<T>(ref subList);
            while (iterator.GetNext(ref buffer, out T iteratedElement, out int iteratedElementIndex))
            {
                if (indexCounter == indexInSubList)
                {
                    elementIndexInBuffer = iteratedElementIndex;
                    element = iteratedElement;
                    return true;
                }

                indexCounter++;
            }

            elementIndexInBuffer = -1;
            element = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetAt<T>(ref CompactLinkedSubList subList, ref DynamicBuffer<T> buffer,
            int indexInSubList, T element)
            where T : unmanaged, ICompactLinkedSubListElement
        {
            subList.AutoInitialze();

            int indexCounter = 0;
            Iterator<T> iterator = GetIterator<T>(ref subList);
            while (iterator.GetNext(ref buffer, out T iteratedElement, out int iteratedElementIndex))
            {
                if (indexCounter == indexInSubList)
                {
                    T existingElement = buffer[iteratedElementIndex];
                    element.NextElementIndex = existingElement.NextElementIndex;
                    element.IsCreated = 1;
                    element.IsPinnedFirstElement = existingElement.IsPinnedFirstElement;
                    buffer[iteratedElementIndex] = element;
                    return true;
                }

                indexCounter++;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemoveAt<T>(ref CompactLinkedSubList subList, ref DynamicBuffer<T> buffer,
            int indexInSubList)
            where T : unmanaged, ICompactLinkedSubListElement
        {
            subList.AutoInitialze();

            int indexCounter = 0;
            Iterator<T> iterator = GetIterator<T>(ref subList);
            while (iterator.GetNext(ref buffer, out T iteratedElement, out int iteratedElementIndex))
            {
                if (indexCounter == indexInSubList)
                {
                    iterator.RemoveCurrentElement(ref subList, ref buffer);
                    return true;
                }

                indexCounter++;
            }

            return false;
        }

        public static void PatchNextIndexes<T>(ref DynamicBuffer<T> buffer, int changedIndex, int changeAmount)
            where T : unmanaged, ICompactLinkedSubListElement
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                T iteratedElement = buffer[i];
                if (iteratedElement.NextElementIndex >= changedIndex)
                {
                    iteratedElement.NextElementIndex += changeAmount;
                    buffer[i] = iteratedElement;
                }
            }
        }
    }

    #endregion
}