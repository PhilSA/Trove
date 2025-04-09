using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Assertions;

namespace Trove
{
    public interface IPoolObject
    {
        public int Version { get; set; }
    }
    
    /// <summary>
    /// A pool of objects:
    /// - Guarantees unchanging object indexes.
    /// - Object allocation has to search through the indexes in ascending order to find the first free slot.
    /// </summary>
    public static class Pool
    {
        public struct ObjectHandle : IEquatable<ObjectHandle>
        {
            public int Index;
            public int Version;

            public static readonly ObjectHandle Null = default;
        
            public ObjectHandle(int index, int version)
            {
                Index = index;
                Version = version;
            }
        
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
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(T poolObject)
            where T : unmanaged, IPoolObject
        {
            return poolObject.Version > 0;
        }

        #region DynamicBuffer
        public static void Init<T>(ref DynamicBuffer<T> poolBuffer, int initialCapacity)
            where T : unmanaged, IPoolObject
        {
            Resize(ref poolBuffer, initialCapacity);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref DynamicBuffer<T> poolBuffer, ObjectHandle objectHandle)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                return Exists(poolBuffer[objectHandle.Index]);
            }

            return false;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObject<T>(
            ref DynamicBuffer<T> poolBuffer,
            ObjectHandle objectHandle,
            out T poolObject)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
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
            ObjectHandle objectHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
                {
                    ref T poolObject = 
                        ref UnsafeUtility.ArrayElementAsRef<T>(poolBuffer.GetUnsafePtr(), objectHandle.Index);
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
            ObjectHandle objectHandle,
            T poolObject)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
                {
                    poolObject.Version = objectHandle.Version;
                    poolBuffer[objectHandle.Index] = poolObject;
                    return true;
                }
            }

            return false;
        }

        public static void AddObject<T>(
            ref DynamicBuffer<T> poolBuffer,
            T newObject, 
            out ObjectHandle objectHandle,
            float growFactor = 1.5f)
            where T : unmanaged, IPoolObject
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
            
            objectHandle = new ObjectHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
        }

        public static bool TryRemoveObject<T>(
            ref DynamicBuffer<T> poolBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
                {
                    existingObject.Version = -existingObject.Version; // flip version
                    poolBuffer[objectHandle.Index] = existingObject;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Note: can only grow; not shrink
        /// </summary>
        public static void Resize<T>(ref DynamicBuffer<T> poolBuffer, int newSize)
            where T : unmanaged, IPoolObject
        {
            if (newSize > poolBuffer.Length)
            {
                poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            }
        }
        
        public static void Trim<T>(ref DynamicBuffer<T> poolBuffer, bool trimCapacity = false)
            where T : unmanaged, IPoolObject
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
        public static void Init<T>(ref NativeList<T> poolBuffer, int initialCapacity)
            where T : unmanaged, IPoolObject
        {
            Resize(ref poolBuffer, initialCapacity);
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exists<T>(ref NativeList<T> poolBuffer, ObjectHandle objectHandle)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                return Exists(poolBuffer[objectHandle.Index]);
            }

            return false;
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObject<T>(
            ref NativeList<T> poolBuffer,
            ObjectHandle objectHandle,
            out T poolObject)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
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
            ObjectHandle objectHandle,
            out bool success,
            ref T nullResult)
            where T : unmanaged, IPoolObject
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetObject<T>(
            ref NativeList<T> poolBuffer,
            ObjectHandle objectHandle,
            T poolObject)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
                {
                    poolObject.Version = objectHandle.Version;
                    poolBuffer[objectHandle.Index] = poolObject;
                    return true;
                }
            }

            return false;
        }

        public static void AddObject<T>(
            ref NativeList<T> poolBuffer,
            T newObject, 
            out ObjectHandle objectHandle,
            float growFactor = 1.5f)
            where T : unmanaged, IPoolObject
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
            
            objectHandle = new ObjectHandle
            {
                Index = addIndex,
                Version = newObject.Version,
            };
        }

        public static bool TryRemoveObject<T>(
            ref NativeList<T> poolBuffer,
            ObjectHandle objectHandle)
            where T : unmanaged, IPoolObject
        {
            if (objectHandle.Exists() && objectHandle.Index < poolBuffer.Length)
            {
                T existingObject = poolBuffer[objectHandle.Index];
                if (existingObject.Version == objectHandle.Version)
                {
                    existingObject.Version = -existingObject.Version; // flip version
                    poolBuffer[objectHandle.Index] = existingObject;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Note: can only grow; not shrink
        /// </summary>
        public static void Resize<T>(ref NativeList<T> poolBuffer, int newSize)
            where T : unmanaged, IPoolObject
        {
            if (newSize > poolBuffer.Length)
            {
                poolBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
            }
        }
        
        public static void Trim<T>(ref NativeList<T> poolBuffer, bool trimCapacity = false)
            where T : unmanaged, IPoolObject
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