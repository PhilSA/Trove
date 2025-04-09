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
    /// Allows storing multiple independent linked lists in a single buffer acting as a pool of objects.
    /// - Guarantees unchanging object indexes.
    /// - Object allocation has to search through the indexes in ascending order to find the first free slot.
    /// - Each object can only be part of one (and only one) linked list.
    /// Main use case is for object hierarchies, like for a hierarchical state machines where each state can have
    /// a list of child states.
    /// </summary>
    /// <typeparam name="T"></typeparam>
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

        public Iterator<T> GetIterator<T>()
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            return new Iterator<T>
            {
                _prevIteratedObjectIndex = -1,
                _iteratedObjectHandle = LastObjectHandle,
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
        public bool TryGetObject<T>(ref DynamicBuffer<T> poolBuffer, ObjectHandle objectHandle,
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
        public unsafe ref T TryGetObjectRef<T>(
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

        public void AddObject<T>(ref DynamicBuffer<T> poolBuffer, T newObject,
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
            newObject.PrevObjectHandle = LastObjectHandle;
            poolBuffer[addIndex] = newObject;
            
            objectHandle = new ObjectHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
            
            LastObjectHandle = objectHandle;
        }

        public bool TryRemoveObject<T>(ref DynamicBuffer<T> poolBuffer, ObjectHandle objectHandle)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (LastObjectHandle.Exists() && objectHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>();
                while (iterator.GetNext(ref poolBuffer, out T iteratedObject,
                           out ObjectHandle iteratedObjectHandle))
                {
                    if (iteratedObjectHandle == objectHandle)
                    {
                        iterator.RemoveIteratedObject(ref this, ref poolBuffer);
                        return true;
                    }
                }
            }

            return false;
        }

        public void Clear<T>(ref DynamicBuffer<T> poolBuffer)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (LastObjectHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>();
                while (iterator.GetNext(ref poolBuffer, out _, out _))
                {
                    iterator.RemoveIteratedObject(ref this, ref poolBuffer);
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
        public bool TryGetObject<T>(ref NativeList<T> poolBuffer, ObjectHandle objectHandle,
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
        public unsafe ref T TryGetObjectRef<T>(
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

        public void AddObject<T>(ref NativeList<T> poolBuffer, T newObject,
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
            newObject.PrevObjectHandle = LastObjectHandle;
            poolBuffer[addIndex] = newObject;
            
            objectHandle = new ObjectHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
            
            LastObjectHandle = objectHandle;
        }

        public bool TryRemoveObject<T>(ref NativeList<T> poolBuffer, ObjectHandle objectHandle)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (LastObjectHandle.Exists() && objectHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>();
                while (iterator.GetNext(ref poolBuffer, out T iteratedObject,
                           out ObjectHandle iteratedObjectHandle))
                {
                    if (iteratedObjectHandle == objectHandle)
                    {
                        iterator.RemoveIteratedObject(ref this, ref poolBuffer);
                        return true;
                    }
                }
            }

            return false;
        }

        public void Clear<T>(ref NativeList<T> poolBuffer)
            where T : unmanaged, IMultiLinkedListPoolObject
        {
            if (LastObjectHandle.Exists())
            {
                Iterator<T> iterator = GetIterator<T>();
                while (iterator.GetNext(ref poolBuffer, out _, out _))
                {
                    iterator.RemoveIteratedObject(ref this, ref poolBuffer);
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
                        poolBuffer.SetCapacity(i + 1);
                    }
                }
            }
        }
        #endregion
    }
}