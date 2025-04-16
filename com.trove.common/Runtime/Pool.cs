using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Assertions;

namespace Trove
{
    public interface IPoolElement
    {
        public int Version { get; set; }
    }
    
    public struct PoolElementHandle : IEquatable<PoolElementHandle>
    {
        public int Index;
        public int Version;

        public static readonly PoolElementHandle Null = default;
        
        public PoolElementHandle(int index, int version)
        {
            Index = index;
            Version = version;
        }
        
        public bool Exists()
        {
            return Version > 0 && Index >= 0;
        }
            
        public bool Equals(PoolElementHandle other)
        {
            return Index == other.Index && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            return obj is PoolElementHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Version);
        }

        public static bool operator ==(PoolElementHandle left, PoolElementHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PoolElementHandle left, PoolElementHandle right)
        {
            return !left.Equals(right);
        }
    }
    
    /// <summary>
    /// A pool of objects:
    /// - Guarantees unchanging object indexes.
    /// - Object allocation has to search through the indexes in ascending order to find the first free slot.
    /// </summary>
    public static class Pool
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Exists<T>(T poolObject)
            where T : unmanaged, IPoolElement
        {
            return poolObject.Version > 0;
        }

        #region DynamicBuffer
        public static void Init<T>(ref DynamicBuffer<T> poolBuffer, int initialCapacity)
            where T : unmanaged, IPoolElement
        {
            Resize(ref poolBuffer, initialCapacity);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref DynamicBuffer<T> poolBuffer, PoolElementHandle poolElementHandle)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                return Exists(poolBuffer[poolElementHandle.Index]);
            }

            return false;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObject<T>(
            ref DynamicBuffer<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            out T poolObject)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    poolObject = existingObject;
                    return true;
                }
            }

            poolObject = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T TryGetObjectRef<T>(
            ref DynamicBuffer<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    ref T poolObject = 
                        ref UnsafeUtility.ArrayElementAsRef<T>(poolBuffer.GetUnsafePtr(), poolElementHandle.Index);
                    success = true;
                    return ref poolObject;
                }
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetObject<T>(
            ref DynamicBuffer<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            T poolObject)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    poolObject.Version = poolElementHandle.Version;
                    poolBuffer[poolElementHandle.Index] = poolObject;
                    return true;
                }
            }

            return false;
        }

        public static void AddElement<T>(
            ref DynamicBuffer<T> poolBuffer,
            T newObject, 
            out PoolElementHandle poolElementHandle,
            float growFactor = 1.5f)
            where T : unmanaged, IPoolElement
        {
            int addIndex = -1;
            for (int i = 0; i < poolBuffer.Length; i++)
            {
                T iteratedObject = poolBuffer[i];
                if (!Exists(iteratedObject))
                {
                    addIndex = i;
                    break;
                }
            }

            if (addIndex < 0)
            {
                addIndex = poolBuffer.Length;
                int newCapacity = math.max((int)math.ceil(poolBuffer.Length * growFactor), poolBuffer.Length + 1);
                Resize(ref poolBuffer, newCapacity);
            }

            T existingObject = poolBuffer[addIndex];
            newObject.Version = -existingObject.Version + 1; // flip version and increment
            poolBuffer[addIndex] = newObject;
            
            poolElementHandle = new PoolElementHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
        }

        public static bool TryRemoveObject<T>(
            ref DynamicBuffer<T> poolBuffer,
            PoolElementHandle poolElementHandle)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    existingObject.Version = -existingObject.Version; // flip version
                    poolBuffer[poolElementHandle.Index] = existingObject;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Note: can only grow; not shrink
        /// </summary>
        public static void Resize<T>(ref DynamicBuffer<T> poolBuffer, int newSize)
            where T : unmanaged, IPoolElement
        {
            if (newSize > poolBuffer.Length)
            {
                poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            }
        }
        
        public static void Trim<T>(ref DynamicBuffer<T> poolBuffer, bool trimCapacity = false)
            where T : unmanaged, IPoolElement
        {
            for (int i = poolBuffer.Length - 1; i >= 0; i--)
            {
                T iteratedObject = poolBuffer[i];
                if (Exists(iteratedObject))
                {
                    poolBuffer.Resize(i + 1, NativeArrayOptions.ClearMemory);
                    if (trimCapacity)
                    {
                        poolBuffer.Capacity = i + 1;
                    }

                    return;
                }
            }
        }
        #endregion
        
        #region NativeList
        public static void Init<T>(ref NativeList<T> poolBuffer, int initialCapacity)
            where T : unmanaged, IPoolElement
        {
            Resize(ref poolBuffer, initialCapacity);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref NativeList<T> poolBuffer, PoolElementHandle poolElementHandle)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                return Exists(poolBuffer[poolElementHandle.Index]);
            }

            return false;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObject<T>(
            ref NativeList<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            out T poolObject)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    poolObject = existingObject;
                    return true;
                }
            }

            poolObject = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T TryGetObjectRef<T>(
            ref NativeList<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                ref T existingObject = 
                    ref UnsafeUtility.ArrayElementAsRef<T>(poolBuffer.GetUnsafePtr(), poolElementHandle.Index);
                if (existingObject.Version == poolElementHandle.Version)
                {
                    success = true;
                    return ref existingObject;
                }
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetObject<T>(
            ref NativeList<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            T poolObject)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    poolObject.Version = poolElementHandle.Version;
                    poolBuffer[poolElementHandle.Index] = poolObject;
                    return true;
                }
            }

            return false;
        }

        public static void AddElement<T>(
            ref NativeList<T> poolBuffer,
            T newObject, 
            out PoolElementHandle poolElementHandle,
            float growFactor = 1.5f)
            where T : unmanaged, IPoolElement
        {
            int addIndex = -1;
            for (int i = 0; i < poolBuffer.Length; i++)
            {
                T iteratedObject = poolBuffer[i];
                if (!Exists(iteratedObject))
                {
                    addIndex = i;
                    break;
                }
            }

            if (addIndex < 0)
            {
                addIndex = poolBuffer.Length;
                int newCapacity = math.max((int)math.ceil(poolBuffer.Length * growFactor), poolBuffer.Length + 1);
                Resize(ref poolBuffer, newCapacity);
            }

            T existingObject = poolBuffer[addIndex];
            newObject.Version = -existingObject.Version + 1; // flip version and increment
            poolBuffer[addIndex] = newObject;
            
            poolElementHandle = new PoolElementHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
        }

        public static bool TryRemoveObject<T>(
            ref NativeList<T> poolBuffer,
            PoolElementHandle poolElementHandle)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    existingObject.Version = -existingObject.Version; // flip version
                    poolBuffer[poolElementHandle.Index] = existingObject;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Note: can only grow; not shrink
        /// </summary>
        public static void Resize<T>(ref NativeList<T> poolBuffer, int newSize)
            where T : unmanaged, IPoolElement
        {
            if (newSize > poolBuffer.Length)
            {
                poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            }
        }
        
        public static void Trim<T>(ref NativeList<T> poolBuffer, bool trimCapacity = false)
            where T : unmanaged, IPoolElement
        {
            for (int i = poolBuffer.Length - 1; i >= 0; i--)
            {
                T iteratedObject = poolBuffer[i];
                if (Exists(iteratedObject))
                {
                    poolBuffer.Resize(i + 1, NativeArrayOptions.ClearMemory);
                    if (trimCapacity)
                    {
                        poolBuffer.SetCapacity(i + 1);
                    }

                    return;
                }
            }
        }
        #endregion
        
    }
    
    /// <summary>
    /// A pool of objects:
    /// - Guarantees unchanging object indexes.
    /// - Object allocation uses a second list of free indexes
    /// </summary>
    public static class FreeRangesPool
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Exists<T>(T poolObject)
            where T : unmanaged, IPoolElement
        {
            return poolObject.Version > 0;
        }

        #region DynamicBuffer
        public static void Init<T>(ref DynamicBuffer<T> poolBuffer, ref DynamicBuffer<IndexRange> freeRangesBuffer, int initialCapacity)
            where T : unmanaged, IPoolElement
        {
            Resize(ref poolBuffer, ref freeRangesBuffer, initialCapacity);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref DynamicBuffer<T> poolBuffer, PoolElementHandle poolElementHandle)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                return Exists(poolBuffer[poolElementHandle.Index]);
            }

            return false;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObject<T>(
            ref DynamicBuffer<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            out T poolObject)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    poolObject = existingObject;
                    return true;
                }
            }

            poolObject = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T TryGetObjectRef<T>(
            ref DynamicBuffer<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    ref T poolObject = 
                        ref UnsafeUtility.ArrayElementAsRef<T>(poolBuffer.GetUnsafePtr(), poolElementHandle.Index);
                    success = true;
                    return ref poolObject;
                }
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetObject<T>(
            ref DynamicBuffer<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            T poolObject)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    poolObject.Version = poolElementHandle.Version;
                    poolBuffer[poolElementHandle.Index] = poolObject;
                    return true;
                }
            }

            return false;
        }

        public static void AddElement<T>(
            ref DynamicBuffer<T> poolBuffer, 
            ref DynamicBuffer<IndexRange> freeRangesBuffer,
            T newObject, 
            out PoolElementHandle poolElementHandle,
            float growFactor = 1.5f)
            where T : unmanaged, IPoolElement
        {
            int addIndex = -1;
            
            // Find first free range that can accomodate the object
            if (freeRangesBuffer.Length > 0)
            {
                IndexRange firstFreeRange = freeRangesBuffer[0];
                
                addIndex = firstFreeRange.Start;
                firstFreeRange.Start++;
                firstFreeRange.Length--;
                
                if (firstFreeRange.Length == 0)
                {
                    freeRangesBuffer.RemoveAt(0);
                }
                else
                {
                    freeRangesBuffer[0] = firstFreeRange;
                }
            }
            // If no more free ranges, grow
            else
            {
                int newCapacity = math.max((int)math.ceil(poolBuffer.Length * growFactor),
                    poolBuffer.Length + 1);
                Resize(ref poolBuffer, ref freeRangesBuffer, newCapacity);

                int lastRangeIndex = freeRangesBuffer.Length - 1;
                IndexRange lastIndexRange = freeRangesBuffer[lastRangeIndex];

                addIndex = lastIndexRange.Start;
                lastIndexRange.Start++;
                lastIndexRange.Length--;
                
                if (lastIndexRange.Length == 0)
                {
                    freeRangesBuffer.RemoveAt(lastRangeIndex);
                }
                else
                {
                    freeRangesBuffer[lastRangeIndex] = lastIndexRange;
                }
            }
            
            Assert.IsTrue(addIndex >= 0);
            
            // Write element
            T existingObject = poolBuffer[addIndex];
            newObject.Version = -existingObject.Version + 1; // Flip and increment version
            poolBuffer[addIndex] = newObject;
            
            poolElementHandle = new PoolElementHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
        }

        public static bool TryRemoveObject<T>(
            ref DynamicBuffer<T> poolBuffer, 
            ref DynamicBuffer<IndexRange> freeRangesBuffer,
            PoolElementHandle poolElementHandle)
            where T : unmanaged, IPoolElement
        {
            int removedElementIndex = poolElementHandle.Index;
            if (poolElementHandle.Exists() && removedElementIndex < poolBuffer.Length)
            {
                T existingObject = poolBuffer[removedElementIndex];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    // Clear element
                    existingObject.Version = -existingObject.Version; // flip version
                    poolBuffer[removedElementIndex] = existingObject;
                    
                    // Add back free range
                    bool addedToFreeRanges = false;
                    for (int i = 0; i < freeRangesBuffer.Length; i++)
                    {
                        IndexRange iteratedIndexRange = freeRangesBuffer[i];

                        // If the freed index is right before the iterated range starts, expand the range backward and
                        // check to merge with a previous
                        if (removedElementIndex == iteratedIndexRange.Start - 1)
                        {
                            // Grow range backward if removed range starts earlier
                            int iteratedRangeLastIndex = iteratedIndexRange.Start + iteratedIndexRange.Length - 1;
                            iteratedIndexRange.Start = math.min(iteratedIndexRange.Start, removedElementIndex);
                            iteratedIndexRange.Length = iteratedRangeLastIndex - iteratedIndexRange.Start + 1;

                            // Check for merge if prev range ends right before the new expanded iterated range
                            CheckMergeWithPrevRange<T>(ref freeRangesBuffer, ref i, ref iteratedIndexRange, iteratedRangeLastIndex);

                            // Write back iterated free range
                            freeRangesBuffer[i] = iteratedIndexRange;

                            addedToFreeRanges = true;
                            break;
                        }
                        // If the ranges don't touch and the iterated range is completely past the freed range, insert 
                        // new free range
                        else if (removedElementIndex < iteratedIndexRange.Start - 1)
                        {
                            freeRangesBuffer.InsertRange(i, 1);
                            IndexRange newIndexRange = new IndexRange
                            {
                                Start = removedElementIndex,
                                Length = 1,
                            };

                            // Check for merge if prev range ends right before the new expanded iterated range
                            CheckMergeWithPrevRange<T>(ref freeRangesBuffer, ref i, ref newIndexRange, newIndexRange.Start + newIndexRange.Length - 1);
                            
                            freeRangesBuffer[i] = newIndexRange;

                            addedToFreeRanges = true;
                            break;
                        }
                    }

                    // If we haven't added to an existing range or in-between existing ranges, add new range at the end
                    if (!addedToFreeRanges)
                    {
                        IndexRange newIndexRange = new IndexRange
                        {
                            Start = removedElementIndex,
                            Length = 1,
                        };
                        
                        int addIndex = freeRangesBuffer.Length;
                        CheckMergeWithPrevRange<T>(ref freeRangesBuffer, ref addIndex, ref newIndexRange, newIndexRange.Start + newIndexRange.Length - 1);
                        
                        freeRangesBuffer.Add(newIndexRange);
                    }
                    
                    return true;
                }
            }

            return false;
        }

        private static void CheckMergeWithPrevRange<T>(
            ref DynamicBuffer<IndexRange> freeRangesBuffer,
            ref int i,
            ref IndexRange currentIndexRange,
            int curentRangeLastIndex)
            where T : unmanaged, IPoolElement
        {
            // Check for merge if prev range ends right before the new expanded iterated range
            if (i > 0)
            {
                IndexRange prevFreeIndexRange = freeRangesBuffer[i - 1];
                int prevRangeLastIndex = prevFreeIndexRange.Start + prevFreeIndexRange.Length - 1;
                if (prevRangeLastIndex == currentIndexRange.Start - 1)
                {
                    currentIndexRange.Start = prevFreeIndexRange.Start;
                    currentIndexRange.Length = curentRangeLastIndex - currentIndexRange.Start + 1;
                    freeRangesBuffer.RemoveAt(i - 1);
                    i--; // decrement index to compensate for element remove
                }
            }
        }

        public static void Resize<T>(ref DynamicBuffer<T> poolBuffer, ref DynamicBuffer<IndexRange> freeRangesBuffer, int newSize)
            where T : unmanaged, IPoolElement
        {
            int initialSize = poolBuffer.Length;
            
            // Grow
            if (newSize > poolBuffer.Length)
            {
                int addedSize = newSize - initialSize;
                
                poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
                
                // Add free range
                if (freeRangesBuffer.Length > 0)
                {
                    IndexRange lastFreeRange = freeRangesBuffer[freeRangesBuffer.Length - 1];

                    // Check if we can just expand last range (if it ended at the capacity before resize)
                    if (lastFreeRange.Start + lastFreeRange.Length == initialSize)
                    {
                        lastFreeRange.Length += addedSize;
                        freeRangesBuffer[freeRangesBuffer.Length - 1] = lastFreeRange;
                    }
                    // If not, add new range
                    else
                    {
                        freeRangesBuffer.Add(new IndexRange
                            { Start = initialSize, Length = addedSize });
                    }
                }
                // If there were no free ranges left at all, add new one
                else
                {
                    freeRangesBuffer.Add(new IndexRange
                        { Start = initialSize, Length = addedSize });
                }
            }
            // Trim
            else if (newSize < poolBuffer.Length)
            {
                if (freeRangesBuffer.Length > 0)
                {
                    IndexRange lastFreeRange = freeRangesBuffer[freeRangesBuffer.Length - 1];
                    int lastFreeIndex = lastFreeRange.Start + lastFreeRange.Length - 1;
                
                    // If the last free index reaches the end of the buffer, there is potential for trimming
                    if (lastFreeIndex == poolBuffer.Length - 1)
                    {
                        // Determine a new size that can't go lower than the last allocated element
                        newSize = math.max(lastFreeRange.Start, newSize);
                        int newLastIndex = newSize - 1;
                        
                        // If the new last index is fully before the last range, remove entire range
                        if (newLastIndex < lastFreeRange.Start)
                        {
                            freeRangesBuffer.RemoveAt(freeRangesBuffer.Length - 1);
                        }
                        // Otherwise, shrink range
                        else
                        {
                            lastFreeRange.Length = newSize - lastFreeRange.Start;
                            freeRangesBuffer[freeRangesBuffer.Length - 1] = lastFreeRange;
                        }

                        poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
                    }
                }
            }
        }
        
        public static void Trim<T>(ref DynamicBuffer<T> poolBuffer, ref DynamicBuffer<IndexRange> freeRangesBuffer, bool trimCapacity = false)
            where T : unmanaged, IPoolElement
        {
            Resize(ref poolBuffer, ref freeRangesBuffer, 1);
            
            if (trimCapacity)
            {
                poolBuffer.Capacity = poolBuffer.Length;
            }
        }
        #endregion
        

        #region NativeList
        public static void Init<T>(ref NativeList<T> poolBuffer, ref NativeList<IndexRange> freeRangesBuffer, int initialCapacity)
            where T : unmanaged, IPoolElement
        {
            Resize(ref poolBuffer, ref freeRangesBuffer, initialCapacity);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref NativeList<T> poolBuffer, PoolElementHandle poolElementHandle)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                return Exists(poolBuffer[poolElementHandle.Index]);
            }

            return false;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObject<T>(
            ref NativeList<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            out T poolObject)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    poolObject = existingObject;
                    return true;
                }
            }

            poolObject = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T TryGetObjectRef<T>(
            ref NativeList<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    ref T poolObject = 
                        ref UnsafeUtility.ArrayElementAsRef<T>(poolBuffer.GetUnsafePtr(), poolElementHandle.Index);
                    success = true;
                    return ref poolObject;
                }
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetObject<T>(
            ref NativeList<T> poolBuffer,
            PoolElementHandle poolElementHandle,
            T poolObject)
            where T : unmanaged, IPoolElement
        {
            if (poolElementHandle.Exists() && poolElementHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[poolElementHandle.Index];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    poolObject.Version = poolElementHandle.Version;
                    poolBuffer[poolElementHandle.Index] = poolObject;
                    return true;
                }
            }

            return false;
        }

        public static void AddElement<T>(
            ref NativeList<T> poolBuffer, 
            ref NativeList<IndexRange> freeRangesBuffer,
            T newObject, 
            out PoolElementHandle poolElementHandle,
            float growFactor = 1.5f)
            where T : unmanaged, IPoolElement
        {
            int addIndex = -1;
            
            // Find first free range that can accomodate the object
            if (freeRangesBuffer.Length > 0)
            {
                IndexRange firstFreeRange = freeRangesBuffer[0];
                
                addIndex = firstFreeRange.Start;
                firstFreeRange.Start++;
                firstFreeRange.Length--;
                
                if (firstFreeRange.Length == 0)
                {
                    freeRangesBuffer.RemoveAt(0);
                }
                else
                {
                    freeRangesBuffer[0] = firstFreeRange;
                }
            }
            // If no more free ranges, grow
            else
            {
                int newCapacity = math.max((int)math.ceil(poolBuffer.Length * growFactor),
                    poolBuffer.Length + 1);
                Resize(ref poolBuffer, ref freeRangesBuffer, newCapacity);

                int lastRangeIndex = freeRangesBuffer.Length - 1;
                IndexRange lastIndexRange = freeRangesBuffer[lastRangeIndex];

                addIndex = lastIndexRange.Start;
                lastIndexRange.Start++;
                lastIndexRange.Length--;
                
                if (lastIndexRange.Length == 0)
                {
                    freeRangesBuffer.RemoveAt(lastRangeIndex);
                }
                else
                {
                    freeRangesBuffer[lastRangeIndex] = lastIndexRange;
                }
            }
            
            Assert.IsTrue(addIndex >= 0);
            
            // Write element
            T existingObject = poolBuffer[addIndex];
            newObject.Version = -existingObject.Version + 1; // Flip and increment version
            poolBuffer[addIndex] = newObject;
            
            poolElementHandle = new PoolElementHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
        }

        public static bool TryRemoveObject<T>(
            ref NativeList<T> poolBuffer, 
            ref NativeList<IndexRange> freeRangesBuffer,
            PoolElementHandle poolElementHandle)
            where T : unmanaged, IPoolElement
        {
            int removedElementIndex = poolElementHandle.Index;
            if (poolElementHandle.Exists() && removedElementIndex < poolBuffer.Length)
            {
                T existingObject = poolBuffer[removedElementIndex];
                if (existingObject.Version == poolElementHandle.Version)
                {
                    // Clear element
                    existingObject.Version = -existingObject.Version; // flip version
                    poolBuffer[removedElementIndex] = existingObject;
                    
                    // Add back free range
                    bool addedToFreeRanges = false;
                    for (int i = 0; i < freeRangesBuffer.Length; i++)
                    {
                        IndexRange iteratedIndexRange = freeRangesBuffer[i];

                        // If the freed index is right before the iterated range starts, expand the range backward and
                        // check to merge with a previous
                        if (removedElementIndex == iteratedIndexRange.Start - 1)
                        {
                            // Grow range backward if removed range starts earlier
                            int iteratedRangeLastIndex = iteratedIndexRange.Start + iteratedIndexRange.Length - 1;
                            iteratedIndexRange.Start = math.min(iteratedIndexRange.Start, removedElementIndex);
                            iteratedIndexRange.Length = iteratedRangeLastIndex - iteratedIndexRange.Start + 1;

                            // Check for merge if prev range ends right before the new expanded iterated range
                            CheckMergeWithPrevRange<T>(ref freeRangesBuffer, ref i, ref iteratedIndexRange, iteratedRangeLastIndex);

                            // Write back iterated free range
                            freeRangesBuffer[i] = iteratedIndexRange;

                            addedToFreeRanges = true;
                            break;
                        }
                        // If the ranges don't touch and the iterated range is completely past the freed range, insert 
                        // new free range
                        else if (removedElementIndex < iteratedIndexRange.Start - 1)
                        {
                            freeRangesBuffer.InsertRange(i, 1);
                            IndexRange newIndexRange = new IndexRange
                            {
                                Start = removedElementIndex,
                                Length = 1,
                            };

                            // Check for merge if prev range ends right before the new expanded iterated range
                            CheckMergeWithPrevRange<T>(ref freeRangesBuffer, ref i, ref newIndexRange, newIndexRange.Start + newIndexRange.Length - 1);
                            
                            freeRangesBuffer[i] = newIndexRange;

                            addedToFreeRanges = true;
                            break;
                        }
                    }

                    // If we haven't added to an existing range or in-between existing ranges, add new range at the end
                    if (!addedToFreeRanges)
                    {
                        IndexRange newIndexRange = new IndexRange
                        {
                            Start = removedElementIndex,
                            Length = 1,
                        };
                        
                        int addIndex = freeRangesBuffer.Length;
                        CheckMergeWithPrevRange<T>(ref freeRangesBuffer, ref addIndex, ref newIndexRange, newIndexRange.Start + newIndexRange.Length - 1);
                        
                        freeRangesBuffer.Add(newIndexRange);
                    }
                    
                    return true;
                }
            }

            return false;
        }

        private static void CheckMergeWithPrevRange<T>(
            ref NativeList<IndexRange> freeRangesBuffer,
            ref int i,
            ref IndexRange currentIndexRange,
            int curentRangeLastIndex)
            where T : unmanaged, IPoolElement
        {
            // Check for merge if prev range ends right before the new expanded iterated range
            if (i > 0)
            {
                IndexRange prevFreeIndexRange = freeRangesBuffer[i - 1];
                int prevRangeLastIndex = prevFreeIndexRange.Start + prevFreeIndexRange.Length - 1;
                if (prevRangeLastIndex == currentIndexRange.Start - 1)
                {
                    currentIndexRange.Start = prevFreeIndexRange.Start;
                    currentIndexRange.Length = curentRangeLastIndex - currentIndexRange.Start + 1;
                    freeRangesBuffer.RemoveAt(i - 1);
                    i--; // decrement index to compensate for element remove
                }
            }
        }

        public static void Resize<T>(ref NativeList<T> poolBuffer, ref NativeList<IndexRange> freeRangesBuffer, int newSize)
            where T : unmanaged, IPoolElement
        {
            int initialSize = poolBuffer.Length;
            
            // Grow
            if (newSize > poolBuffer.Length)
            {
                int addedSize = newSize - initialSize;
                
                poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
                
                // Add free range
                if (freeRangesBuffer.Length > 0)
                {
                    IndexRange lastFreeRange = freeRangesBuffer[freeRangesBuffer.Length - 1];

                    // Check if we can just expand last range (if it ended at the capacity before resize)
                    if (lastFreeRange.Start + lastFreeRange.Length == initialSize)
                    {
                        lastFreeRange.Length += addedSize;
                        freeRangesBuffer[freeRangesBuffer.Length - 1] = lastFreeRange;
                    }
                    // If not, add new range
                    else
                    {
                        freeRangesBuffer.Add(new IndexRange
                            { Start = initialSize, Length = addedSize });
                    }
                }
                // If there were no free ranges left at all, add new one
                else
                {
                    freeRangesBuffer.Add(new IndexRange
                        { Start = initialSize, Length = addedSize });
                }
            }
            // Trim
            else if (newSize < poolBuffer.Length)
            {
                if (freeRangesBuffer.Length > 0)
                {
                    IndexRange lastFreeRange = freeRangesBuffer[freeRangesBuffer.Length - 1];
                    int lastFreeIndex = lastFreeRange.Start + lastFreeRange.Length - 1;
                
                    // If the last free index reaches the end of the buffer, there is potential for trimming
                    if (lastFreeIndex == poolBuffer.Length - 1)
                    {
                        // Determine a new size that can't go lower than the last allocated element
                        newSize = math.max(lastFreeRange.Start, newSize);
                        int newLastIndex = newSize - 1;
                        
                        // If the new last index is fully before the last range, remove entire range
                        if (newLastIndex < lastFreeRange.Start)
                        {
                            freeRangesBuffer.RemoveAt(freeRangesBuffer.Length - 1);
                        }
                        // Otherwise, shrink range
                        else
                        {
                            lastFreeRange.Length = newSize - lastFreeRange.Start;
                            freeRangesBuffer[freeRangesBuffer.Length - 1] = lastFreeRange;
                        }

                        poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
                    }
                }
            }
        }
        
        public static void Trim<T>(ref NativeList<T> poolBuffer, ref NativeList<IndexRange> freeRangesBuffer, bool trimCapacity = false)
            where T : unmanaged, IPoolElement
        {
            Resize(ref poolBuffer, ref freeRangesBuffer, 1);
            
            if (trimCapacity)
            {
                poolBuffer.Capacity = poolBuffer.Length;
            }
        }
        #endregion
    }
}