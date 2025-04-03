using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Trove.Stats
{
    /// <summary>
    /// Useful for making fast stat changes, potentially in parallel,
    /// and then deferring the stats update to a later single-thread job
    /// NOTE: clears the list.
    /// </summary>
    [BurstCompile]
    public struct DeferredStatsUpdateListJob<TStatModifier, TStatModifierStack> : IJob
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public StatsAccessor<TStatModifier, TStatModifierStack> StatsAccessor;
        public StatsWorldData<TStatModifierStack> StatsWorldData;
        public NativeList<StatHandle> StatsToUpdate;
        
        public void Execute()
        {
            for (int i = 0; i < StatsToUpdate.Length; i++)
            {
                StatsAccessor.TryUpdateStat(StatsToUpdate[i], ref StatsWorldData);
            }
            StatsToUpdate.Clear();
        }
    }

    /// <summary>
    /// Useful for making fast stat changes, potentially in parallel,
    /// and then deferring the stats update to a later single-thread job.
    /// NOTE: clears the queue.
    /// </summary>
    [BurstCompile]
    public struct DeferredStatsUpdateQueueJob<TStatModifier, TStatModifierStack> : IJob
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public StatsAccessor<TStatModifier, TStatModifierStack> StatsAccessor;
        public StatsWorldData<TStatModifierStack> StatsWorldData;
        public NativeQueue<StatHandle> StatsToUpdate;
        
        public void Execute()
        {
            while(StatsToUpdate.TryDequeue(out StatHandle statHandle))
            {
                StatsAccessor.TryUpdateStat(statHandle, ref StatsWorldData);
            }
            StatsToUpdate.Clear();
        }
    }

    /// <summary>
    /// Useful for making fast stat changes, potentially in parallel,
    /// and then deferring the stats update to a later single-thread job.
    /// NOTE: you must dispose the stream afterwards.
    /// </summary>
    [BurstCompile]
    public struct DeferredStatsUpdateStreamJob<TStatModifier, TStatModifierStack> : IJob
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public StatsAccessor<TStatModifier, TStatModifierStack> StatsAccessor;
        public StatsWorldData<TStatModifierStack> StatsWorldData;
        public NativeStream.Reader StatsToUpdate;
        
        public void Execute()
        {
            for (int i = 0; i < StatsToUpdate.ForEachCount; i++)
            {
                StatsToUpdate.BeginForEachIndex(i);
                while (StatsToUpdate.RemainingItemCount > 0)
                {
                    StatHandle statHandle = StatsToUpdate.Read<StatHandle>();
                    StatsAccessor.TryUpdateStat(statHandle, ref StatsWorldData);
                }
                StatsToUpdate.EndForEachIndex();
            }
        }
    }
}