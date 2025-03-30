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
        private byte _statsLookupExists;
        private DynamicBuffer<Stat> _cachedBuffer;

        internal StatValueReader(BufferLookup<Stat> statsLookup)
        {
            _statsLookup = statsLookup;
            _statsLookupExists = 1;
            _cachedBuffer = default;
        }

        internal StatValueReader(DynamicBuffer<Stat> cachedBuffer)
        {
            _statsLookup = default;
            _statsLookupExists = 0;
            _cachedBuffer = cachedBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out Stat stat)
        {
            if (_statsLookupExists == 1)
            {
                if (_statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
                {
                    if (statHandle.Index < statsBuffer.Length)
                    {
                        stat = statsBuffer[statHandle.Index];
                        return true;
                    }
                }
            }
            else if (statHandle.Index < _cachedBuffer.Length)
            {
                stat = _cachedBuffer[statHandle.Index];
                return true;
            }

            stat = default;
            return false;
        }
    }
}