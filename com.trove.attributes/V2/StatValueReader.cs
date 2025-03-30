using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Trove.Stats
{
    /// <summary>
    /// Used to read stat values (used in modifiers apply logic)
    /// </summary>
    public struct StatValueReader
    {
        private CachedBufferLookup<Stat> _statsCachedLookup;

        internal StatValueReader(CachedBufferLookup<Stat> statsCachedLookup)
        {
            _statsCachedLookup = statsCachedLookup;
        }

        internal void CopyCachedData(CachedBufferLookup<Stat> otherCachedLookup)
        {
            _statsCachedLookup.CopyCachedData(otherCachedLookup);
        }

        internal void SetCachedData(Entity entity, DynamicBuffer<Stat> buffer)
        {
            _statsCachedLookup.SetCachedData(entity, buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out Stat stat)
        {
            if (_statsCachedLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
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