using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Assertions;
using UnityEditor.Build;

namespace Trove.Stats
{
    public struct StatsHandler
    {
        private ComponentLookup<StatsOwner> _statsOwnerLookup;
        private BufferLookup<Stat> _statsBufferLookup;

        internal StatsHandler(ComponentLookup<StatsOwner> statsOwnerLookup, BufferLookup<Stat> statsBufferLookup)
        {
            _statsOwnerLookup = statsOwnerLookup;
            _statsBufferLookup = statsBufferLookup;
        }

        internal void OnUpdate(ref SystemState state)
        {
            _statsOwnerLookup.Update(ref state);
            _statsBufferLookup.Update(ref state);
        }

        internal ref StatsOwner TryGetStatsOwnerRef(Entity entity, out bool success, ref StatsOwner nullResult)
        {
            if(_statsOwnerLookup.HasComponent(entity))
            {
                success = true;
                return ref _statsOwnerLookup.GetRefRW(entity).ValueRW;
            }

            success = false;
            return ref nullResult;
        }

        internal bool TryGetStatsBuffer(Entity entity, out DynamicBuffer<Stat> statsBuffer)
        {
            return _statsBufferLookup.TryGetBuffer(entity, out statsBuffer);
        }

        internal ref StatsOwner TryCreateForSingleEntity(Entity entity, out SingleEntityStatsHandler singleEntityStatsHandler, out bool success, ref StatsOwner nullResult)
        {
            if (_statsOwnerLookup.HasComponent(entity))
            {
                singleEntityStatsHandler = new SingleEntityStatsHandler(entity, in _statsBufferLookup);
                success = true;
                return ref _statsOwnerLookup.GetRefRW(entity).ValueRW;
            }

            singleEntityStatsHandler = default;
            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out Stat stat)
        {
            if (statHandle.Index < FastStatsStorage.Capacity)
            {
                if (_statsOwnerLookup.TryGetComponent(statHandle.Entity, out StatsOwner statOwner))
                {
                    stat = statOwner.FastStatsStorage[statHandle.Index];
                    return true;
                }
            }
            else if (_statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> datasBuffer))
            {
                int indexInBuffer = statHandle.Index - FastStatsStorage.Capacity;
                if (indexInBuffer < datasBuffer.Length)
                {
                    stat = datasBuffer[indexInBuffer];
                    return true;
                }
            }

            stat = default;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe ref Stat GetStatRefUnsafe(StatHandle statHandle, out bool success, ref Stat nullResult)
        {
            if (statHandle.Index < FastStatsStorage.Capacity)
            {
                if (_statsOwnerLookup.HasComponent(statHandle.Entity))
                {
                    success = true;
                    ref StatsOwner statsOwnerRef = ref _statsOwnerLookup.GetRefRW(statHandle.Entity).ValueRW;
                    return ref statsOwnerRef.FastStatsStorage.GetElementAsRefUnsafe(statHandle.Index);
                }
            }
            else if (_statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                int indexInBuffer = statHandle.Index - FastStatsStorage.Capacity;
                if (indexInBuffer < statsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), indexInBuffer);
                }
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TrySetStat(StatHandle statHandle, Stat stat)
        {
            if (statHandle.Index < FastStatsStorage.Capacity)
            {
                if (_statsOwnerLookup.TryGetComponent(statHandle.Entity, out StatsOwner statOwner))
                {
                    statOwner.FastStatsStorage[statHandle.Index] = stat;
                    return true;
                }
            }
            else if (_statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> datasBuffer))
            {
                int indexInBuffer = statHandle.Index - FastStatsStorage.Capacity;
                if (indexInBuffer < datasBuffer.Length)
                {
                    datasBuffer[indexInBuffer] = stat;
                    return true;
                }
            }

