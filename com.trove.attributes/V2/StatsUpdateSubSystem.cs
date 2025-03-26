using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Trove.EventSystems;
using Trove.Stats;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Logging;
using Unity.Mathematics;
using static Codice.Client.BaseCommands.Import.Commit;


namespace Trove.Stats
{
    [BurstCompile]
    public struct StatsUpdateSubSystem<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        private GlobalEventSubSystem<GlobalStatEventsSingleton<TStatModifier, TStatModifierStack>, StatEvent<TStatModifier, TStatModifierStack>> _globalStatEventsSubSystem;

        private EntityQuery _batchRecomputeStatsQuery;
        private EntityQuery _dirtyStatsQuery;
        private EntityQuery _statsSettingsQuery;
        private EntityQuery _globalStatEventsSingletonQuery;

        private ComponentLookup<StatOwner> StatOwnerLookup;
        private ComponentLookup<DirtyStatsMask> DirtyStatsMaskLookup;
        private BufferLookup<Stat> StatsBufferLookup;
        private BufferLookup<TStatModifier> StatModifiersBufferLookup;
        private BufferLookup<TStatModifier> StatModifiersBufferLookupRO;
        private BufferLookup<StatObserver> StatObserversBufferLookup;
        private BufferLookup<StatObserver> StatObserversBufferLookupRO;
        private BufferLookup<AddModifierEventCallback> AddModifierEventCallbackBufferLookup;

        private EntityTypeHandle EntityTypeHandle;
        private ComponentTypeHandle<DirtyStatsMask> DirtyStatsMaskTypeHandle;
        private BufferTypeHandle<TStatModifier> StatModifiersBufferTypeHandleRO;
        private BufferTypeHandle<StatObserver> StatObserversBufferTypeHandleRO;

        private NativeQueue<StatHandle> _recomputeImmediateDirtyStatsQueue;
        private NativeQueue<StatHandle> _eventsDirtyStatsQueue;

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
            EntityQueryBuilder baseStatQueryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<
                StatOwner,
                TStatModifier,
                StatObserver>()
                .WithAllRW<Stat>();

            _batchRecomputeStatsQuery = baseStatQueryBuilder.Build(ref state);
            _dirtyStatsQuery = baseStatQueryBuilder.WithAllRW<DirtyStatsMask>().Build(ref state);
            _statsSettingsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<StatsSettings>().Build(ref state);
            _globalStatEventsSingletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<GlobalStatEventsSingleton<TStatModifier, TStatModifierStack>>().Build(ref state);

            state.RequireForUpdate(_batchRecomputeStatsQuery);

            StatOwnerLookup = state.GetComponentLookup<StatOwner>(false);
            DirtyStatsMaskLookup = state.GetComponentLookup<DirtyStatsMask>(false);
            StatsBufferLookup = state.GetBufferLookup<Stat>(false);
            StatModifiersBufferLookup = state.GetBufferLookup<TStatModifier>(false);
            StatModifiersBufferLookupRO = state.GetBufferLookup<TStatModifier>(true);
            StatObserversBufferLookup = state.GetBufferLookup<StatObserver>(false);
            StatObserversBufferLookupRO = state.GetBufferLookup<StatObserver>(true);
            AddModifierEventCallbackBufferLookup = state.GetBufferLookup<AddModifierEventCallback>(false);

            EntityTypeHandle = state.EntityManager.GetEntityTypeHandle();
            DirtyStatsMaskTypeHandle = state.EntityManager.GetComponentTypeHandle<DirtyStatsMask>(false);
            StatModifiersBufferTypeHandleRO = state.EntityManager.GetBufferTypeHandle<TStatModifier>(true);
            StatObserversBufferTypeHandleRO = state.EntityManager.GetBufferTypeHandle<StatObserver>(true);

            _recomputeImmediateDirtyStatsQueue = new NativeQueue<StatHandle>(Allocator.Persistent);
            _eventsDirtyStatsQueue = new NativeQueue<StatHandle>(Allocator.Persistent);

