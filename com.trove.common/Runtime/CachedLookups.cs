using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Trove
{
    public struct CachedBufferLookup<T> where T : unmanaged, IBufferElementData
    {
        private Entity _latestBufferEntity;
        private BufferLookup<T> _bufferLookup;
        private DynamicBuffer<T> _cachedBuffer;

        public CachedBufferLookup(BufferLookup<T> bufferLookup)
        {
            _latestBufferEntity = Entity.Null;
            _bufferLookup = bufferLookup;
            _cachedBuffer = default;
        }

        public void Update(ref SystemState state)
        {
            _bufferLookup.Update(ref state);
            _latestBufferEntity = Entity.Null;
            _cachedBuffer = default;
        }

        public BufferLookup<T> GetLookup()
        {
            return _bufferLookup;
        }

        public void CopyCachedData(CachedBufferLookup<T> otherCachedLookup)
        {
            _latestBufferEntity = otherCachedLookup._latestBufferEntity;
            _cachedBuffer = otherCachedLookup._cachedBuffer;
        }

        public void SetCachedData(Entity entity, DynamicBuffer<T> buffer)
        {
            _latestBufferEntity = entity;
            _cachedBuffer = buffer;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetBuffer(Entity onEntity, out DynamicBuffer<T> buffer)
        {
            if (onEntity != Entity.Null)
            {
                if (onEntity == _latestBufferEntity && _cachedBuffer.IsCreated)
                {
                    buffer = _cachedBuffer;
                    return true;
                }

                bool success = _bufferLookup.TryGetBuffer(onEntity, out buffer);
                if (success)
                {
                    _latestBufferEntity = onEntity;
                    return true;
                }
            }

            buffer = default;
            return false;
        }
    }
}