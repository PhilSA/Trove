using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Trove.ObjectHandles
{
    public struct ReferenceHandle<T> where T : class
    {
        private struct ReferenceData
        {
            public T Reference;
            public int Version;
        }

        public int Index;
        /// <summary>
        /// Version 0 means invalid
        /// Version <0 means destroyed (invalid)
        /// Version >0 means valid
        /// </summary>
        public int Version;

        public static int Capacity => _references.Length;
        private static ReferenceData[] _references;
        private static Queue<int> _availableIndexes;
        private static float _growFactor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolve(out T t)
        {
            if (Version > 0 && Index >= 0 && Index < _references.Length)
            {
                ReferenceData data = _references[Index];
                if (Version == data.Version)
                {
                    t = data.Reference;
                    return true;
                }
            }

            t = null;
            return false;
        }

        public static void Initialize(int initialCapacity, float growFactor = 1.5f)
        {
            _references = new ReferenceData[0];
            _availableIndexes = new Queue<int>();
            _growFactor = growFactor;

            GrowCapacity(initialCapacity);
        }

        public static bool Register(T t, out ReferenceHandle<T> handle)
        {
            if (_availableIndexes.Count <= 0 && _growFactor > 1f)
            {
                int addedCapacity = (int)math.ceil(_references.Length * _growFactor) - _references.Length;
                GrowCapacity(addedCapacity);
            }

            if (_availableIndexes.TryDequeue(out int newIndex))
            {
                int version = math.abs(_references[newIndex].Version) + 1;
                if (version < 0)
                {
                    version = 1;
                }

                _references[newIndex] = new ReferenceData
                {
                    Reference = t,
                    Version = version,
                };

                handle = new ReferenceHandle<T>
                {
                    Index = newIndex,
                    Version = version,
                };

                return true;
            }

            handle = default;
            return false;
        }

        public static bool Unregister(ReferenceHandle<T> handle)
        {
            if (handle.Index >= 0 && handle.Index < _references.Length)
            {
                ReferenceData data = _references[handle.Index];
                if (handle.Version == data.Version)
                {
                    data.Reference = null;
                    if (data.Version > 0)
                    {
                        data.Version++;
                    }
                    _references[handle.Index] = data;
                    _availableIndexes.Enqueue(handle.Index);

                    return true;
                }
            }

            return false;
        }

        private static void GrowCapacity(int addedCapacity)
        {
            if (addedCapacity > 0)
            {
                int startLength = _references.Length;

                // Resize
                ReferenceData[] newReferences = new ReferenceData[_references.Length + addedCapacity];
                Array.Copy(_references, newReferences, _references.Length);
                _references = newReferences;

                // New available indexes
                for (int i = 0; i < addedCapacity; i++)
                {
                    _availableIndexes.Enqueue(startLength + i);
                }
            }
        }

        public static void TrimCapacity(int minCapacity)
        {
            int highestValidIndex = -1;
            for (int i = _references.Length - 1; i >= 0; i--)
            {
                if (_references[i].Version > 0)
                {
                    highestValidIndex = i;
                    break;
                }
            }

            int newCapacity = math.max(0, math.max(minCapacity, highestValidIndex));
            ReferenceData[] newReferences = new ReferenceData[newCapacity];
            Array.Copy(_references, newReferences, newCapacity);
            _references = newReferences;

            // Remove available indexes that are above new capacity
            Queue<int> newAvailableIndexes = new Queue<int>();
            while (_availableIndexes.TryDequeue(out int tmpIndex))
            {
                if (tmpIndex < _references.Length)
                {
                    newAvailableIndexes.Enqueue(tmpIndex);
                }
            }
            _availableIndexes = newAvailableIndexes;
        }
    }
}