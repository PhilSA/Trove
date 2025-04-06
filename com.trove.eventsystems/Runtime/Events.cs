using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Burst.Intrinsics;

[assembly: RegisterGenericJobType(typeof(Trove.EventSystems.EventClearListJob<byte>))]

namespace Trove.EventSystems
{
    public interface IEventForEntity<T> where T : unmanaged, IBufferElementData
    {
        public Entity AffectedEntity { get; set; }
        public T Event { get; set; }
    }
    
    public interface IPolyByteArrayEventForEntity<T> where T : unmanaged, IPolymorphicObject
    {
        public Entity AffectedEntity { get; set; }
        public T Event { get; set; }
    }
    
    [BurstCompile]
    public struct EventClearListJob<E> : IJob
        where E : unmanaged // The event struct
    {
        public NativeList<E> EventList;

        public void Execute()
        {
            EventList.Clear();
        }
    }

    [BurstCompile]
    public struct EventClearBuffersJob<B, H> : IJobChunk
        where B : unmanaged, IBufferElementData // The event buffer element
        where H : unmanaged, IComponentData, IEnableableComponent // The enableable component that signals presence of events on buffer entities
    {
        public BufferTypeHandle<B> EventBufferType;
        public ComponentTypeHandle<H> HasEventType;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (chunkEnabledMask.ULong0 > 0 || chunkEnabledMask.ULong1 > 0)
            {
                BufferAccessor<B> eventsBufferAccessor = chunk.GetBufferAccessor(ref EventBufferType);
                EnabledMask doesEntityHaveEvents = chunk.GetEnabledMask(ref HasEventType);
                ChunkEntityEnumerator entityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (entityEnumerator.NextEntityIndex(out int i))
                {
                    if (!doesEntityHaveEvents[i])
                        continue;

                    DynamicBuffer<B> eventsBuffer = eventsBufferAccessor[i];
                    eventsBuffer.Clear();
                    doesEntityHaveEvents[i] = false;
                }
            }
        }
    }
}