            stat = default;
            return false;
        }
    }

    public struct SingleEntityStatsHandler
    {
        internal bool _hasLookup;
        internal Entity _cachedForEntity;
        internal BufferLookup<Stat> _statsBufferLookup;
        internal DynamicBuffer<Stat> _cachedStatsBuffer;

        public SingleEntityStatsHandler(Entity forEntity, in BufferLookup<Stat> statsBufferLookup)
        {
            _hasLookup = true;
            _cachedForEntity = forEntity;
            _statsBufferLookup = statsBufferLookup;
            _cachedStatsBuffer = default;
        }

        public SingleEntityStatsHandler(Entity forEntity, in DynamicBuffer<Stat> statsBuffer)
        {
            _hasLookup = false;
            _cachedForEntity = forEntity;
            _statsBufferLookup = default;
            _cachedStatsBuffer = statsBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetStatsCount(in StatsOwner statsOwner)
        {
            Assert.IsTrue(_hasLookup);

            if (_cachedStatsBuffer.IsCreated || _statsBufferLookup.TryGetBuffer(_cachedForEntity, out _cachedStatsBuffer))
            {
                return statsOwner.FastStatsStorage.Length + _cachedStatsBuffer.Length;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stat GetStatAtIndex(int index, in StatsOwner statsOwner)
        {
            if (index < FastStatsStorage.Capacity)
            {
                return statsOwner.FastStatsStorage[index];
            }

            if (_cachedStatsBuffer.IsCreated ||
                _statsBufferLookup.TryGetBuffer(_cachedForEntity, out _cachedStatsBuffer))
            {
                int indexInBuffer = index - FastStatsStorage.Capacity;
                return _cachedStatsBuffer[indexInBuffer];
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stat GetStat(StatHandle statHandle, in StatsOwner statsOwner)
        {
            if (statHandle.Entity != _cachedForEntity)
            {
                return default;
            }
            
            if (statHandle.Index < FastStatsStorage.Capacity)
            {
                return statsOwner.FastStatsStorage[statHandle.Index];
            }

            if (_cachedStatsBuffer.IsCreated ||
                _statsBufferLookup.TryGetBuffer(_cachedForEntity, out _cachedStatsBuffer))
            {
                int indexInBuffer = statHandle.Index - FastStatsStorage.Capacity;
                return _cachedStatsBuffer[indexInBuffer];
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, in StatsOwner statsOwner, out Stat stat)
        {
            if (statHandle.Entity != _cachedForEntity)
            {
                stat = default;
                return false;
            }
            
            if (statHandle.Index < FastStatsStorage.Capacity)
            {
                stat = statsOwner.FastStatsStorage[statHandle.Index];
                return true;
            }

            if (_cachedStatsBuffer.IsCreated ||
                _statsBufferLookup.TryGetBuffer(_cachedForEntity, out _cachedStatsBuffer))
            {
                int indexInBuffer = statHandle.Index - FastStatsStorage.Capacity;
                if (indexInBuffer < _cachedStatsBuffer.Length)
                {
                    stat = _cachedStatsBuffer[indexInBuffer];
                    return true;
                }
            }

            stat = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStatAtIndex(int index, Stat stat, ref StatsOwner statsOwner)
        {
            if (index < FastStatsStorage.Capacity)
            {
                statsOwner.FastStatsStorage[index] = stat;
                return;
            }

            if (_cachedStatsBuffer.IsCreated ||
                _statsBufferLookup.TryGetBuffer(_cachedForEntity, out _cachedStatsBuffer))
            {
                int indexInBuffer = index - FastStatsStorage.Capacity;
                _cachedStatsBuffer[indexInBuffer] = stat;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStat(StatHandle statHandle, Stat stat, ref StatsOwner statsOwner)
        {
            if (statHandle.Entity != _cachedForEntity)
            {
                return;
            }
            
            if (statHandle.Index < FastStatsStorage.Capacity)
            {
                statsOwner.FastStatsStorage[statHandle.Index] = stat;
                return;
            }

            if (_cachedStatsBuffer.IsCreated ||
                _statsBufferLookup.TryGetBuffer(_cachedForEntity, out _cachedStatsBuffer))
            {
                int indexInBuffer = statHandle.Index - FastStatsStorage.Capacity;
                _cachedStatsBuffer[indexInBuffer] = stat;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStat(StatHandle statHandle, Stat stat, ref StatsOwner statsOwner)
        {
            if (statHandle.Entity != _cachedForEntity)
            {
                stat = default;
                return false;
            }
            
            if (statHandle.Index < FastStatsStorage.Capacity)
            {
                statsOwner.FastStatsStorage[statHandle.Index] = stat;
                return true;
            }

            if (_cachedStatsBuffer.IsCreated ||
                _statsBufferLookup.TryGetBuffer(_cachedForEntity, out _cachedStatsBuffer))
            {
                int indexInBuffer = statHandle.Index - FastStatsStorage.Capacity;
                if (indexInBuffer < _cachedStatsBuffer.Length)
                {
                    _cachedStatsBuffer[indexInBuffer] = stat;
                    return true;
                }
            }

            stat = default;
            return false;
        }
    }
}