using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Logging;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Trove.Stats
{
    public interface IStatsModifierStack
    {
        public void Reset();
        public void Apply(ref Stat stat);
    }

    public interface IStatsModifier<TStack> where TStack : unmanaged, IStatsModifierStack 
    {
        public uint Id { get; set; }
        public StatHandle AffectedStat { get; set; }
        public void AddObservedStatsToList(ref UnsafeList<StatHandle> observedStats);
        public void Apply(
            ref TStack stack,
            StatHandle selfStatHandle,
            ref DynamicBuffer<Stat> selfStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup);
    }

    public struct StatOwner : IComponentData
    {
        public uint ModifierIdCounter;
    }

    [InternalBufferCapacity(3)]
    public partial struct Stat : IBufferElementData
    {
        public byte Exists;
        public float BaseValue;
        public float Value;
    }

    [InternalBufferCapacity(0)]
    public unsafe partial struct StatObserver : IBufferElementData
    {
        public StatHandle ObserverStat;
        public StatHandle ObservedStat;
        public int Count;

        public StatObserver(StatHandle observerStat, StatHandle observedStat, int count = 0)
        {
            ObservedStat = observedStat;
            ObserverStat = observerStat;
            Count = count;
        }
    }

    public struct HasDirtyStats : IComponentData, IEnableableComponent
    { }

    [InternalBufferCapacity(0)]
    public partial struct DirtyStat : IBufferElementData
    {
        public byte Value;
    }

    public struct StatHandle : IEquatable<StatHandle>
    {
        public Entity Entity;
        public int Index;

        public StatHandle(Entity entity, int index)
        {
            Entity = entity;
            Index = index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is StatHandle h)
            {
                return Equals(h);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(StatHandle other)
        {
            return this == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 55339;
            hash = hash * 104579 + Entity.GetHashCode();
            hash = hash * 104579 + Index.GetHashCode();
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(StatHandle x, StatHandle y)
        {
            return x.Index == y.Index && x.Entity == y.Entity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(StatHandle x, StatHandle y)
        {
            return x.Index != y.Index || x.Entity != y.Entity;
        }
    }

    public struct ModifierHandle : IEquatable<ModifierHandle>
    {
        public Entity Entity;
        public uint Id;

        public ModifierHandle(Entity entity, uint id)
        {
            Entity = entity;
            Id = id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is ModifierHandle h)
            {
                return Equals(h);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ModifierHandle other)
        {
            return this == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 55339;
            hash = hash * 104579 + Entity.GetHashCode();
            hash = hash * 104579 + Id.GetHashCode();
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ModifierHandle x, ModifierHandle y)
        {
            return x.Entity == y.Entity && x.Id == y.Id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ModifierHandle x, ModifierHandle y)
        {
            return x.Entity != y.Entity || x.Id != y.Id;
        }
    }

    [BurstCompile]
    public struct StatsUpdateSubSystem<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        private EntityQuery _batchRecomputeStatsQuery;
        private EntityQuery _dirtyStatsQuery;

        private ComponentLookup<HasDirtyStats> HasDirtyStatsLookup;
        private BufferLookup<Stat> StatsBufferLookup;
        private BufferLookup<TStatModifier> StatModifiersBufferLookupRO;
        private BufferLookup<StatObserver> StatObserversBufferLookupRO;
        private BufferLookup<DirtyStat> DirtyStatsBufferLookup;

        private EntityTypeHandle EntityTypeHandle;
        private ComponentTypeHandle<HasDirtyStats> HasDirtyStatsTypeHandle;
        private BufferTypeHandle<TStatModifier> StatModifiersBufferTypeHandleRO;
        private BufferTypeHandle<StatObserver> StatObserversBufferTypeHandleRO;
        private BufferTypeHandle<DirtyStat> DirtyStatsBufferTypeHandle;

        private NativeQueue<StatHandle> _tmpDirtyStatsQueue;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _batchRecomputeStatsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<
                StatOwner,
                TStatModifier,
                StatObserver>()
                .WithAllRW<Stat>()
                .WithAllRW<DirtyStat>()
                .Build(ref state);
            _dirtyStatsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<
                StatOwner,
                TStatModifier,
                StatObserver>()
                .WithAllRW<Stat>()
                .WithAllRW<DirtyStat>()
                .WithAllRW<HasDirtyStats>()
                .Build(ref state);

            state.RequireForUpdate(_batchRecomputeStatsQuery);

            HasDirtyStatsLookup = state.GetComponentLookup<HasDirtyStats>(false);
            StatsBufferLookup = state.GetBufferLookup<Stat>(false);
            StatModifiersBufferLookupRO = state.GetBufferLookup<TStatModifier>(true);
            StatObserversBufferLookupRO = state.GetBufferLookup<StatObserver>(true);
            DirtyStatsBufferLookup = state.GetBufferLookup<DirtyStat>(false);

            EntityTypeHandle = state.EntityManager.GetEntityTypeHandle();
            HasDirtyStatsTypeHandle = state.EntityManager.GetComponentTypeHandle<HasDirtyStats>(false);
            StatModifiersBufferTypeHandleRO = state.EntityManager.GetBufferTypeHandle<TStatModifier>(true);
            StatObserversBufferTypeHandleRO = state.EntityManager.GetBufferTypeHandle<StatObserver>(true);
            DirtyStatsBufferTypeHandle = state.EntityManager.GetBufferTypeHandle<DirtyStat>(false);

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
        public void OnUpdate(ref SystemState state)
        {
            bool finishWithRecomputeImmediate = true;  // TODO: make configurable
            int batchRecomputeUpdatesCount = 3; // TODO: make configurable

            int statEntitiesChunkCount = _batchRecomputeStatsQuery.CalculateChunkCount();

            // TODO: process stat commands

            StatsBufferLookup.Update(ref state);
            StatModifiersBufferLookupRO.Update(ref state);
            StatObserversBufferLookupRO.Update(ref state);

            EntityTypeHandle.Update(ref state);
            HasDirtyStatsTypeHandle.Update(ref state);
            DirtyStatsBufferTypeHandle.Update(ref state);

            if (batchRecomputeUpdatesCount > 0)
            {
                HasDirtyStatsLookup.Update(ref state);
                DirtyStatsBufferLookup.Update(ref state);

                StatModifiersBufferTypeHandleRO.Update(ref state);
                StatObserversBufferTypeHandleRO.Update(ref state);

                for (int i = 0; i < batchRecomputeUpdatesCount; i++)
                {
                    // TODO: have a stats update group in which we can add systems that react to stat changes?

                    NativeStream markStatsDirtyStream = new NativeStream(statEntitiesChunkCount, state.WorldUpdateAllocator);

                    state.Dependency = new BatchRecomputeDirtyStatsJob
                    {
                        StatsBufferLookup = StatsBufferLookup,

                        EntityTypeHandle = EntityTypeHandle,
                        HasDirtyStatsTypeHandle = HasDirtyStatsTypeHandle,
                        StatModifiersBufferTypeHandle = StatModifiersBufferTypeHandleRO,
                        StatObserversBufferTypeHandle = StatObserversBufferTypeHandleRO,
                        DirtyStatsBufferTypeHandle = DirtyStatsBufferTypeHandle,

                        MarkStatsDirtyStream = markStatsDirtyStream.AsWriter(),
                    }.ScheduleParallel(_dirtyStatsQuery, state.Dependency);

                    bool isLastBatch = i >= batchRecomputeUpdatesCount - 1;
                    if (isLastBatch && finishWithRecomputeImmediate)
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
                            HasDirtyStatsLookup = HasDirtyStatsLookup,
                            DirtyStatsBufferLookup = DirtyStatsBufferLookup,
                            MarkStatsDirtyStream = markStatsDirtyStream.AsReader(),
                        }.Schedule(statEntitiesChunkCount, 1, state.Dependency);
                    }

                    markStatsDirtyStream.Dispose(state.Dependency);
                }
            }
            else if (finishWithRecomputeImmediate)
            {
                // Schedule a job to transfer dirty stats to recompute queue
                state.Dependency = new EnqueueDirtyStatsForRecomputeImmediateJob
                {
                    TmpDirtyStatsQueue = _tmpDirtyStatsQueue.AsParallelWriter(),

                    EntityTypeHandle = EntityTypeHandle,
                    HasDirtyStatsTypeHandle = HasDirtyStatsTypeHandle,
                    DirtyStatsBufferTypeHandle = DirtyStatsBufferTypeHandle,
                }.ScheduleParallel(_dirtyStatsQuery, state.Dependency);
            }

            // Optional final job that recomputes remaining dirty stats and observers immediately
            if(finishWithRecomputeImmediate)
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
            public ComponentTypeHandle<HasDirtyStats> HasDirtyStatsTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<TStatModifier> StatModifiersBufferTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<StatObserver> StatObserversBufferTypeHandle;
            public BufferTypeHandle<DirtyStat> DirtyStatsBufferTypeHandle;

            [NativeDisableParallelForRestriction]
            public NativeStream.Writer MarkStatsDirtyStream;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (chunkEnabledMask.ULong0 > 0 || chunkEnabledMask.ULong1 > 0)
                {
                    NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                    EnabledMask doesEntityHaveDirtyStats = chunk.GetEnabledMask(ref HasDirtyStatsTypeHandle);
                    BufferAccessor<TStatModifier> statModifiersBufferAccessor = chunk.GetBufferAccessor(ref StatModifiersBufferTypeHandle);
                    BufferAccessor<StatObserver> statObserversBufferAccessor = chunk.GetBufferAccessor(ref StatObserversBufferTypeHandle);
                    BufferAccessor<DirtyStat> dirtyStatsBufferAccessor = chunk.GetBufferAccessor(ref DirtyStatsBufferTypeHandle);

                    MarkStatsDirtyStream.BeginForEachIndex(unfilteredChunkIndex);

                    ChunkEntityEnumerator entityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                    while (entityEnumerator.NextEntityIndex(out int i))
                    {
                        // TODO: is this needed, considering we operate on the dirtyStatsQuery?
                        if (!doesEntityHaveDirtyStats[i])
                            continue;

                        Entity entity = entities[i];
                        DynamicBuffer<Stat> statsBuffer = StatsBufferLookup[entity];
                        DynamicBuffer<TStatModifier> statModifiersBuffer = statModifiersBufferAccessor[i];
                        DynamicBuffer<StatObserver> statObserversBuffer = statObserversBufferAccessor[i];
                        DynamicBuffer<DirtyStat> dirtyStatsBuffer = dirtyStatsBufferAccessor[i];

                        void* dirtyStatsBufferPtr = dirtyStatsBuffer.GetUnsafePtr();
                        void* statsBufferPtr = statsBuffer.GetUnsafePtr();

                        for (int statIndex = 0; statIndex < dirtyStatsBuffer.Length; statIndex++)
                        {
                            ref byte dirtyStatByteRef = ref UnsafeUtility.ArrayElementAsRef<byte>(dirtyStatsBufferPtr, statIndex);
                            if (dirtyStatByteRef == 1)
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
                                dirtyStatByteRef = 0;

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
    }

    [BurstCompile]
    public struct ApplyHasDirtyStatsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public ComponentLookup<HasDirtyStats> HasDirtyStatsLookup;
        [NativeDisableParallelForRestriction]
        public BufferLookup<DirtyStat> DirtyStatsBufferLookup;
        [NativeDisableParallelForRestriction]
        public NativeStream.Reader MarkStatsDirtyStream;

        public void Execute(int index)
        {
            MarkStatsDirtyStream.BeginForEachIndex(index);

            while (MarkStatsDirtyStream.RemainingItemCount > 0)
            {
                ref StatHandle dirtyStatHandle = ref MarkStatsDirtyStream.Read<StatHandle>();

                DynamicBuffer<DirtyStat> dirtyStatsBuffer = DirtyStatsBufferLookup[dirtyStatHandle.Entity];
                dirtyStatsBuffer[dirtyStatHandle.Index] = new DirtyStat { Value = 1 };
                HasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(dirtyStatHandle.Entity).ValueRW = true;
            }

            MarkStatsDirtyStream.EndForEachIndex();
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

    [BurstCompile]
    public struct EnqueueDirtyStatsForRecomputeImmediateJob : IJobChunk
    {
        public NativeQueue<StatHandle>.ParallelWriter TmpDirtyStatsQueue;

        [ReadOnly]
        public EntityTypeHandle EntityTypeHandle;
        public ComponentTypeHandle<HasDirtyStats> HasDirtyStatsTypeHandle;
        public BufferTypeHandle<DirtyStat> DirtyStatsBufferTypeHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (chunkEnabledMask.ULong0 > 0 || chunkEnabledMask.ULong1 > 0)
            {
                NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                EnabledMask doesEntityHaveDirtyStats = chunk.GetEnabledMask(ref HasDirtyStatsTypeHandle);
                BufferAccessor<DirtyStat> dirtyStatsBufferAccessor = chunk.GetBufferAccessor(ref DirtyStatsBufferTypeHandle);

                ChunkEntityEnumerator entityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entityEnumerator.NextEntityIndex(out int i))
                {
                    // TODO: is this needed, considering we operate on the dirtyStatsQuery?
                    if (!doesEntityHaveDirtyStats[i])
                        continue;

                    Entity entity = entities[i];
                    DynamicBuffer<DirtyStat> dirtyStatsBuffer = dirtyStatsBufferAccessor[i];

                    for (int statIndex = 0; statIndex < dirtyStatsBuffer.Length; statIndex++)
                    {
                        if (dirtyStatsBuffer[statIndex].Value == 1)
                        {
                            StatHandle selfStatHandle = new StatHandle(entity, statIndex);
                            TmpDirtyStatsQueue.Enqueue(selfStatHandle);

                            dirtyStatsBuffer[statIndex] = default;
                        }
                    }

                    doesEntityHaveDirtyStats[i] = false;
                }
            }
        }
    }

    // TODO
    [System.Serializable]
    public struct StatDefinition
    {
        public bool HasStat;
        public float BaseValue;
    }

    public static class StatUtilities
    {
        public static void BakeStatsOwner<TStatModifier, TStatModifierStack>(
            IBaker baker,
            MonoBehaviour authoring,
            StatDefinition[] statDefinitions,
            bool supportOnlyImmediateRecompute = false)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Entity entity = baker.GetEntity(authoring, TransformUsageFlags.None);
            baker.AddComponent(entity, new StatOwner
            {
                ModifierIdCounter = 1,
            });
            DynamicBuffer<Stat> statsBuffer = baker.AddBuffer<Stat>(entity);
            DynamicBuffer<TStatModifier> statModifiersBuffer = baker.AddBuffer<TStatModifier>(entity);
            DynamicBuffer<StatObserver> statObserversBuffer = baker.AddBuffer<StatObserver>(entity);

            statsBuffer.Resize(statDefinitions.Length, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < statDefinitions.Length; i++)
            {
                statsBuffer[i] = new Stat
                {
                    Exists = statDefinitions[i].HasStat ? (byte)1 : (byte)0,
                    BaseValue = statDefinitions[i].BaseValue,
                    Value = statDefinitions[i].BaseValue,
                };
            }

            // TODO: test that this works with the queries in the subsystem
            if (!supportOnlyImmediateRecompute)
            {
                DynamicBuffer<DirtyStat> dirtyStatsBuffer = baker.AddBuffer<DirtyStat>(entity);
                baker.AddComponent(entity, new HasDirtyStats());

                dirtyStatsBuffer.Resize(statDefinitions.Length, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < statDefinitions.Length; i++)
                {
                    dirtyStatsBuffer[i] = new DirtyStat
                    {
                        Value = statDefinitions[i].HasStat ? (byte)1 : (byte)0,
                    };
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveStat(
            StatHandle selfStatHandle,
            StatHandle resolvedStatHandle,
            ref DynamicBuffer<Stat> selfStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            out Stat result)
        {
            if (selfStatHandle.Entity == resolvedStatHandle.Entity)
            {
                if (resolvedStatHandle.Index >= 0 && resolvedStatHandle.Index < selfStatsBuffer.Length)
                {
                    result = selfStatsBuffer[resolvedStatHandle.Index];
                    return true;
                }
            }
            else if (statsBufferLookup.TryGetBuffer(resolvedStatHandle.Entity, out DynamicBuffer<Stat>  resolvedStatsBuffer))
            {
                if (resolvedStatHandle.Index >= 0 && resolvedStatHandle.Index < resolvedStatsBuffer.Length)
                {
                    result = resolvedStatsBuffer[resolvedStatHandle.Index];
                    return true;
                }
            }
            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveStat(
            StatHandle statHandle,
            ref BufferLookup<Stat> statsBufferLookup,
            out Stat result)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> resolvedStatsBuffer))
            {
                if (statHandle.Index >= 0 && statHandle.Index < resolvedStatsBuffer.Length)
                {
                    result = resolvedStatsBuffer[statHandle.Index];
                    return true;
                }
            }
            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ref Stat TryResolveStatRef(
            StatHandle selfStatHandle,
            StatHandle resolvedStatHandle,
            ref DynamicBuffer<Stat> selfStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            out bool success)
        {
            if (selfStatHandle.Entity == resolvedStatHandle.Entity)
            {
                if (resolvedStatHandle.Index >= 0 && resolvedStatHandle.Index < selfStatsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(selfStatsBuffer.GetUnsafePtr(), resolvedStatHandle.Index);
                }
            }
            else if (statsBufferLookup.TryGetBuffer(resolvedStatHandle.Entity, out DynamicBuffer<Stat> resolvedStatsBuffer))
            {
                if (resolvedStatHandle.Index >= 0 && resolvedStatHandle.Index < resolvedStatsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(resolvedStatsBuffer.GetUnsafePtr(), resolvedStatHandle.Index);
                }
            }
            success = false;
            return ref UnsafeUtility.ArrayElementAsRef<Stat>(selfStatsBuffer.GetUnsafePtr(), 0); ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ref Stat TryResolveStatRef(
            StatHandle statHandle,
            ref BufferLookup<Stat> statsBufferLookup,
            out bool success)
        {
            DynamicBuffer<Stat> resolvedStatsBuffer = default;
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out resolvedStatsBuffer))
            {
                if (statHandle.Index >= 0 && statHandle.Index < resolvedStatsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(resolvedStatsBuffer.GetUnsafePtr(), statHandle.Index);
                }
            }
            success = false;
            return ref UnsafeUtility.ArrayElementAsRef<Stat>(resolvedStatsBuffer.GetUnsafePtr(), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkStatForBatchRecompute(
            int statIndex,
            ref DynamicBuffer<DirtyStat> dirtyStatsBuffer,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW)
        {
            if (statIndex >= 0 && statIndex < dirtyStatsBuffer.Length)
            {
                dirtyStatsBuffer[statIndex] = new DirtyStat { Value = 1 };
                hasDirtyStatsEnabledRefRW.ValueRW = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkStatForBatchRecompute(
            StatHandle statHandle,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup)
        {
            if (dirtyStatsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<DirtyStat> dirtyStatsBuffer))
            {
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(statHandle.Entity);
                MarkStatForBatchRecompute(statHandle.Index, ref dirtyStatsBuffer, hasDirtyStatsEnabledRefRW);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecomputeStatsAndObserversImmediate<TStatModifier, TStatModifierStack>(
            ref NativeQueue<StatHandle> statsQueue,
            ref DynamicBuffer<Stat> statsBuffer,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            // TODO: infininte loop detection

            StatHandle prevStatHandle = default;
            while (statsQueue.TryDequeue(out StatHandle dirtyStatHandle))
            {
                if (dirtyStatHandle.Entity == prevStatHandle.Entity)
                {
                    RecomputeStatAndObserversImmediateInternal<TStatModifier, TStatModifierStack>(
                        dirtyStatHandle,
                        ref statsBuffer,
                        ref statModifiersBuffer,
                        ref statObserversBuffer,
                        ref statsBufferLookup,
                        ref statsQueue);
                }
                else if (statsBufferLookup.TryGetBuffer(dirtyStatHandle.Entity, out DynamicBuffer<Stat> otherStatsBuffer))
                {
                    DynamicBuffer<TStatModifier> otherStatModifiersBuffer = statModifiersBufferLookup[dirtyStatHandle.Entity];
                    DynamicBuffer<StatObserver> otherStatObserversBuffer = statObserversBufferLookup[dirtyStatHandle.Entity];

                    RecomputeStatAndObserversImmediateInternal<TStatModifier, TStatModifierStack>(
                        dirtyStatHandle,
                        ref otherStatsBuffer,
                        ref otherStatModifiersBuffer,
                        ref otherStatObserversBuffer,
                        ref statsBufferLookup,
                        ref statsQueue);
                }

                prevStatHandle = dirtyStatHandle;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RecomputeStatAndObserversImmediateInternal<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref DynamicBuffer<Stat> statsBuffer,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            ref NativeQueue<StatHandle> dirtyStatsQueue)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Stat stat = statsBuffer[statHandle.Index];

            // Apply Modifiers
            TStatModifierStack modifierStack = new TStatModifierStack();
            modifierStack.Reset();
            for (int m = 0; m < statModifiersBuffer.Length; m++)
            {
                TStatModifier modifier = statModifiersBuffer[m];
                if (statHandle == modifier.AffectedStat)
                {
                    modifier.Apply(
                    ref modifierStack,
                        statHandle,
                        ref statsBuffer,
                        ref statsBufferLookup);
                }
            }
            modifierStack.Apply(ref stat);
            statsBuffer[statHandle.Index] = stat;

            // Notify Observers
            for (int o = statObserversBuffer.Length - 1; o >= 0; o--)
            {
                StatObserver observer = statObserversBuffer[o];
                if (observer.ObservedStat == statHandle)
                {
                    dirtyStatsQueue.Enqueue(observer.ObserverStat);

                    // TODO: if observer no longer exists, remove it
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierHandle AddModifier<TStatModifier, TStatModifierStack>(
            StatHandle affectedStatHandle,
            TStatModifier modifier,
            ref StatOwner statOwner,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref DynamicBuffer<DirtyStat> dirtyStatsBuffer,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ModifierHandle modifierHandle = new ModifierHandle(Entity.Null, 0);
            if (affectedStatHandle.Index >= 0 && affectedStatHandle.Index < dirtyStatsBuffer.Length)
            {
                uint modifierId = statOwner.ModifierIdCounter++;
                modifierHandle = new ModifierHandle(affectedStatHandle.Entity, modifierId);
                modifier.Id = modifierId;
                modifier.AffectedStat = affectedStatHandle;

                statModifiersBuffer.Add(modifier);

                tmpObservedStatsList.Clear();
                modifier.AddObservedStatsToList(ref tmpObservedStatsList);
                for (int i = 0; i < tmpObservedStatsList.Length; i++)
                {
                    AddAsObserverOf(
                        affectedStatHandle,
                        tmpObservedStatsList[i],
                        ref statObserversBuffer,
                        ref dirtyStatsBuffer,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsBufferLookup,
                        ref hasDirtyStatsLookup);
                }

                MarkStatForBatchRecompute(affectedStatHandle.Index, ref dirtyStatsBuffer, hasDirtyStatsEnabledRefRW);
            }
            return modifierHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierHandle AddModifier<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            TStatModifier modifier,
            ref ComponentLookup<StatOwner> statOwnerLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ModifierHandle modifierHandle = new ModifierHandle(Entity.Null, 0);
            if (statOwnerLookup.TryGetComponent(statHandle.Entity, out StatOwner statOwner) &&
                statModifiersBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                statObserversBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer) &&
                dirtyStatsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<DirtyStat> dirtyStatsBuffer))
            {
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(statHandle.Entity);
                modifierHandle = AddModifier<TStatModifier, TStatModifierStack>(
                    statHandle,
                    modifier,
                    ref statOwner,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref dirtyStatsBuffer,
                    hasDirtyStatsEnabledRefRW,
                    ref statObserversBufferLookup,
                    ref dirtyStatsBufferLookup,
                    ref hasDirtyStatsLookup,
                    ref tmpObservedStatsList);

                statOwnerLookup[statHandle.Entity] = statOwner;
            }
            return modifierHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveModifier<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ModifierHandle modifierHandle,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref DynamicBuffer<DirtyStat> dirtyStatsBuffer,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statHandle.Index >= 0 && statHandle.Index < dirtyStatsBuffer.Length)
            {
                for (int i = statModifiersBuffer.Length - 1; i >= 0; i--)
                {
                    TStatModifier statModifier = statModifiersBuffer[i];
                    if (modifierHandle.Id == statModifier.Id)
                    {
                        tmpObservedStatsList.Clear();
                        statModifier.AddObservedStatsToList(ref tmpObservedStatsList);
                        for (int o = 0; o < tmpObservedStatsList.Length; o++)
                        {
                            RemoveAsObserverOf(
                                statHandle,
                                tmpObservedStatsList[o],
                                ref statObserversBuffer,
                                ref statObserversBufferLookup);
                        }

                        statModifiersBuffer.RemoveAt(i);
                        MarkStatForBatchRecompute(statHandle.Index, ref dirtyStatsBuffer, hasDirtyStatsEnabledRefRW);
                        return;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveModifier<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ModifierHandle modifierHandle,
            ref ComponentLookup<StatOwner> statOwnerLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statOwnerLookup.TryGetComponent(statHandle.Entity, out StatOwner statOwner) &&
                statModifiersBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                statObserversBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer) &&
                dirtyStatsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<DirtyStat> dirtyStatsBuffer))
            {
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(statHandle.Entity);
                RemoveModifier<TStatModifier, TStatModifierStack>(
                    statHandle,
                    modifierHandle,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref dirtyStatsBuffer,
                    hasDirtyStatsEnabledRefRW,
                    ref statObserversBufferLookup,
                    ref tmpObservedStatsList);

                statOwnerLookup[statHandle.Entity] = statOwner;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddAsObserverOf(
            StatHandle observerStatHandle,
            StatHandle observedStatHandle,
            ref DynamicBuffer<StatObserver> observerStatObserversBuffer,
            ref DynamicBuffer<DirtyStat> observerDirtyStatsBuffer,
            EnabledRefRW<HasDirtyStats> observerHasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup)
        {
            // TODO: observer loop detection?

            if (observerStatHandle.Entity == observedStatHandle.Entity)
            {
                AddObserverToBuffer(observerStatHandle, observedStatHandle, ref observerStatObserversBuffer);
                MarkStatForBatchRecompute(observedStatHandle.Index, ref observerDirtyStatsBuffer, observerHasDirtyStatsEnabledRefRW);
            }
            else if (statObserversBufferLookup.TryGetBuffer(observedStatHandle.Entity, out DynamicBuffer<StatObserver> observedStatObserversBuffer))
            {
                AddObserverToBuffer(observerStatHandle, observedStatHandle, ref observedStatObserversBuffer);
                MarkStatForBatchRecompute(observedStatHandle, ref dirtyStatsBufferLookup, ref hasDirtyStatsLookup);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddObserverToBuffer(
            StatHandle observerStatHandle,
            StatHandle observedStatHandle,
            ref DynamicBuffer<StatObserver> observedStatObserversBuffer)
        {
            for (int i = 0; i < observedStatObserversBuffer.Length; i++)
            {
                StatObserver statObserver = observedStatObserversBuffer[i];
                if (statObserver.ObserverStat == observerStatHandle)
                {
                    statObserver.Count++;
                    observedStatObserversBuffer[i] = statObserver;
                    return;
                }
            }

            observedStatObserversBuffer.Add(new StatObserver(observerStatHandle, observedStatHandle, 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveAsObserverOf(
            StatHandle observerStatHandle,
            StatHandle observedStatHandle,
            ref DynamicBuffer<StatObserver> observerStatObserversBuffer,
            ref BufferLookup<StatObserver> statObserversBufferLookup)
        {
            if (observerStatHandle.Entity == observedStatHandle.Entity)
            {
                RemoveObserverFromBuffer(observerStatHandle, ref observerStatObserversBuffer);
            }
            else if (statObserversBufferLookup.TryGetBuffer(observedStatHandle.Entity, out DynamicBuffer<StatObserver> observedStatObserversBuffer))
            {
                RemoveObserverFromBuffer(observerStatHandle, ref observedStatObserversBuffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveObserverFromBuffer(
            StatHandle observerStatHandle,
            ref DynamicBuffer<StatObserver> observedStatObserversBuffer)
        {
            for (int i = observedStatObserversBuffer.Length - 1; i >= 0; i--)
            {
                StatObserver statObserver = observedStatObserversBuffer[i];
                if (statObserver.ObserverStat == observerStatHandle)
                {
                    statObserver.Count--;
                    if (statObserver.Count <= 0)
                    {
                        observedStatObserversBuffer.RemoveAt(i);
                    }
                    return;
                }
            }
        }
    }
}
