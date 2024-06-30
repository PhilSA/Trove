using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Logging;
using Unity.Mathematics;
using UnityEngine;

namespace Trove.Stats
{
    [BurstCompile]
    public struct StatsUpdateSubSystem<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        private EntityQuery _batchRecomputeStatsQuery;
        private EntityQuery _dirtyStatsQuery;
        private EntityQuery _statsSettingsQuery;

        private ComponentLookup<DirtyStatsMask> DirtyStatsMaskLookup;
        private BufferLookup<Stat> StatsBufferLookup;
        private BufferLookup<TStatModifier> StatModifiersBufferLookupRO;
        private BufferLookup<StatObserver> StatObserversBufferLookupRO;

        private EntityTypeHandle EntityTypeHandle;
        private ComponentTypeHandle<DirtyStatsMask> DirtyStatsMaskTypeHandle;
        private BufferTypeHandle<Stat> StatsBufferTypeHandle;
        private BufferTypeHandle<Stat> StatsBufferTypeHandleRO;
        private BufferTypeHandle<TStatModifier> StatModifiersBufferTypeHandle;
        private BufferTypeHandle<TStatModifier> StatModifiersBufferTypeHandleRO;
        private BufferTypeHandle<StatObserver> StatObserversBufferTypeHandle;
        private BufferTypeHandle<StatObserver> StatObserversBufferTypeHandleRO;

        private NativeQueue<StatHandle> _tmpDirtyStatsQueue;


