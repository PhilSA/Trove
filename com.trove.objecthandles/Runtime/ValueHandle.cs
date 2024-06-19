
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Trove.ObjectHandles
{
    public struct ValueHandle
    {
        public int Index;
        /// <summary>
        /// Version 0 means invalid
        /// Version <0 means destroyed (invalid)
        /// Version >0 means valid
        /// </summary>
        public int Version;
    }

    public struct ValueStore<T> where T : unmanaged
    {
        private struct ValueData
        {
            public T Value;
            public int Version;
        }

        public int Capacity => _values.Length;
        private NativeList<ValueData> _values;
        private NativeQueue<int> _availableIndexes;
        private float _growFactor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolve(ValueHandle handle, out T t)
        {
            if (handle.Version > 0 && handle.Index >= 0 && handle.Index < _values.Length)
            {
                ValueData data = _values[handle.Index];
                if (handle.Version == data.Version)
                {
                    t = data.Value;
                    return true;
                }
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySet(ValueHandle handle, T t)
        {
            if (handle.Version > 0 && handle.Index >= 0 && handle.Index < _values.Length)
            {
                ValueData data = _values[handle.Index];
                if (handle.Version == data.Version)
                {
                    _values[handle.Index] = new ValueData
                    {
                        Value = t,
                        Version = handle.Version,
                    };
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUnsafe(ValueHandle handle, T t)
        {
            _values[handle.Index] = new ValueData
            {
                Value = t,
                Version = handle.Version,
            };
        }

        public ValueStore(int initialCapacity, float growFactor = 1.5f)
        {
            _values = new NativeList<ValueData>(0, Allocator.Persistent);
            _availableIndexes = new NativeQueue<int>(Allocator.Persistent);
            _growFactor = growFactor;

            GrowCapacity(initialCapacity);
        }

        public void Dispose()
        {
            if (_values.IsCreated)
            {
                _values.Dispose();
            }
            if (_availableIndexes.IsCreated)
            {
                _availableIndexes.Dispose();
            }
        }

        public bool Register(T t, out ValueHandle handle)
        {
            if (_availableIndexes.Count <= 0 && _growFactor > 1f)
            {
                int addedCapacity = (int)math.ceil(_values.Length * _growFactor) - _values.Length;
                GrowCapacity(addedCapacity);
            }

            if (_availableIndexes.TryDequeue(out int newIndex))
            {
                int version = math.abs(_values[newIndex].Version) + 1;
                if (version < 0)
                {
                    version = 1;
                }

                _values[newIndex] = new ValueData
                {
                    Value = t,
                    Version = version,
                };

                handle = new ValueHandle
                {
                    Index = newIndex,
                    Version = version,
                };

                return true;
            }

            handle = default;
            return false;
        }

        public bool Unregister(ValueHandle handle)
        {
            if (handle.Index >= 0 && handle.Index < _values.Length)
            {
                ValueData data = _values[handle.Index];
                if (handle.Version == data.Version)
                {
                    data.Value = default;
                    if (data.Version > 0)
                    {
                        data.Version = -data.Version;
                    }
                    _values[handle.Index] = data;
                    _availableIndexes.Enqueue(handle.Index);

                    return true;
                }
            }

            return false;
        }

        private unsafe void GrowCapacity(int addedCapacity)
        {
            if (addedCapacity > 0)
            {
                int startLength = _values.Length;

                // Resize
                _values.Resize(_values.Length + addedCapacity, NativeArrayOptions.ClearMemory);

                // New available indexes
                for (int i = 0; i < addedCapacity; i++)
                {
                    _availableIndexes.Enqueue(startLength + i);
                }
            }
        }

        public unsafe void TrimCapacity(int minCapacity)
        {
            if (minCapacity < _values.Length)
            {
                int highestValidIndex = -1;
                for (int i = _values.Length - 1; i >= 0; i--)
                {
                    if (_values[i].Version > 0)
                    {
                        highestValidIndex = i;
                        break;
                    }
                }

                int newCapacity = math.max(0, math.max(minCapacity, highestValidIndex));
                _values.Resize(newCapacity, NativeArrayOptions.ClearMemory);

                // Remove available indexes that are above new capacity
                int initialQueueLength = _availableIndexes.Count;
                for (int i = 0; i < initialQueueLength; i++)
                {
                    int tmpIndex = _availableIndexes.Dequeue();
                    if (tmpIndex < _values.Length)
                    {
                        _availableIndexes.Enqueue(tmpIndex);
                    }

                }
            }
        }
    }
}