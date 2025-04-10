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
     *            The buffer must ONLY contain elements that were added/set using sublist APIs. You must also never
     *            change the data of sub-list interface properties in buffer elements.
     *
     * Different types of sub-lists are available, each with their pros and cons.
     */

    #region SubList

    public interface ISubListElement
    {
        public byte IsOccupied { get; set; }
    }
    
    /// <summary>
    /// 
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
    /// 
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

        public static SubList Create<T>(ref DynamicBuffer<T> buffer, int initialCapacity, float growFactor = 1.5f)
            where T : unmanaged, ISubListElement
        {
            SubList subList = new SubList();
            subList.Length = 0;
            subList.Capacity = 0;
            subList.GrowFactor = growFactor;
            subList.ElementsStartIndex = -1;
            subList.IsCreated = 1;

            SetCapacity(ref subList, ref buffer, math.max(1, initialCapacity));

            return subList;
        }

        public static void Add<T>(ref SubList subList, ref DynamicBuffer<T> buffer, T element)
            where T : unmanaged, ISubListElement
        {
            CheckCreated(in subList);
            
            int newLength = subList.Length + 1;
            
            // Grow capacity
            if (newLength > subList.Capacity)
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
                // Mark the current occupied as unoccupied before searching
                for (int i = subList.ElementsStartIndex; i < subList.ElementsStartIndex + subList.Length; i++)
                {
                    T iteratedElement = buffer[i];
                    iteratedElement.IsOccupied = 0;
                    buffer[i] = iteratedElement;
                }
                
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
                    UnsafeUtility.MemMove(dst, src, sizeof(T) * subList.Length);
                
                    // Free previous range
                    UnsafeUtility.MemClear(src, sizeof(T) * subList.Length);
                }

                // Mark the new range as occupied
                for (int i = freeRangeStart; i < freeRangeStart + newCapacity; i++)
                {
                    T iteratedElement = buffer[i];
                    iteratedElement.IsOccupied = 1;
                    buffer[i] = iteratedElement;
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

    #region PooledSubList

    public interface IPooledSubListElement
    {
        public int Version { get; set; }
        public PooledSubList.ElementHandle PrevElementHandle { get; set; }
    }
    
    /// <summary>
    /// 
    /// Allows storing multiple growable lists in the same list/buffer.
    /// - Sub-list elements are not contiguous in memory.
    /// - Sub-list element indexes do not change after being added.
    /// - Medium sub-list element remove performance, due to only being able to remove an element by iterating
    ///   from the last element in the sub-list. Medium sub-list element add performance due to having to
    ///   search for the lowest free index in the buffer.
    /// - The encompassing buffer will grow by a certain factor as elements are added, but it will never shrink unless
    ///   <Trim> is called. But even then, it cannot shrink smaller than the highest-index element in the buffer.
    ///
    /// This type of sub-list is best suited for when keeping a stable handle to a specific element is needed. The
    /// resulting buffer is also likely to be more compact in size than with the regular <SubList>, and add/remove
    /// performance doesn't decay as much with buffer length as with the <CompactSubList>. 
    /// 
    /// </summary>
    // TODO: optional version relying on FreeIndexRanges?
    public struct PooledSubList
    {
        public ElementHandle LastElementHandle;
        public int Count;
            
        public struct ElementHandle : IEquatable<ElementHandle>
        {
            public int Index;
            public int Version;

            public static ElementHandle Null => new ElementHandle { Index = 0, Version = 0 };
            
            public bool Exists()
            {
                return Version > 0 && Index >= 0;
            }
            
            public bool Equals(ElementHandle other)
            {
                return Index == other.Index && Version == other.Version;
            }

            public override bool Equals(object obj)
            {
                return obj is ElementHandle other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Index, Version);
            }

            public static bool operator ==(ElementHandle left, ElementHandle right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(ElementHandle left, ElementHandle right)
            {
                return !left.Equals(right);
            }
        }

        public struct Iterator<T>
            where T : unmanaged, IPooledSubListElement
        {
            internal int PrevPrevIteratedElementIndex;
            internal int PrevIteratedElementIndex;
            internal ElementHandle IteratedElementHandle;
            internal bool LastGetWasByRef;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool GetNext(in DynamicBuffer<T> buffer, out T iteratedElement, out ElementHandle iteratedElementHandle)
            {
                LastGetWasByRef = false;
                
                if (IteratedElementHandle.Exists() && IteratedElementHandle.Index < buffer.Length)
                {
                    iteratedElement = buffer[IteratedElementHandle.Index];
                    if (iteratedElement.Version == IteratedElementHandle.Version)
                    {
                        iteratedElementHandle = IteratedElementHandle;
                        return true;
                    }

                    PrevPrevIteratedElementIndex = PrevIteratedElementIndex;
                    PrevIteratedElementIndex = IteratedElementHandle.Index;
                    IteratedElementHandle = iteratedElement.PrevElementHandle;
                }

                PrevPrevIteratedElementIndex = -1;
                PrevIteratedElementIndex = -1;
                iteratedElementHandle = default;
                iteratedElement = default;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe ref T GetNextRef(ref DynamicBuffer<T> buffer, out bool success, out ElementHandle iteratedElementHandle)
            {
                LastGetWasByRef = true;
                
                if (IteratedElementHandle.Exists() && IteratedElementHandle.Index < buffer.Length)
                {
                    ref T element = ref UnsafeUtility.ArrayElementAsRef<T>(buffer.GetUnsafePtr(), IteratedElementHandle.Index);
                    if (element.Version == IteratedElementHandle.Version)
                    {
                        iteratedElementHandle = IteratedElementHandle;
                        
                        success = true;
                        return ref element;
                    }

                    PrevPrevIteratedElementIndex = PrevIteratedElementIndex;
                    PrevIteratedElementIndex = IteratedElementHandle.Index;
                    IteratedElementHandle = element.PrevElementHandle;
                }

                PrevPrevIteratedElementIndex = -1;
                PrevIteratedElementIndex = -1;
                iteratedElementHandle = default;
                success = false;
                return ref *(T*)buffer.GetUnsafePtr();
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveIteratedElement(ref PooledSubList subList, ref DynamicBuffer<T> buffer)
            {
                int removedElementIndex = PrevIteratedElementIndex;
                if (removedElementIndex >= 0)
                {
                    if (LastGetWasByRef)
                    {
                        throw new Exception("Cannot remove iterated elements when the element was gotten by ref");
                    }

                    int removedElementNextIndex = PrevPrevIteratedElementIndex;
                    T removedElement = buffer[removedElementIndex];

                    // If there was an element after the removed one, update its PrevElement to the removed element's PrevElement
                    if (removedElementNextIndex >= 0)
                    {
                        T removedElementNextElement = buffer[removedElementNextIndex];
                        removedElementNextElement.PrevElementHandle = removedElement.PrevElementHandle;
                        buffer[removedElementNextIndex] = removedElementNextElement;
                    }
                    // If we're removing the last element, update sub-list
                    else
                    {
                        subList.LastElementHandle = removedElement.PrevElementHandle;
                    }

                    // Write removed element
                    removedElement.Version = -removedElement.Version; // flip version
                    buffer[removedElementIndex] = removedElement;
                    
                    // Update sub-list
                    subList.Count--;
                    Assert.IsTrue(subList.Count >= 0);
                }
            }
        }
        
        public static Iterator<T> GetIterator<T>(PooledSubList subList)
            where T : unmanaged, IPooledSubListElement
        {
            return new Iterator<T>
            {
                PrevPrevIteratedElementIndex = -1,
                PrevIteratedElementIndex = -1,
                IteratedElementHandle = subList.LastElementHandle,
            };
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(T element)
            where T : unmanaged, IPooledSubListElement
        {
            return element.Version > 0;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref DynamicBuffer<T> buffer, ElementHandle elementHandle)
            where T : unmanaged, IPooledSubListElement
        {
            if (elementHandle.Exists() && elementHandle.Index < buffer.Length)
            {
                return Exists(buffer[elementHandle.Index]);
            }

            return false;
        }
        
        public static void Add<T>(ref PooledSubList subList, ref DynamicBuffer<T> buffer, T element,
            out ElementHandle elementHandle, float growFactor = 1.5f)
            where T : unmanaged, IPooledSubListElement
        {
            int addIndex = -1;

            // Iterate buffer element to try to find a free index
            for (int i = 0; i < buffer.Length; i++)
            {
                T iteratedObject = buffer[i];
                if (!Exists(iteratedObject))
                {
                    addIndex = i;
                    break;
                }
            }

            // If haven't found a free index, grow buffer capacity/length
            if (addIndex < 0)
            {
                addIndex = buffer.Length;
                int newCapacity = math.max((int)math.ceil(buffer.Length * growFactor), buffer.Length + 1);
                Resize(ref buffer, newCapacity);
            }

            // Write element at free index
            T existingElement = buffer[addIndex];
            element.Version = -existingElement.Version + 1; // flip version and increment
            element.PrevElementHandle = subList.LastElementHandle;
            buffer[addIndex] = element;
            
            elementHandle = new ElementHandle
            {
                Index = addIndex,
                Version = element.Version,
            };
            
            // Update sub-list
            subList.LastElementHandle = elementHandle;
            subList.Count++;
            Assert.IsTrue(subList.Count >= 0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGet<T>(ref DynamicBuffer<T> buffer, ElementHandle elementHandle,
            out T existingObject)
            where T : unmanaged, IPooledSubListElement
        {
            if (elementHandle.Exists() && elementHandle.Index < buffer.Length)
            {
                existingObject = buffer[elementHandle.Index];
                if (existingObject.Version == elementHandle.Version)
                {
                    return true;
                }
            }

            existingObject = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T TryGetRef<T>(
            ref DynamicBuffer<T> buffer,
            ElementHandle elementHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IPooledSubListElement
        {
            if (elementHandle.Exists() && elementHandle.Index < buffer.Length)
            {
                ref T existingObject =
                    ref UnsafeUtility.ArrayElementAsRef<T>(buffer.GetUnsafePtr(), elementHandle.Index);
                if (existingObject.Version == elementHandle.Version)
                {
                    success = true;
                    return ref existingObject;
                }
            }

            success = false;
            return ref nullResult;
        }
        
        public static bool TryRemove<T>(ref PooledSubList subList, ref DynamicBuffer<T> buffer, ElementHandle elementHandle)
            where T : unmanaged, IPooledSubListElement
        {
            if (subList.LastElementHandle.Exists() && elementHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>(subList);
                while (iterator.GetNext(in buffer, out T iteratedElement, out ElementHandle iteratedElementHandle))
                {
                    if (iteratedElementHandle == elementHandle)
                    {
                        iterator.RemoveIteratedElement(ref subList, ref buffer);
                        return true;
                    }
                }
            }
            
            Assert.IsTrue(subList.Count == 0);
            return false;
        }

        public static void Clear<T>(ref PooledSubList subList, ref DynamicBuffer<T> buffer)
            where T : unmanaged, IPooledSubListElement
        {
            if (subList.LastElementHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>(subList);
                while (iterator.GetNext(in buffer, out _, out _))
                {
                    iterator.RemoveIteratedElement(ref subList, ref buffer);
                }
            }
            
            Assert.IsTrue(subList.LastElementHandle == ElementHandle.Null);
            Assert.IsTrue(subList.Count == 0);
        }

        /// <summary>
        /// Note: can only grow; not shrink
        /// </summary>
        public static void Resize<T>(ref DynamicBuffer<T> buffer, int newSize)
            where T : unmanaged, IPooledSubListElement
        {
            if (newSize > buffer.Length)
            {
                buffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            }
        }
        
        public static void Trim<T>(ref DynamicBuffer<T> buffer, bool trimCapacity = false)
            where T : unmanaged, IPooledSubListElement
        {
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                T iteratedElement = buffer[i];
                if (Exists(iteratedElement))
                {
                    buffer.Resize(i + 1, NativeArrayOptions.ClearMemory);
                    if (trimCapacity)
                    {
                        buffer.Capacity = i + 1;
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// Reorganizes the pool so that all elements are compact and the pool length fits the elements count exactly.
        /// This will invalidate the data of any <PooledSubList>, or of any <ElementHandle> aside from the subList
        /// elements' "PrevElementHandle". However, a "allSubListsInBuffer" array will contain the updated SubLists data,
        /// and the handlesRemapper will allow remapping old <ElementHandle>s to the updated ones. You will have to handle
        /// setting back updated SubLists data and remapping <ElementHandle>s manually.
        /// </summary>
        public static void MakeCompactAndInvalidateHandles<T>(
            ref DynamicBuffer<T> buffer,
            ref NativeArray<PooledSubList> allSubListsInBuffer,
            ref NativeHashMap<ElementHandle, ElementHandle> handlesRemapper,
            bool alsoTrimCapacity)
            where T : unmanaged, IPooledSubListElement
        {
            handlesRemapper.Clear();

            // Iterate buffer from the start until we find free indices. When we do, iterate from the end to find an
            // existing element to move there. Continue until start iterator and end iterator meet
            int dscIndex = buffer.Length - 1;
            for (int ascIndex = 0; ascIndex < dscIndex; ascIndex++)
            {
                T iteratedAscElement = buffer[ascIndex];
                if (!Exists(iteratedAscElement))
                {
                    T iteratedDscElement = default;
                    while (dscIndex > ascIndex && !Exists(iteratedDscElement))
                    {
                        iteratedDscElement = buffer[ascIndex];
                        dscIndex--;
                    }

                    // Move element
                    if (Exists(iteratedDscElement))
                    {
                        handlesRemapper.Add(
                            new ElementHandle { Index = dscIndex, Version = iteratedDscElement.Version },
                            new ElementHandle { Index = ascIndex, Version = iteratedDscElement.Version });
                        buffer[ascIndex] = iteratedDscElement;
                    }
                }
            }

            // Trim excess length (and optionally capacity)
            Trim(ref buffer, alsoTrimCapacity);
            
            // Update elements
            for (int i = 0; i < buffer.Length; i++)
            {
                T iteratedElement = buffer[i];
                if (handlesRemapper.TryGetValue(iteratedElement.PrevElementHandle, out ElementHandle newPrevElementHandle))
                {
                    iteratedElement.PrevElementHandle = newPrevElementHandle;
                }
                buffer[i] = iteratedElement;
            }
            
            // Update sublists
            for (int i = 0; i < allSubListsInBuffer.Length; i++)
            {
                PooledSubList subList = allSubListsInBuffer[i];
                if (handlesRemapper.TryGetValue(subList.LastElementHandle, out ElementHandle newLastElementHandle))
                {
                    subList.LastElementHandle = newLastElementHandle;
                }
                allSubListsInBuffer[i] = subList;
            }
        }
    }

    #endregion

    #region CompactSubList

    public interface ICompactSubListElement
    {
        public int NextElementIndex { get; set; }
        public int LastElementIndex { get; set; }
        public byte IsCreated { get; set; }
        public byte IsPinnedFirstElement { get; set; }
    }

    /// <summary>
    /// 
    /// Allows storing multiple growable lists in the same list/buffer.
    /// - Sub-list elements are not contiguous in memory.
    /// - Sub-list element indexes can change when elements are added or removed.
    /// - Relatively poor sub-list element add and remove performance due to having to iterate elements of the sub-list until the
    ///   last one (for add) or the removed one (for remove) are found, and having to patch all elements in buffer during
    ///   removes or first sub-list adds. So add/remove performance decays with buffer and sub-list size.
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
    public struct CompactSubList
    {
        public int FirstElementIndex;
        public int Count;
        public byte IsCreated;

        public struct Iterator<T>
            where T : unmanaged, ICompactSubListElement
        {
            internal int PrevPrevIteratedIndex;
            internal int PrevIteratedIndex;
            internal int IteratedIndex;
            internal bool LastGetWasByRef;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool GetNext(in DynamicBuffer<T> buffer, out T element, out int elementIndex)
            {
                LastGetWasByRef = false;
                
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe ref T GetNextRef(ref DynamicBuffer<T> buffer, out bool success, out int elementIndex)
            {
                LastGetWasByRef = true;
                
                if (IteratedIndex >= 0 && IteratedIndex < buffer.Length)
                {
                    ref T element = ref UnsafeUtility.ArrayElementAsRef<T>(buffer.GetUnsafePtr(), IteratedIndex);
                    elementIndex = IteratedIndex;

                    PrevPrevIteratedIndex = PrevIteratedIndex;
                    PrevIteratedIndex = IteratedIndex;
                    IteratedIndex = element.NextElementIndex;

                    success = true;
                    return ref element;
                }

                elementIndex = -1;
                success = false;
                return ref *(T*)buffer.GetUnsafePtr();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveIteratedElement(ref CompactSubList subList, ref DynamicBuffer<T> buffer)
            {
                int indexOfElementBeforeRemovedElement = PrevPrevIteratedIndex;
                int indexOfRemovedElement = PrevIteratedIndex;

                if (indexOfRemovedElement >= 0)
                {
                    if (LastGetWasByRef)
                    {
                        throw new Exception("Cannot remove iterated elements when the element was gotten by ref");
                    }

                    Assert.IsTrue(buffer.Length > indexOfElementBeforeRemovedElement);
                    Assert.IsTrue(buffer.Length > indexOfRemovedElement);

                    T removedElement = buffer[indexOfRemovedElement];

                    Assert.IsTrue(buffer.Length > removedElement.NextElementIndex);
                    Assert.AreEqual(1, removedElement.IsCreated);

                    // Removing a pinned first element
                    if (removedElement.IsPinnedFirstElement == 1)
                    {
                        // If there was a next element, this next element becomes the first
                        if (removedElement.NextElementIndex >= 0)
                        {
                            int indexOfRemovedElementNextElement = removedElement.NextElementIndex;
                            T removedElementNextElement = buffer[indexOfRemovedElementNextElement];
                            
                            // Transfer pinned first element data 
                            removedElementNextElement.LastElementIndex = removedElement.LastElementIndex;
                            removedElementNextElement.IsPinnedFirstElement = 1;
                            removedElement = removedElementNextElement;

                            // Overwrite first element data. 
                            buffer[indexOfRemovedElement] = removedElement;
                            
                            // Remove next element and patch indexes
                            buffer.RemoveAt(indexOfRemovedElementNextElement);
                            PatchNextIndexes(ref buffer, indexOfRemovedElementNextElement, -1);
                            int patchedRemovedElementNextElementIndex = removedElement.NextElementIndex -= 1;
                            
                            // Make the iterated index the next element index
                            IteratedIndex = patchedRemovedElementNextElementIndex;
                        }
                        // If there was no next element, mark not created, and update sub-list
                        else
                        {
                            removedElement.IsCreated = 0;
                            removedElement.NextElementIndex = -1;
                            removedElement.LastElementIndex = -1;
                            subList.FirstElementIndex = -1;

                            // Overwrite first element data. 
                            buffer[indexOfRemovedElement] = removedElement;
                        }
                    }
                    // Removing a regular element
                    else
                    {
                        // Make the previous element's NextIndex be the removed element's NextIndex
                        if (indexOfElementBeforeRemovedElement >= 0)
                        {
                            T elementBeforeRemovedElement = buffer[indexOfElementBeforeRemovedElement];
                            elementBeforeRemovedElement.NextElementIndex = removedElement.NextElementIndex;
                            buffer[indexOfElementBeforeRemovedElement] = elementBeforeRemovedElement;
                        }

                        // If we're removing the last element, update LastElementIndex with the index of the element
                        // that was before this one.
                        if (removedElement.NextElementIndex < 0)
                        {
                            Assert.IsTrue(subList.FirstElementIndex >= 0);
                            T firstElementOfSubList = buffer[subList.FirstElementIndex];
                            firstElementOfSubList.LastElementIndex = indexOfElementBeforeRemovedElement;
                            buffer[subList.FirstElementIndex] = firstElementOfSubList;
                        }

                        // Remove and patch indexes
                        buffer.RemoveAt(indexOfRemovedElement);
                        PatchNextIndexes(ref buffer, indexOfRemovedElement, -1);
                        int patchedRemovedElementNextElementIndex = removedElement.NextElementIndex -= 1;

                        // Make the iterated index the next element index
                        IteratedIndex = patchedRemovedElementNextElementIndex;
                    }

                    subList.Count--;

                    Assert.IsTrue(subList.Count >= 0);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckCreated()
        {
            if (IsCreated == 0)
            {
                throw new Exception($"Error: CompactSubList is not created.");
            }
        }

        public static CompactSubList Create()
        {
            return new CompactSubList
            {
                FirstElementIndex = -1,
                Count = 0,
                IsCreated = 1,
            };
        }

        public static Iterator<T> GetIterator<T>(CompactSubList subList)
            where T : unmanaged, ICompactSubListElement
        {
            subList.CheckCreated();

            return new Iterator<T>
            {
                PrevPrevIteratedIndex = -1,
                PrevIteratedIndex = -1,
                IteratedIndex = subList.FirstElementIndex,
            };
        }

        public static void Add<T>(
            ref CompactSubList subList,
            ref DynamicBuffer<T> buffer,
            T element)
            where T : unmanaged, ICompactSubListElement
        {
            subList.CheckCreated();

            // Handle adding the first element at the start of the buffer
            if (subList.FirstElementIndex < 0)
            {
                Assert.AreEqual(0, subList.Count);

                bool firstElementReplacesExisting = false;
                int firstElementInsertionIndex = -1;
                int lastPinnedFirstElementIndex = -1;

                // If there are any pinned first elements added, look for a free one
                for (int i = 0; i < buffer.Length; i++)
                {
                    T iteratedElement = buffer[i];

                    // Stop looking as soon as we are not iterating pinned first elements anymore.
                    if (iteratedElement.IsPinnedFirstElement == 0)
                    {
                        firstElementInsertionIndex = i;
                        break;
                    }

                    lastPinnedFirstElementIndex = i;
                    
                    // If this is a pinned first element that is not valid, take its place
                    if (iteratedElement.IsCreated == 0)
                    {
                        firstElementReplacesExisting = true;
                        firstElementInsertionIndex = i;
                        break;
                    }
                }

                // If we haven't found a free first element index, allocate new one at the end of the current first elements
                if (firstElementInsertionIndex < 0)
                {
                    firstElementInsertionIndex = lastPinnedFirstElementIndex + 1;
                }

                // Set element data
                element.IsCreated = 1;
                element.IsPinnedFirstElement = 1;
                element.NextElementIndex = -1;
                element.LastElementIndex = firstElementInsertionIndex;

                // Write element to buffer
                if (firstElementReplacesExisting)
                {
                    buffer[firstElementInsertionIndex] = element;
                }
                else
                {
                    if (firstElementInsertionIndex >= buffer.Length)
                    {
                        buffer.Add(element);
                    }
                    else
                    {
                        buffer.Insert(firstElementInsertionIndex, element);

                        // Patch all "NextElementIndex"s of the buffer if they were affected by the insert
                        PatchNextIndexes(ref buffer, firstElementInsertionIndex, 1);
                    }
                }
                
                // Update sub-list
                subList.FirstElementIndex = firstElementInsertionIndex;
                subList.Count = 1;
                return;
            }
            
            Assert.IsTrue(subList.FirstElementIndex < buffer.Length);

            int lastElementIndexBeforeAdd = buffer[subList.FirstElementIndex].LastElementIndex;
            
            Assert.IsTrue(lastElementIndexBeforeAdd >= subList.FirstElementIndex && lastElementIndexBeforeAdd < buffer.Length);
            
            // Add element at the end of the buffer
            int addedElementIndex = buffer.Length;
            element.IsCreated = 1;
            element.IsPinnedFirstElement = 0;
            element.NextElementIndex = -1;
            buffer.Add(element);
            
            // Update first element of sublist, storing LastElementIndex
            T subListFirstElement = buffer[subList.FirstElementIndex];
            subListFirstElement.LastElementIndex = addedElementIndex;
            buffer[subList.FirstElementIndex] = subListFirstElement;

            // Patch NextIndex of previous last element
            T elementBeforeTheAddedElement = buffer[lastElementIndexBeforeAdd];
            elementBeforeTheAddedElement.NextElementIndex = addedElementIndex;
            buffer[lastElementIndexBeforeAdd] = elementBeforeTheAddedElement;

            // Update sub-list
            subList.Count++;
        }

        public static void Clear<T>(ref CompactSubList subList, ref DynamicBuffer<T> buffer)
            where T : unmanaged, ICompactSubListElement
        {
            subList.CheckCreated();

            Iterator<T> iterator = GetIterator<T>(subList);
            while (iterator.GetNext(in buffer, out T iteratedElement, out int iteratedElementIndex))
            {
                iterator.RemoveIteratedElement(ref subList, ref buffer);
            }

            Assert.AreEqual(0, subList.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGet<T>(ref CompactSubList subList, ref DynamicBuffer<T> buffer,
            int indexInSubList, out T element, out int elementIndexInBuffer)
            where T : unmanaged, ICompactSubListElement
        {
            subList.CheckCreated();

            int indexCounter = 0;
            Iterator<T> iterator = GetIterator<T>(subList);
            while (iterator.GetNext(in buffer, out T iteratedElement, out int iteratedElementIndex))
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
        public static bool TrySet<T>(ref CompactSubList subList, ref DynamicBuffer<T> buffer,
            int indexInSubList, T element)
            where T : unmanaged, ICompactSubListElement
        {
            subList.CheckCreated();

            int indexCounter = 0;
            Iterator<T> iterator = GetIterator<T>(subList);
            while (iterator.GetNext(in buffer, out T iteratedElement, out int iteratedElementIndex))
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
        public static bool TryRemove<T>(ref CompactSubList subList, ref DynamicBuffer<T> buffer,
            int indexInSubList)
            where T : unmanaged, ICompactSubListElement
        {
            subList.CheckCreated();

            int indexCounter = 0;
            Iterator<T> iterator = GetIterator<T>(subList);
            while (iterator.GetNext(in buffer, out T iteratedElement, out int iteratedElementIndex))
            {
                if (indexCounter == indexInSubList)
                {
                    iterator.RemoveIteratedElement(ref subList, ref buffer);
                    return true;
                }

                indexCounter++;
            }

            return false;
        }

        public static void PatchNextIndexes<T>(ref DynamicBuffer<T> buffer, int changedIndex, int changeAmount)
            where T : unmanaged, ICompactSubListElement
        {
            // Next indexes of all element
            for (int i = 0; i < buffer.Length; i++)
            {
                T iteratedElement = buffer[i];
                if (iteratedElement.NextElementIndex >= changedIndex)
                {
                    iteratedElement.NextElementIndex += changeAmount;
                    buffer[i] = iteratedElement;
                }
            }

            // Last indexes of only first elements
            for (int i = 0; i < buffer.Length; i++)
            {
                T iteratedElement = buffer[i];
                if (iteratedElement.LastElementIndex != i && iteratedElement.LastElementIndex >= changedIndex)
                {
                    iteratedElement.LastElementIndex += changeAmount;
                    buffer[i] = iteratedElement;
                }
                
                if (iteratedElement.IsPinnedFirstElement == 0)
                {
                    break;
                }
            }
        }
    }

    #endregion
}