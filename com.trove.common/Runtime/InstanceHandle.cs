using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Trove
{
    /// <summary>
    /// Serves as a better-performing but baking-incompatible alternative to UnityObjectRef
    /// </summary>
    public struct InstanceHandle<T>
    {
        public int Index;
        public int Version;

        private static T[] Instances;
        private static int[] Versions;
        private static List<int> FreeIndexes;
        
        private const float GrowFactor = 1.5f;

        public static void Init(int initialCapacity)
        {
            Instances = new T[initialCapacity];
            Versions = new int[initialCapacity];
            FreeIndexes = new List<int>(initialCapacity);
            for (int i = initialCapacity - 1; i >= 0; i--)
            {
                FreeIndexes.Add(i);
            }
        }

        public static InstanceHandle<T> Add(T obj)
        {
            // Grow
            if(FreeIndexes.Count <= 0)
            {
                int oldSize = Instances.Length;
                int newSize = (int)math.ceil(Instances.Length * GrowFactor);
                Array.Resize(ref Instances, newSize);
                Array.Resize(ref Versions, newSize);

                for (int i = oldSize; i < newSize; i++)
                {
                    Versions[i] = 0;
                    FreeIndexes.Insert(0, i);
                }
            }

            int addIndex = FreeIndexes[FreeIndexes.Count - 1];
            FreeIndexes.RemoveAt(FreeIndexes.Count - 1);
            
            Instances[addIndex] = obj;
            int version = Versions[addIndex];
            version = -version + 1;
            Versions[addIndex] = version;

            return new InstanceHandle<T>
            {
                Index = addIndex,
                Version = version,
            };
        }

        public static void Remove(InstanceHandle<T> handle)
        {
            if (Exists(handle))
            {
                Versions[handle.Index] = -handle.Version;

                for (int i = FreeIndexes.Count - 1; i >= 0; i--)
                {
                    int freeIndex = FreeIndexes[i];
                    if (freeIndex > handle.Index)
                    {
                        if (i == FreeIndexes.Count - 1)
                        {
                            FreeIndexes.Add(handle.Index);
                        }
                        else
                        {
                            FreeIndexes.Insert(i + 1, handle.Index);
                        }
                        break;
                    }
                }
            }
        }

        public static bool TryGet(InstanceHandle<T> handle, out T obj)
        {
            if (Exists(handle))
            {
                obj = Instances[handle.Index];
                return true;
            }

            obj = default;
            return false;
        }

        public static bool TrySet(InstanceHandle<T> handle, T obj)
        {
            if (Exists(handle))
            {
                Instances[handle.Index] = obj;
                return true;
            }
            
            return false;
        }

        public static bool Exists(InstanceHandle<T> handle)
        {
            if (handle.Index < Instances.Length)
            {
                int version = Versions[handle.Index];
                if (handle.Version == version)
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}