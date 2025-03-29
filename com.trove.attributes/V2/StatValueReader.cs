using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Trove.Stats
{
    /// <summary>
    /// Used to read stat values (used in modifiers apply logic)
    /// </summary>
    public struct StatValueReader
    {
        private BufferLookup<Stat> _statsLookup;
        private Entity _latestStatsEntity;
        private DynamicBuffer<Stat> _latestStatsBuffer;

        internal StatValueReader(ref BufferLookup<Stat> statsLookup)
        {
            _statsLookup = statsLookup;
            _latestStatsEntity = Entity.Null;
            _latestStatsBuffer = default;
        }

        internal void UpdateCacheData(Entity latestStatsEntity, DynamicBuffer<Stat> latestStatsBuffer)
        {
            _latestStatsEntity = latestStatsEntity;
            _latestStatsBuffer = latestStatsBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetStatsBuffer(StatHandle statHandle, out DynamicBuffer<Stat> statsBuffer)
        {
            if (statHandle.Entity != Entity.Null &&
                (statHandle.Entity == _latestStatsEntity ||
                 _statsLookup.TryGetBuffer(statHandle.Entity, out _latestStatsBuffer)))
            {
                statsBuffer = _latestStatsBuffer;
                return true;
            }

            statsBuffer = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out Stat stat)
        {
            if (TryGetStatsBuffer(statHandle, out DynamicBuffer<Stat> statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    stat = statsBuffer[statHandle.Index];
                    return true;
                }
            }

            stat = default;
            return false;
        }
    }
}