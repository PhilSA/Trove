using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove
{
    public interface IMultiLinkedListPoolObject
    {
        public int Version { get; set; }
        public MultiLinkedListPool.ObjectHandle PrevObjectHandle { get; set; }
    }

    /// <summary>
    /// Allows storing multiple independent growable lists in a single buffer.
    /// - Guarantees unchanging indexes for all objects added to lists.
    /// - Object allocation has to search through the indexes in ascending order to find the first free slot.
    /// - Each object can only be part of one -and only one- linked list (the API enforces it).
    ///
    /// ----
    /// 
    /// The main use case for this is to provide a solution to the lack of nested collections on entities. Imagine you
    /// have a `DynamicBuffer<Item>`, and each `Item` needs a list of `Effect`s, and `Item`s will gain and lose
    /// `Effect`s during play. You could choose to give each `Item` an Entity that stores a `DynamicBuffer<Effect>`,
    /// but then you have to pay the price of a buffer lookup for each item when accessing `Effect`s. You could choose
    /// to store `Effect`s in a `FixedList` in `Item`, but the storage size of that `FixedList` would be limited, and
    /// it would no doubt make iterating your `Item`s less efficient if you pick a worst-case-scenario `FixedList` size.
    ///
    /// `MultiLinkedListPool` is an alternative
    /// </summary>
    public struct MultiLinkedListPool
    {
        public ObjectHandle LastObjectHandle;

        public struct ObjectHandle : IEquatable<ObjectHandle>
        {
            public int Index;
            public int Version;

            public bool Exists()
            {
                return Version > 0 && Index >= 0;
            }

            public bool Equals(ObjectHandle other)
            {
                return Index == other.Index && Version == other.Version;
            }

            public override bool Equals(object obj)
            {
                return obj is ObjectHandle other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Index, Version);
            }

            public static bool operator ==(ObjectHandle left, ObjectHandle right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(ObjectHandle left, ObjectHandle right)
            {
                return !left.Equals(right);
            }
        }

        public struct Iterator<T>
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            internal int _prevPrevIteratedObjectIndex;
            internal int _prevIteratedObjectIndex;
            internal ObjectHandle _iteratedObjectHandle;

            public bool GetNext(ref DynamicBuffer<T> poolBuffer, out T iteratedObject,
                out ObjectHandle iteratedObjectHandle)
            {
                if (_iteratedObjectHandle.Exists() && _iteratedObjectHandle.Index < poolBuffer.Length)
                {
                    iteratedObject = poolBuffer[_iteratedObjectHandle.Index];
                    if (iteratedObject.Version == _iteratedObjectHandle.Version)
                    {
                        iteratedObjectHandle = _iteratedObjectHandle;
                        return true;
                    }

                    _prevPrevIteratedObjectIndex = _prevIteratedObjectIndex;
                    _prevIteratedObjectIndex = _iteratedObjectHandle.Index;
                    _iteratedObjectHandle = iteratedObject.PrevObjectHandle;
                }

                _prevPrevIteratedObjectIndex = -1;
                _prevIteratedObjectIndex = -1;
                iteratedObjectHandle = default;
                iteratedObject = default;
                return false;
            }

            public bool GetNext(ref NativeList<T> poolBuffer, out T iteratedObject,
                out ObjectHandle iteratedObjectHandle)
            {
                if (_iteratedObjectHandle.Exists() && _iteratedObjectHandle.Index < poolBuffer.Length)
                {
                    iteratedObject = poolBuffer[_iteratedObjectHandle.Index];
                    if (iteratedObject.Version == _iteratedObjectHandle.Version)
                    {
                        iteratedObjectHandle = _iteratedObjectHandle;
                        return true;
                    }

                    _prevPrevIteratedObjectIndex = _prevIteratedObjectIndex;
                    _prevIteratedObjectIndex = _iteratedObjectHandle.Index;
                    _iteratedObjectHandle = iteratedObject.PrevObjectHandle;
                }

                _prevPrevIteratedObjectIndex = -1;
                _prevIteratedObjectIndex = -1;
                iteratedObjectHandle = default;
                iteratedObject = default;
                return false;
            }

            public void RemoveIteratedObject(ref MultiLinkedListPool multiLinkedListPool, ref DynamicBuffer<T> poolBuffer)
            {
                int iteratedObjectIndex = _prevIteratedObjectIndex;
                if (iteratedObjectIndex >= 0)
                {
                    int prevIteratedObjectIndex = _prevPrevIteratedObjectIndex;
                    T iteratedObject = poolBuffer[iteratedObjectIndex];

                    if (prevIteratedObjectIndex >= 0)
                    {
                        // Update prev element of the element coming after the iterated element
                        T prevIteratedObject = poolBuffer[prevIteratedObjectIndex];
                        prevIteratedObject.PrevObjectHandle = iteratedObject.PrevObjectHandle;
                        poolBuffer[prevIteratedObjectIndex] = prevIteratedObject;
                    }
                    else
                    {
                        // Update sublist last element if we removed the last element
                        multiLinkedListPool.LastObjectHandle = iteratedObject.PrevObjectHandle;
                    }

                    // Write removed element
                    iteratedObject.Version = -iteratedObject.Version; // flip version
                    poolBuffer[iteratedObjectIndex] = iteratedObject;
                }
            }

            public void RemoveIteratedObject(ref MultiLinkedListPool multiLinkedListPool, ref NativeList<T> poolBuffer)
            {
                int iteratedObjectIndex = _prevIteratedObjectIndex;
                if (iteratedObjectIndex >= 0)
                {
                    int prevIteratedObjectIndex = _prevPrevIteratedObjectIndex;
                    T iteratedObject = poolBuffer[iteratedObjectIndex];

                    if (prevIteratedObjectIndex >= 0)
                    {
                        // Update prev element of the element coming after the iterated element
                        T prevIteratedObject = poolBuffer[prevIteratedObjectIndex];
                        prevIteratedObject.PrevObjectHandle = iteratedObject.PrevObjectHandle;
                        poolBuffer[prevIteratedObjectIndex] = prevIteratedObject;
                    }
                    else
                    {
                        // Update sublist last element if we removed the last element
                        multiLinkedListPool.LastObjectHandle = iteratedObject.PrevObjectHandle;
                    }

                    // Write removed element
                    iteratedObject.Version = -iteratedObject.Version; // flip version
                    poolBuffer[iteratedObjectIndex] = iteratedObject;
                }
            }
        }

        public static MultiLinkedListPool Create()
        {
            return new MultiLinkedListPool
            {
                LastObjectHandle = new ObjectHandle
                {
                    Index = -1,
                    Version = 0,
                },
            };
        }

        public static Iterator<T> GetIterator<T>(MultiLinkedListPool listPool)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            return new Iterator<T>
            {
                _prevIteratedObjectIndex = -1,
                _iteratedObjectHandle = listPool.LastObjectHandle,
            };
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(T poolObject)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            return poolObject.Version > 0;
        }
        
        #region DynamicBuffer
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObject<T>(ref DynamicBuffer<T> poolBuffer, ObjectHandle objectHandle,
            out T existingObject)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
                {
                    return true;
                }
            }

            existingObject = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T TryGetObjectRef<T>(
            ref DynamicBuffer<T> poolBuffer,
            ObjectHandle objectHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                ref T existingObject =
                    ref UnsafeUtility.ArrayElementAsRef<T>(poolBuffer.GetUnsafePtr(), objectHandle.Index);
                if (existingObject.Version == objectHandle.Version)
                {
                    success = true;
                    return ref existingObject;
                }
            }

            success = false;
            return ref nullResult;
        }

        public static void AddObject<T>(ref MultiLinkedListPool listPool, ref DynamicBuffer<T> poolBuffer, T newObject,
            out ObjectHandle objectHandle, float growFactor = 1.5f)
            where T : unmanaged, IMultiLinkedListPoolObject
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
            newObject.PrevObjectHandle = listPool.LastObjectHandle;
            poolBuffer[addIndex] = newObject;
            
            objectHandle = new ObjectHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
            
            listPool.LastObjectHandle = objectHandle;
        }

        public static bool TryRemoveObject<T>(ref MultiLinkedListPool listPool, ref DynamicBuffer<T> poolBuffer, ObjectHandle objectHandle)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (listPool.LastObjectHandle.Exists() && objectHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>(listPool);
                while (iterator.GetNext(ref poolBuffer, out T iteratedObject,
                           out ObjectHandle iteratedObjectHandle))
                {
                    if (iteratedObjectHandle == objectHandle)
                    {
                        iterator.RemoveIteratedObject(ref listPool, ref poolBuffer);
                        return true;
                    }
                }
            }

            return false;
        }

        public static void Clear<T>(ref MultiLinkedListPool listPool, ref DynamicBuffer<T> poolBuffer)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (listPool.LastObjectHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>(listPool);
                while (iterator.GetNext(ref poolBuffer, out _, out _))
                {
                    iterator.RemoveIteratedObject(ref listPool, ref poolBuffer);
                }
            }
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref DynamicBuffer<T> poolBuffer, ObjectHandle objectHandle)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                return Exists(poolBuffer[objectHandle.Index]);
            }

            return false;
        }

        /// <summary>
        /// Note: can only grow; not shrink
        /// </summary>
        public static void Resize<T>(ref DynamicBuffer<T> poolBuffer, int newSize)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (newSize > poolBuffer.Length)
            {
                poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            }
        }
        
        public static void Trim<T>(ref DynamicBuffer<T> poolBuffer, bool trimCapacity = false)
            where T : unmanaged, IMultiLinkedListPoolObject
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
                }
            }
        }
        #endregion

        #region NativeList
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObject<T>(ref NativeList<T> poolBuffer, ObjectHandle objectHandle,
            out T existingObject)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
                {
                    return true;
                }
            }

            existingObject = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T TryGetObjectRef<T>(
            ref NativeList<T> poolBuffer,
            ObjectHandle objectHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                ref T existingObject =
                    ref UnsafeUtility.ArrayElementAsRef<T>(poolBuffer.GetUnsafePtr(), objectHandle.Index);
                if (existingObject.Version == objectHandle.Version)
                {
                    success = true;
                    return ref existingObject;
                }
            }

            success = false;
            return ref nullResult;
        }

        public static void AddObject<T>(ref MultiLinkedListPool listPool, ref NativeList<T> poolBuffer, T newObject,
            out ObjectHandle objectHandle, float growFactor = 1.5f)
            where T : unmanaged, IMultiLinkedListPoolObject
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
            newObject.PrevObjectHandle = listPool.LastObjectHandle;
            poolBuffer[addIndex] = newObject;
            
            objectHandle = new ObjectHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
            
            listPool.LastObjectHandle = objectHandle;
        }

        public static bool TryRemoveObject<T>(ref MultiLinkedListPool listPool, ref NativeList<T> poolBuffer, ObjectHandle objectHandle)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (listPool.LastObjectHandle.Exists() && objectHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>(listPool);
                while (iterator.GetNext(ref poolBuffer, out T iteratedObject,
                           out ObjectHandle iteratedObjectHandle))
                {
                    if (iteratedObjectHandle == objectHandle)
                    {
                        iterator.RemoveIteratedObject(ref listPool, ref poolBuffer);
                        return true;
                    }
                }
            }

            return false;
        }

        public static void Clear<T>(ref MultiLinkedListPool listPool, ref NativeList<T> poolBuffer)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (listPool.LastObjectHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>(listPool);
                while (iterator.GetNext(ref poolBuffer, out _, out _))
                {
                    iterator.RemoveIteratedObject(ref listPool, ref poolBuffer);
                }
            }
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref NativeList<T> poolBuffer, ObjectHandle objectHandle)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                return Exists(poolBuffer[objectHandle.Index]);
            }

            return false;
        }

        /// <summary>
        /// Note: can only grow; not shrink
        /// </summary>
        public static void Resize<T>(ref NativeList<T> poolBuffer, int newSize)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (newSize > poolBuffer.Length)
            {
                poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            }
        }
        
        public static void Trim<T>(ref NativeList<T> poolBuffer, bool trimCapacity = false)
            where T : unmanaged, IMultiLinkedListPoolObject
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
                }
            }
        }
        #endregion
    }
}