        private StatsSettings GetStatsSettings()
        {
            if(_statsSettingsQuery.TryGetSingleton(out StatsSettings statsSettings))
            {
                return statsSettings;
            }
            else
            {
                return StatsSettings.Default();
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _batchRecomputeStatsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<
                StatOwner,
                TStatModifier,
                StatObserver>()
                .WithAllRW<Stat>()
                .Build(ref state);
            _dirtyStatsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<
                StatOwner,
                TStatModifier,
                StatObserver>()
                .WithAllRW<Stat>()
                .WithAllRW<DirtyStatsMask>()
                .Build(ref state);
            _statsSettingsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<StatsSettings>().Build(ref state);

            state.RequireForUpdate(_batchRecomputeStatsQuery);

            DirtyStatsMaskLookup = state.GetComponentLookup<DirtyStatsMask>(false);
            StatsBufferLookup = state.GetBufferLookup<Stat>(false);
            StatModifiersBufferLookupRO = state.GetBufferLookup<TStatModifier>(true);
            StatObserversBufferLookupRO = state.GetBufferLookup<StatObserver>(true);

            EntityTypeHandle = state.EntityManager.GetEntityTypeHandle();
            DirtyStatsMaskTypeHandle = state.EntityManager.GetComponentTypeHandle<DirtyStatsMask>(false);
            StatsBufferTypeHandle = state.EntityManager.GetBufferTypeHandle<Stat>(false);
            StatsBufferTypeHandleRO = state.EntityManager.GetBufferTypeHandle<Stat>(true);
            StatModifiersBufferTypeHandle = state.EntityManager.GetBufferTypeHandle<TStatModifier>(false);
            StatModifiersBufferTypeHandleRO = state.EntityManager.GetBufferTypeHandle<TStatModifier>(true);
            StatObserversBufferTypeHandle = state.EntityManager.GetBufferTypeHandle<StatObserver>(false);
            StatObserversBufferTypeHandleRO = state.EntityManager.GetBufferTypeHandle<StatObserver>(true);

            _tmpDirtyStatsQueue = new NativeQueue<StatHandle>(Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_tmpDirtyStatsQueue.IsCreated)
            {
                _tmpDirtyStatsQueue.Dispose();
            }
        }

        [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            StatsSettings statsSettings = GetStatsSettings();

            // TODO: process stat commands

            int statEntitiesChunkCount = _batchRecomputeStatsQuery.CalculateChunkCount();

            StatsBufferLookup.Update(ref state);
            StatModifiersBufferLookupRO.Update(ref state);
            StatObserversBufferLookupRO.Update(ref state);
            EntityTypeHandle.Update(ref state);
            DirtyStatsMaskTypeHandle.Update(ref state);

            if (statsSettings.BatchRecomputeUpdatesCount > 0)
            {
                DirtyStatsMaskLookup.Update(ref state);
                StatModifiersBufferTypeHandleRO.Update(ref state);
                StatObserversBufferTypeHandleRO.Update(ref state);

                for (int i = 0; i < statsSettings.BatchRecomputeUpdatesCount; i++)
                {
                    // TODO: have a stats update group in which we can add systems that react to stat changes?

                    NativeStream markStatsDirtyStream = new NativeStream(statEntitiesChunkCount, state.WorldUpdateAllocator);

                    state.Dependency = new BatchRecomputeDirtyStatsJob
                    {
                        StatsBufferLookup = StatsBufferLookup,

                        EntityTypeHandle = EntityTypeHandle,
                        DirtyStatsMaskTypeHandle = DirtyStatsMaskTypeHandle,
                        StatModifiersBufferTypeHandle = StatModifiersBufferTypeHandleRO,
                        StatObserversBufferTypeHandle = StatObserversBufferTypeHandleRO,

                        MarkStatsDirtyStream = markStatsDirtyStream.AsWriter(),
                    }.ScheduleParallel(_dirtyStatsQuery, state.Dependency);

                    bool isLastBatch = i >= statsSettings.BatchRecomputeUpdatesCount - 1;
                    if (isLastBatch && statsSettings.EndWithRecomputeImmediate)
                    {
                        state.Dependency = new EnqueueDirtyStatsEventsForRecomputeImmediateJob
                        {
                            MarkStatsDirtyStream = markStatsDirtyStream.AsReader(),
                            TmpDirtyStatsQueue = _tmpDirtyStatsQueue.AsParallelWriter(),
                        }.Schedule(statEntitiesChunkCount, 1, state.Dependency);
                    }
                    else
                    {
                        state.Dependency = new ApplyHasDirtyStatsJob
                        {
                            DirtyStatsMaskLookup = DirtyStatsMaskLookup,
                            MarkStatsDirtyStream = markStatsDirtyStream.AsReader(),
                        }.Schedule(statEntitiesChunkCount, 1, state.Dependency);
                    }

                    markStatsDirtyStream.Dispose(state.Dependency);
                }
            }
            else if (statsSettings.EndWithRecomputeImmediate)
            {
                // Schedule a job to transfer dirty stats to recompute queue
                state.Dependency = new EnqueueDirtyStatsForRecomputeImmediateJob
                {
                    TmpDirtyStatsQueue = _tmpDirtyStatsQueue.AsParallelWriter(),

                    EntityTypeHandle = EntityTypeHandle,
                    DirtyStatsMaskTypeHandle = DirtyStatsMaskTypeHandle,
                }.ScheduleParallel(_dirtyStatsQuery, state.Dependency);
            }

            // Optional final job that recomputes remaining dirty stats and observers immediately
            if(statsSettings.EndWithRecomputeImmediate)
            {
                // TODO: infinite loop
                state.Dependency = new RecomputeDirtyStatsImmediateJob
                {
                    StatsBufferLookup = StatsBufferLookup,
                    StatModifiersBufferLookup = StatModifiersBufferLookupRO,
                    StatObserversBufferLookup = StatObserversBufferLookupRO,

                    TmpDirtyStatsQueue = _tmpDirtyStatsQueue,
                }.Schedule(state.Dependency);
            }
        }

        [BurstCompile]
        public struct BatchRecomputeDirtyStatsJob : IJobChunk
        {
            // Note:
            // -------------------------------------
            // We can disable restrictions for this lookup. 
            //
            // When a stat is dirty, we read its value from buffer, update it, and write it back.
            // However when updating it, it must read stat values potentially on other entities,
            // so it gets the stats buffer on these entities from lookup. In these cases, we could get non-deterministic
            // stat updates (race conditions).
            // 
            // However, this doesn't matter, because whenever a stat has changes, it requests its
            // observer stats to be recalculated on the next batch update (or final immediate update).
            // This means that even if there is a race condition, the stats will eventually be fully
            // recalculated at the end of the stat system update.
            [NativeDisableParallelForRestriction]
            public BufferLookup<Stat> StatsBufferLookup;

            [ReadOnly]
            public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<DirtyStatsMask> DirtyStatsMaskTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<TStatModifier> StatModifiersBufferTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<StatObserver> StatObserversBufferTypeHandle;

            [NativeDisableParallelForRestriction]
            public NativeStream.Writer MarkStatsDirtyStream;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (chunkEnabledMask.ULong0 > 0 || chunkEnabledMask.ULong1 > 0)
                {
                    NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                    NativeArray<DirtyStatsMask> dirtyStatsMasks = chunk.GetNativeArray(ref DirtyStatsMaskTypeHandle);
                    EnabledMask doesEntityHaveDirtyStats = chunk.GetEnabledMask(ref DirtyStatsMaskTypeHandle);
                    BufferAccessor<TStatModifier> statModifiersBufferAccessor = chunk.GetBufferAccessor(ref StatModifiersBufferTypeHandle);
                    BufferAccessor<StatObserver> statObserversBufferAccessor = chunk.GetBufferAccessor(ref StatObserversBufferTypeHandle);

                    void* dirtyStatsMasksArrayPtr = dirtyStatsMasks.GetUnsafePtr();

                    MarkStatsDirtyStream.BeginForEachIndex(unfilteredChunkIndex);

                    ChunkEntityEnumerator entityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                    while (entityEnumerator.NextEntityIndex(out int i))
                    {
                        Entity entity = entities[i];
                        ref DirtyStatsMask dirtyStatsMaskRef = ref UnsafeUtility.ArrayElementAsRef<DirtyStatsMask>(dirtyStatsMasksArrayPtr, i);
                        DynamicBuffer<Stat> statsBuffer = StatsBufferLookup[entity];
                        DynamicBuffer<TStatModifier> statModifiersBuffer = statModifiersBufferAccessor[i];
                        DynamicBuffer<StatObserver> statObserversBuffer = statObserversBufferAccessor[i];

                        void* statsBufferPtr = statsBuffer.GetUnsafePtr();

                        DirtyStatsMask.Iterator dirtyStatsMaskIterator = dirtyStatsMaskRef.GetIterator();
                        while (dirtyStatsMaskIterator.GetNextDirtyStat(out int statIndex))
                        {
                            StatHandle selfStatHandle = new StatHandle(entity, statIndex);
                            ref Stat stat = ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBufferPtr, statIndex);

                            // Apply Modifiers
                            TStatModifierStack modifierStack = new TStatModifierStack();
                            modifierStack.Reset();
                            for (int m = 0; m < statModifiersBuffer.Length; m++)
                            {
                                TStatModifier modifier = statModifiersBuffer[m];
                                if (selfStatHandle == modifier.AffectedStat)
                                {
                                    modifier.Apply(
                                        ref modifierStack,
                                        new StatHandle(entity, statIndex),
                                        ref statsBuffer,
                                        ref StatsBufferLookup);
                                }
                            }
                            modifierStack.Apply(ref stat);
                            dirtyStatsMaskRef.ClearBit(statIndex);

                            // Notify Observers
                            for (int o = statObserversBuffer.Length - 1; o >= 0; o--)
                            {
                                StatObserver observer = statObserversBuffer[o];
                                if (observer.ObservedStat == selfStatHandle)
                                {
                                    MarkStatsDirtyStream.Write(observer.ObserverStat);

                                    // TODO: if observer no longer exists, remove it
                                }
                            }
                        }

                        doesEntityHaveDirtyStats[i] = false;
                    }

                    MarkStatsDirtyStream.EndForEachIndex();
                }
            }
        }

        [BurstCompile]
        public struct RecomputeDirtyStatsImmediateJob : IJob
        {
            public BufferLookup<Stat> StatsBufferLookup;
            [ReadOnly]
            public BufferLookup<TStatModifier> StatModifiersBufferLookup;
            [ReadOnly]
            public BufferLookup<StatObserver> StatObserversBufferLookup;

            public NativeQueue<StatHandle> TmpDirtyStatsQueue;
            public void Execute()
            {
                StatHandle prevStatHandle = default;
                DynamicBuffer<Stat> prevStatsBuffer = default;
                DynamicBuffer<TStatModifier> prevStatModifiersBuffer = default;
                DynamicBuffer<StatObserver> prevStatObserversBuffer = default;

                while (TmpDirtyStatsQueue.TryDequeue(out StatHandle dirtyStatHandle))
                {
                    if (dirtyStatHandle.Entity != Entity.Null)
                    {
                        if (dirtyStatHandle.Entity == prevStatHandle.Entity)
                        {
                            StatUtilities.RecomputeStatAndObserversImmediateInternal<TStatModifier, TStatModifierStack>(
                                dirtyStatHandle,
                                ref prevStatsBuffer,
                                ref prevStatModifiersBuffer,
                                ref prevStatObserversBuffer,
                                ref StatsBufferLookup,
                                ref TmpDirtyStatsQueue);
                        }
                        else if (StatsBufferLookup.TryGetBuffer(dirtyStatHandle.Entity, out prevStatsBuffer))
                        {
                            prevStatModifiersBuffer = StatModifiersBufferLookup[dirtyStatHandle.Entity];
                            prevStatObserversBuffer = StatObserversBufferLookup[dirtyStatHandle.Entity];

                            StatUtilities.RecomputeStatAndObserversImmediateInternal<TStatModifier, TStatModifierStack>(
                                dirtyStatHandle,
                                ref prevStatsBuffer,
                                ref prevStatModifiersBuffer,
                                ref prevStatObserversBuffer,
                                ref StatsBufferLookup,
                                ref TmpDirtyStatsQueue);
                        }
                    }

                    prevStatHandle = dirtyStatHandle;
                }

                TmpDirtyStatsQueue.Clear();
            }
        }

        [BurstCompile]
        public unsafe struct EnqueueDirtyStatsForRecomputeImmediateJob : IJobChunk
        {
            public NativeQueue<StatHandle>.ParallelWriter TmpDirtyStatsQueue;

            [ReadOnly]
            public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<DirtyStatsMask> DirtyStatsMaskTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (chunkEnabledMask.ULong0 > 0 || chunkEnabledMask.ULong1 > 0)
                {
                    NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                    NativeArray<DirtyStatsMask> dirtyStatsMasks = chunk.GetNativeArray(ref DirtyStatsMaskTypeHandle);
                    EnabledMask doesEntityHaveDirtyStats = chunk.GetEnabledMask(ref DirtyStatsMaskTypeHandle);

                    void* dirtyStatsMasksArrayPtr = dirtyStatsMasks.GetUnsafePtr();

                    ChunkEntityEnumerator entityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                    while (entityEnumerator.NextEntityIndex(out int i))
                    {
                        Entity entity = entities[i];
                        ref DirtyStatsMask dirtyStatsMaskRef = ref UnsafeUtility.ArrayElementAsRef<DirtyStatsMask>(dirtyStatsMasksArrayPtr, i);

                        DirtyStatsMask.Iterator dirtyStatsMaskIterator = dirtyStatsMaskRef.GetIterator();
                        while (dirtyStatsMaskIterator.GetNextDirtyStat(out int statIndex))
                        {
                            StatHandle selfStatHandle = new StatHandle(entity, statIndex);
                            TmpDirtyStatsQueue.Enqueue(selfStatHandle);

                            dirtyStatsMaskRef.ClearBit(statIndex);
                        }

                        doesEntityHaveDirtyStats[i] = false;
                    }
                }
            }
        }

        [BurstCompile]
        public struct ApplyHasDirtyStatsJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public ComponentLookup<DirtyStatsMask> DirtyStatsMaskLookup;
            [NativeDisableParallelForRestriction]
            public NativeStream.Reader MarkStatsDirtyStream;

            public void Execute(int index)
            {
                MarkStatsDirtyStream.BeginForEachIndex(index);

                while (MarkStatsDirtyStream.RemainingItemCount > 0)
                {
                    ref StatHandle dirtyStatHandle = ref MarkStatsDirtyStream.Read<StatHandle>();

                    StatUtilities.MarkStatForBatchRecompute(dirtyStatHandle.Index,
                        ref DirtyStatsMaskLookup.GetRefRW(dirtyStatHandle.Entity).ValueRW,
                        DirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(dirtyStatHandle.Entity));
                }

                MarkStatsDirtyStream.EndForEachIndex();
            }
        }
    }

    [BurstCompile]
    public struct EnqueueDirtyStatsEventsForRecomputeImmediateJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public NativeStream.Reader MarkStatsDirtyStream;
        public NativeQueue<StatHandle>.ParallelWriter TmpDirtyStatsQueue;

        public void Execute(int index)
        {
            MarkStatsDirtyStream.BeginForEachIndex(index);

            while (MarkStatsDirtyStream.RemainingItemCount > 0)
            {
                ref StatHandle dirtyStatHandle = ref MarkStatsDirtyStream.Read<StatHandle>();
                TmpDirtyStatsQueue.Enqueue(dirtyStatHandle);
            }

            MarkStatsDirtyStream.EndForEachIndex();
        }
    }
}