            _globalStatEventsSubSystem =
                new GlobalEventSubSystem<GlobalStatEventsSingleton<TStatModifier, TStatModifierStack>, StatEvent<TStatModifier, TStatModifierStack>>(
                    ref state, 32, 32, 1000);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _globalStatEventsSubSystem.OnDestroy(ref state);

            if (_recomputeImmediateDirtyStatsQueue.IsCreated)
            {
                _recomputeImmediateDirtyStatsQueue.Dispose();
            }
            if (_eventsDirtyStatsQueue.IsCreated)
            {
                _eventsDirtyStatsQueue.Dispose();
            }
        }

        [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            StatsSettings statsSettings = GetStatsSettings();

            StatOwnerLookup.Update(ref state);
            DirtyStatsMaskLookup.Update(ref state);
            StatsBufferLookup.Update(ref state);
            StatModifiersBufferLookup.Update(ref state);
            StatModifiersBufferLookupRO.Update(ref state);
            StatObserversBufferLookup.Update(ref state);
            StatObserversBufferLookupRO.Update(ref state);
            AddModifierEventCallbackBufferLookup.Update(ref state);

            EntityTypeHandle.Update(ref state);
            DirtyStatsMaskTypeHandle.Update(ref state);
            StatModifiersBufferTypeHandleRO.Update(ref state);
            StatObserversBufferTypeHandleRO.Update(ref state);

            // Process stat events
            {
                _globalStatEventsSubSystem.OnUpdate(ref state);

                if (_globalStatEventsSingletonQuery.HasSingleton<GlobalStatEventsSingleton<TStatModifier, TStatModifierStack>>())
                {
                    ref GlobalStatEventsSingleton<TStatModifier, TStatModifierStack> eventsSingleton = ref _globalStatEventsSingletonQuery.GetSingletonRW<GlobalStatEventsSingleton<TStatModifier, TStatModifierStack>>().ValueRW;
                    state.Dependency = new ProcessGlobalStatEventsJob
                    {
                        StatEvents = eventsSingleton.EventsList,
                        StatOwnerLookup = StatOwnerLookup,
                        DirtyStatsMaskLookup = DirtyStatsMaskLookup,
                        StatsBufferLookup = StatsBufferLookup,
                        StatModifiersBufferLookup = StatModifiersBufferLookup,
                        StatObserversBufferLookup = StatObserversBufferLookup,
                        AddModifierEventCallbackBufferLookup = AddModifierEventCallbackBufferLookup,
                        RecomputeImmediateStatsQueue = _eventsDirtyStatsQueue,
                    }.Schedule(state.Dependency);
                }
            }

            int statEntitiesCount = _batchRecomputeStatsQuery.CalculateEntityCount();
            int statEntitiesChunkCount = _batchRecomputeStatsQuery.CalculateChunkCount();

            if (statsSettings.BatchRecomputeUpdatesCount > 0)
            {
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
                            TmpDirtyStatsQueue = _recomputeImmediateDirtyStatsQueue.AsParallelWriter(),
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
                    TmpDirtyStatsQueue = _recomputeImmediateDirtyStatsQueue.AsParallelWriter(),

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

                    TmpDirtyStatsQueue = _recomputeImmediateDirtyStatsQueue,
                }.Schedule(state.Dependency);
            }
        }

        [BurstCompile]
        public struct ProcessGlobalStatEventsJob : IJob
        {
            public NativeList<StatEvent<TStatModifier, TStatModifierStack>> StatEvents;

            public ComponentLookup<StatOwner> StatOwnerLookup;
            public ComponentLookup<DirtyStatsMask> DirtyStatsMaskLookup;
            public BufferLookup<Stat> StatsBufferLookup;
            public BufferLookup<TStatModifier> StatModifiersBufferLookup;
            public BufferLookup<StatObserver> StatObserversBufferLookup;
            public BufferLookup<AddModifierEventCallback> AddModifierEventCallbackBufferLookup;
            public NativeQueue<StatHandle> RecomputeImmediateStatsQueue;

            public void Execute()
            {
                UnsafeList<StatHandle> tmpObservedStatsList = new UnsafeList<StatHandle>(16, Allocator.Temp);
                RecomputeImmediateStatsQueue.Clear();

                for (int i = 0; i < StatEvents.Length; i++)
                {
                    StatEvent<TStatModifier, TStatModifierStack> e = StatEvents[i];
                    switch (e.EventType)
                    {
                        case StatEvent<TStatModifier, TStatModifierStack>.StatEventType.Recompute:
                            {
                                if (StatsBufferLookup.TryGetBuffer(e.StatHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
                                {
                                    ref DirtyStatsMask dirtyStatsMask = ref DirtyStatsMaskLookup.GetRefRW(e.StatHandle.Entity).ValueRW;
                                    EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW = DirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(e.StatHandle.Entity);

                                    if (e.RecomputeImmediate)
                                    {
                                        DynamicBuffer<TStatModifier> statModifiersBuffer = StatModifiersBufferLookup[e.StatHandle.Entity];
                                        DynamicBuffer<StatObserver> statObserversBuffer = StatObserversBufferLookup[e.StatHandle.Entity];

                                        StatUtilities.RecomputeStatAndObserversImmediate<TStatModifier, TStatModifierStack>(
                                            e.StatHandle,
                                            ref statsBuffer,
                                            ref statModifiersBuffer,
                                            ref statObserversBuffer,
                                            ref StatsBufferLookup,
                                            ref StatModifiersBufferLookup,
                                            ref StatObserversBufferLookup,
                                            ref RecomputeImmediateStatsQueue);
                                    }
                                    else
                                    {
                                        StatUtilities.MarkStatForBatchRecompute(
                                            e.StatHandle.Index,
                                            ref dirtyStatsMask,
                                            dirtyStatsMaskEnabledRefRW);
                                    }
                                }

                                break;
                            }
                        case StatEvent<TStatModifier, TStatModifierStack>.StatEventType.AddBaseValue:
                            {
                                if (StatsBufferLookup.TryGetBuffer(e.StatHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
                                {
                                    ref Stat statRef = ref StatUtilities.TryResolveStatRef(
                                        e.StatHandle,
                                        e.StatHandle.Entity,
                                        ref statsBuffer,
                                        ref StatsBufferLookup,
                                        out bool success);
                                    if (success)
                                    {
                                        statRef.BaseValue += e.Value;

                                        ref DirtyStatsMask dirtyStatsMask = ref DirtyStatsMaskLookup.GetRefRW(e.StatHandle.Entity).ValueRW;
                                        EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW = DirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(e.StatHandle.Entity);

                                        if (e.RecomputeImmediate)
                                        {
                                            DynamicBuffer<TStatModifier> statModifiersBuffer = StatModifiersBufferLookup[e.StatHandle.Entity];
                                            DynamicBuffer<StatObserver> statObserversBuffer = StatObserversBufferLookup[e.StatHandle.Entity];

                                            StatUtilities.RecomputeStatAndObserversImmediate<TStatModifier, TStatModifierStack>(
                                                e.StatHandle,
                                                ref statsBuffer,
                                                ref statModifiersBuffer,
                                                ref statObserversBuffer,
                                                ref StatsBufferLookup,
                                                ref StatModifiersBufferLookup,
                                                ref StatObserversBufferLookup,
                                                ref RecomputeImmediateStatsQueue);
                                        }
                                        else
                                        {
                                            StatUtilities.MarkStatForBatchRecompute(
                                                e.StatHandle.Index,
                                                ref dirtyStatsMask,
                                                dirtyStatsMaskEnabledRefRW);
                                        }
                                    }
                                }

                                break;
                            }
                        case StatEvent<TStatModifier, TStatModifierStack>.StatEventType.SetBaseValue:
                            {
                                if (StatsBufferLookup.TryGetBuffer(e.StatHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
                                {
                                    ref Stat statRef = ref StatUtilities.TryResolveStatRef(
                                        e.StatHandle,
                                        e.StatHandle.Entity,
                                        ref statsBuffer,
                                        ref StatsBufferLookup,
                                        out bool success);
                                    if (success)
                                    {
                                        statRef.BaseValue = e.Value;

                                        ref DirtyStatsMask dirtyStatsMask = ref DirtyStatsMaskLookup.GetRefRW(e.StatHandle.Entity).ValueRW;
                                        EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW = DirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(e.StatHandle.Entity);

                                        if (e.RecomputeImmediate)
                                        {
                                            DynamicBuffer<TStatModifier> statModifiersBuffer = StatModifiersBufferLookup[e.StatHandle.Entity];
                                            DynamicBuffer<StatObserver> statObserversBuffer = StatObserversBufferLookup[e.StatHandle.Entity];

                                            StatUtilities.RecomputeStatAndObserversImmediate<TStatModifier, TStatModifierStack>(
                                                e.StatHandle,
                                                ref statsBuffer,
                                                ref statModifiersBuffer,
                                                ref statObserversBuffer,
                                                ref StatsBufferLookup,
                                                ref StatModifiersBufferLookup,
                                                ref StatObserversBufferLookup,
                                                ref RecomputeImmediateStatsQueue);
                                        }
                                        else
                                        {
                                            StatUtilities.MarkStatForBatchRecompute(
                                                e.StatHandle.Index,
                                                ref dirtyStatsMask,
                                                dirtyStatsMaskEnabledRefRW);
                                        }
                                    }
                                }

                                break;
                            }
                        case StatEvent<TStatModifier, TStatModifierStack>.StatEventType.AddModifier:
                            {
                                if (StatsBufferLookup.TryGetBuffer(e.StatHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
                                {
                                    ref StatOwner statOwner = ref StatOwnerLookup.GetRefRW(e.StatHandle.Entity).ValueRW;
                                    ref DirtyStatsMask dirtyStatsMask = ref DirtyStatsMaskLookup.GetRefRW(e.StatHandle.Entity).ValueRW;
                                    EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW = DirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(e.StatHandle.Entity);
                                    DynamicBuffer<TStatModifier> statModifiersBuffer = StatModifiersBufferLookup[e.StatHandle.Entity];
                                    DynamicBuffer<StatObserver> statObserversBuffer = StatObserversBufferLookup[e.StatHandle.Entity];

                                    ModifierHandle addedModifierHandle = StatUtilities.AddModifier<TStatModifier, TStatModifierStack>(
                                        e.StatHandle,
                                        e.Modifier,
                                        ref statOwner,
                                        ref dirtyStatsMask,
                                        dirtyStatsMaskEnabledRefRW,
                                        ref statModifiersBuffer,
                                        ref statObserversBuffer,
                                        ref DirtyStatsMaskLookup,
                                        ref StatObserversBufferLookup,
                                        ref tmpObservedStatsList);

                                    if (e.CallbackEntity != Entity.Null &&
                                        AddModifierEventCallbackBufferLookup.TryGetBuffer(e.CallbackEntity, out DynamicBuffer<AddModifierEventCallback> callbackBuffer))
                                    {
                                        callbackBuffer.Add(new AddModifierEventCallback
                                        {
                                            ModifierHandle = addedModifierHandle,
                                        });
                                    }

                                    if (e.RecomputeImmediate)
                                    {
                                        StatUtilities.RecomputeStatAndObserversImmediate<TStatModifier, TStatModifierStack>(
                                            e.StatHandle,
                                            ref statsBuffer,
                                            ref statModifiersBuffer,
                                            ref statObserversBuffer,
                                            ref StatsBufferLookup,
                                            ref StatModifiersBufferLookup,
                                            ref StatObserversBufferLookup,
                                            ref RecomputeImmediateStatsQueue);
                                    }
                                    else
                                    {
                                        StatUtilities.MarkStatForBatchRecompute(
                                            e.StatHandle.Index,
                                            ref dirtyStatsMask,
                                            dirtyStatsMaskEnabledRefRW);
                                    }
                                }

                                break;
                            }
                        case StatEvent<TStatModifier, TStatModifierStack>.StatEventType.RemoveModifier:
                            {
                                if (StatsBufferLookup.TryGetBuffer(e.StatHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
                                {
                                    ref DirtyStatsMask dirtyStatsMask = ref DirtyStatsMaskLookup.GetRefRW(e.StatHandle.Entity).ValueRW;
                                    EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW = DirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(e.StatHandle.Entity);
                                    DynamicBuffer<TStatModifier> statModifiersBuffer = StatModifiersBufferLookup[e.StatHandle.Entity];
                                    DynamicBuffer<StatObserver> statObserversBuffer = StatObserversBufferLookup[e.StatHandle.Entity];

                                    StatUtilities.RemoveModifier<TStatModifier, TStatModifierStack>(
                                        e.StatHandle,
                                        e.ModifierHandle,
                                        ref dirtyStatsMask,
                                        dirtyStatsMaskEnabledRefRW,
                                        ref statModifiersBuffer,
                                        ref statObserversBuffer,
                                        ref StatObserversBufferLookup,
                                        ref tmpObservedStatsList);

                                    if (e.RecomputeImmediate)
                                    {
                                        StatUtilities.RecomputeStatAndObserversImmediate<TStatModifier, TStatModifierStack>(
                                            e.StatHandle,
                                            ref statsBuffer,
                                            ref statModifiersBuffer,
                                            ref statObserversBuffer,
                                            ref StatsBufferLookup,
                                            ref StatModifiersBufferLookup,
                                            ref StatObserversBufferLookup,
                                            ref RecomputeImmediateStatsQueue);
                                    }
                                    else
                                    {
                                        StatUtilities.MarkStatForBatchRecompute(
                                            e.StatHandle.Index,
                                            ref dirtyStatsMask,
                                            dirtyStatsMaskEnabledRefRW);
                                    }
                                }

                                break;
                            }
                    }
                }
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
                                if (selfStatHandle == modifier.AffectedStatHandle)
                                {
                                    modifier.Apply(
                                        ref modifierStack,
                                        entity,
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
                StatHandle cachedStatHandle = default;
                DynamicBuffer<Stat> cachedStatsBuffer = default;
                DynamicBuffer<TStatModifier> cachedStatModifiersBuffer = default;
                DynamicBuffer<StatObserver> cachedStatObserversBuffer = default;

                while (TmpDirtyStatsQueue.TryDequeue(out StatHandle dirtyStatHandle))
                {
                    if (dirtyStatHandle.Entity != Entity.Null)
                    {
                        if (dirtyStatHandle.Entity == cachedStatHandle.Entity)
                        {
                            StatUtilities.RecomputeStatAndAddObserversToQueue<TStatModifier, TStatModifierStack>(
                                dirtyStatHandle,
                                ref cachedStatsBuffer,
                                ref cachedStatModifiersBuffer,
                                ref cachedStatObserversBuffer,
                                ref StatsBufferLookup,
                                ref TmpDirtyStatsQueue);
                        }
                        else if (StatsBufferLookup.TryGetBuffer(dirtyStatHandle.Entity, out cachedStatsBuffer))
                        {
                            cachedStatHandle = dirtyStatHandle;
                            cachedStatModifiersBuffer = StatModifiersBufferLookup[dirtyStatHandle.Entity];
                            cachedStatObserversBuffer = StatObserversBufferLookup[dirtyStatHandle.Entity];

                            StatUtilities.RecomputeStatAndAddObserversToQueue<TStatModifier, TStatModifierStack>(
                                dirtyStatHandle,
                                ref cachedStatsBuffer,
                                ref cachedStatModifiersBuffer,
                                ref cachedStatObserversBuffer,
                                ref StatsBufferLookup,
                                ref TmpDirtyStatsQueue);
                        }
                    }
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
