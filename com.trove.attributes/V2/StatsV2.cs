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
using Unity.Plastic.Newtonsoft.Json.Linq;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

namespace Trove.Stats
{
    //public interface IDirtyStatsBitMask
    //{
    //    public bool GetSubMask(uint index, out ulong submask);
    //    public void SetSubMask(uint index, ulong submask);
    //    public int GetSubMaskCount();
    //}

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

    public struct DirtyStatsMask : IComponentData
    {
        public struct Iterator
        {
            internal long BitMask_0;
            internal long BitMask_1;
            internal int BitCount;

            internal int BitIterator;
            internal int SubMaskBitIterator;

            internal Iterator(DirtyStatsMask d)
            {
                BitMask_0 = d.BitMask_0;
                BitMask_1 = d.BitMask_1;
                BitCount = d.StatsCount;

                BitIterator = 0;
                SubMaskBitIterator = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool GetNextDirtyStat(out int nextStatIndex)
            {
                // First mask
                while (BitIterator < BitCount)
                {
                    // If submask has its first bit enabled, return this index and shift mask
                    if ((BitMask_0 & 1) != 0L)
                    {
                        nextStatIndex = BitIterator;
                        BitIterator++;
                        SubMaskBitIterator++;
                        BitMask_0 >>= 1;
                        return true;
                    }

                    BitIterator++;
                    SubMaskBitIterator++;
                    BitMask_0 >>= 1;
                }

                // Moving on to second mask
                SubMaskBitIterator = 0;
                while (BitIterator < BitCount)
                {
                    // If submask has its first bit enabled, return this index and shift mask
                    if ((BitMask_1 & 1) != 0L)
                    {
                        nextStatIndex = BitIterator;
                        BitIterator++;
                        SubMaskBitIterator++;
                        BitMask_1 >>= 1;
                        return true;
                    }

                    BitIterator++;
                    SubMaskBitIterator++;
                    BitMask_1 >>= 1;
                }

                // Additional masks would go here

                nextStatIndex = -1;
                return false;
            }

            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public void ShiftMaskAndIncrementIterators()
            //{
            //    BitIterator++;
            //    SubMaskBitIterator++;
            //    SubBitMask >>= 1;

            //    // Handle moving on to next submask
            //    if (SubMaskBitIterator >= 8)
            //    {
            //        SubMaskIndex++;
            //        SubMaskBitIterator = 0;
            //        BitMask.GetSubMask(SubMaskIndex, out SubBitMask);
            //    }
            //}
        }

        public long BitMask_0;
        public long BitMask_1;
        public int StatsCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Iterator GetIterator()
        {
            return new Iterator(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBit(int index)
        {
            int subMaskIndex = index / 64;
            int indexInSubMask = index % 64;
            long newMask;
            switch (subMaskIndex)
            {
                case 0:
                    newMask = BitMask_0 | (uint)(1 << indexInSubMask);
                    Interlocked.Exchange(ref BitMask_0, newMask);
                    break;
                case 1:
                    newMask = BitMask_1 | (uint)(1 << indexInSubMask);
                    Interlocked.Exchange(ref BitMask_1, newMask);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearBit(int index)
        {
            int subMaskIndex = index / 64;
            int indexInSubMask = index % 64;
            long newMask;
            switch (subMaskIndex)
            {
                case 0:
                    newMask = (BitMask_0 & (uint)(~indexInSubMask));
                    Interlocked.Exchange(ref BitMask_0, newMask);
                    break;
                case 1:
                    newMask = (BitMask_1 & (uint)(~indexInSubMask));
                    Interlocked.Exchange(ref BitMask_1, newMask);
                    break;
            }
        }
    }

    public struct HasDirtyStats : IComponentData, IEnableableComponent
    { }

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

        private ComponentLookup<DirtyStatsMask> DirtyStatsMaskLookup;
        private ComponentLookup<HasDirtyStats> HasDirtyStatsLookup;
        private BufferLookup<Stat> StatsBufferLookup;
        private BufferLookup<TStatModifier> StatModifiersBufferLookupRO;
        private BufferLookup<StatObserver> StatObserversBufferLookupRO;

        private EntityTypeHandle EntityTypeHandle;
        private ComponentTypeHandle<DirtyStatsMask> DirtyStatsMaskTypeHandle;
        private ComponentTypeHandle<HasDirtyStats> HasDirtyStatsTypeHandle;
        private BufferTypeHandle<TStatModifier> StatModifiersBufferTypeHandleRO;
        private BufferTypeHandle<StatObserver> StatObserversBufferTypeHandleRO;

        private NativeQueue<StatHandle> _tmpDirtyStatsQueue;

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
                .WithAllRW<HasDirtyStats>()
                .Build(ref state);

            state.RequireForUpdate(_batchRecomputeStatsQuery);

            DirtyStatsMaskLookup = state.GetComponentLookup<DirtyStatsMask>(false);
            HasDirtyStatsLookup = state.GetComponentLookup<HasDirtyStats>(false);
            StatsBufferLookup = state.GetBufferLookup<Stat>(false);
            StatModifiersBufferLookupRO = state.GetBufferLookup<TStatModifier>(true);
            StatObserversBufferLookupRO = state.GetBufferLookup<StatObserver>(true);

            EntityTypeHandle = state.EntityManager.GetEntityTypeHandle();
            DirtyStatsMaskTypeHandle = state.EntityManager.GetComponentTypeHandle<DirtyStatsMask>(false);
            HasDirtyStatsTypeHandle = state.EntityManager.GetComponentTypeHandle<HasDirtyStats>(false);
            StatModifiersBufferTypeHandleRO = state.EntityManager.GetBufferTypeHandle<TStatModifier>(true);
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
            DirtyStatsMaskTypeHandle.Update(ref state);
            HasDirtyStatsTypeHandle.Update(ref state);

            if (batchRecomputeUpdatesCount > 0)
            {
                DirtyStatsMaskLookup.Update(ref state);
                HasDirtyStatsLookup.Update(ref state);

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
                        DirtyStatsMaskTypeHandle = DirtyStatsMaskTypeHandle,
                        HasDirtyStatsTypeHandle = HasDirtyStatsTypeHandle,
                        StatModifiersBufferTypeHandle = StatModifiersBufferTypeHandleRO,
                        StatObserversBufferTypeHandle = StatObserversBufferTypeHandleRO,

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
                            DirtyStatsMaskLookup = DirtyStatsMaskLookup,
                            HasDirtyStatsLookup = HasDirtyStatsLookup,
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
                    DirtyStatsMaskTypeHandle = DirtyStatsMaskTypeHandle,
                    HasDirtyStatsTypeHandle = HasDirtyStatsTypeHandle,
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
            public ComponentTypeHandle<DirtyStatsMask> DirtyStatsMaskTypeHandle;
            public ComponentTypeHandle<HasDirtyStats> HasDirtyStatsTypeHandle;
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
                    EnabledMask doesEntityHaveDirtyStats = chunk.GetEnabledMask(ref HasDirtyStatsTypeHandle);
                    BufferAccessor<TStatModifier> statModifiersBufferAccessor = chunk.GetBufferAccessor(ref StatModifiersBufferTypeHandle);
                    BufferAccessor<StatObserver> statObserversBufferAccessor = chunk.GetBufferAccessor(ref StatObserversBufferTypeHandle);

                    MarkStatsDirtyStream.BeginForEachIndex(unfilteredChunkIndex);

                    ChunkEntityEnumerator entityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                    while (entityEnumerator.NextEntityIndex(out int i))
                    {
                        Entity entity = entities[i];
                        DirtyStatsMask dirtyStatsMask = dirtyStatsMasks[i];
                        DynamicBuffer<Stat> statsBuffer = StatsBufferLookup[entity];
                        DynamicBuffer<TStatModifier> statModifiersBuffer = statModifiersBufferAccessor[i];
                        DynamicBuffer<StatObserver> statObserversBuffer = statObserversBufferAccessor[i];

                        void* statsBufferPtr = statsBuffer.GetUnsafePtr();

                        DirtyStatsMask.Iterator dirtyStatsMaskIterator = dirtyStatsMask.GetIterator();
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
                            dirtyStatsMask.ClearBit(statIndex);

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
                        dirtyStatsMasks[i] = dirtyStatsMask;
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
        public struct EnqueueDirtyStatsForRecomputeImmediateJob : IJobChunk
        {
            public NativeQueue<StatHandle>.ParallelWriter TmpDirtyStatsQueue;

            [ReadOnly]
            public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<DirtyStatsMask> DirtyStatsMaskTypeHandle;
            public ComponentTypeHandle<HasDirtyStats> HasDirtyStatsTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (chunkEnabledMask.ULong0 > 0 || chunkEnabledMask.ULong1 > 0)
                {
                    NativeArray<Entity> entities = chunk.GetNativeArray(EntityTypeHandle);
                    NativeArray<DirtyStatsMask> dirtyStatsMasks = chunk.GetNativeArray(ref DirtyStatsMaskTypeHandle);
                    EnabledMask doesEntityHaveDirtyStats = chunk.GetEnabledMask(ref HasDirtyStatsTypeHandle);

                    ChunkEntityEnumerator entityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                    while (entityEnumerator.NextEntityIndex(out int i))
                    {
                        Entity entity = entities[i];
                        DirtyStatsMask dirtyStatsMask = dirtyStatsMasks[i];

                        DirtyStatsMask.Iterator dirtyStatsMaskIterator = dirtyStatsMask.GetIterator();
                        while (dirtyStatsMaskIterator.GetNextDirtyStat(out int statIndex))
                        {
                            StatHandle selfStatHandle = new StatHandle(entity, statIndex);
                            TmpDirtyStatsQueue.Enqueue(selfStatHandle);

                            dirtyStatsMask.ClearBit(statIndex);
                        }

                        doesEntityHaveDirtyStats[i] = false;
                        dirtyStatsMasks[i] = dirtyStatsMask;
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
            public ComponentLookup<HasDirtyStats> HasDirtyStatsLookup;
            [NativeDisableParallelForRestriction]
            public NativeStream.Reader MarkStatsDirtyStream;

            public void Execute(int index)
            {
                MarkStatsDirtyStream.BeginForEachIndex(index);

                while (MarkStatsDirtyStream.RemainingItemCount > 0)
                {
                    ref StatHandle dirtyStatHandle = ref MarkStatsDirtyStream.Read<StatHandle>();

                    DirtyStatsMaskLookup.GetRefRW(dirtyStatHandle.Entity).ValueRW.SetBit(dirtyStatHandle.Index);
                    HasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(dirtyStatHandle.Entity).ValueRW = true;
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
                baker.AddComponent(entity, new DirtyStatsMask
                {
                    StatsCount = statDefinitions.Length,
                });
                baker.AddComponent(entity, new HasDirtyStats());
                baker.SetComponentEnabled<HasDirtyStats>(entity, true);
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
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW)
        {
            if (statIndex >= 0 && statIndex < dirtyStatsMask.StatsCount)
            {
                dirtyStatsMask.SetBit(statIndex);
                hasDirtyStatsEnabledRefRW.ValueRW = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkStatForBatchRecompute(
            StatHandle statHandle,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup)
        {
            if (hasDirtyStatsLookup.HasComponent(statHandle.Entity))
            {
                ref DirtyStatsMask dirtyStatsMask = ref dirtyStatsMaskLookup.GetRefRW(statHandle.Entity).ValueRW;
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(statHandle.Entity);
                MarkStatForBatchRecompute(statHandle.Index, ref dirtyStatsMask, hasDirtyStatsEnabledRefRW);
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
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ModifierHandle modifierHandle = new ModifierHandle(Entity.Null, 0);
            if (affectedStatHandle.Index >= 0 && affectedStatHandle.Index < dirtyStatsMask.StatsCount)
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
                        ref dirtyStatsMask,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsMaskLookup,
                        ref hasDirtyStatsLookup);
                }

                MarkStatForBatchRecompute(affectedStatHandle.Index, ref dirtyStatsMask, hasDirtyStatsEnabledRefRW);
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
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ModifierHandle modifierHandle = new ModifierHandle(Entity.Null, 0);
            if (statOwnerLookup.TryGetComponent(statHandle.Entity, out StatOwner statOwner) &&
                statModifiersBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                statObserversBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                ref DirtyStatsMask dirtyStatsMask = ref dirtyStatsMaskLookup.GetRefRW(statHandle.Entity).ValueRW;
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(statHandle.Entity);
                modifierHandle = AddModifier<TStatModifier, TStatModifierStack>(
                    statHandle,
                    modifier,
                    ref statOwner,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref dirtyStatsMask,
                    hasDirtyStatsEnabledRefRW,
                    ref statObserversBufferLookup,
                    ref dirtyStatsMaskLookup,
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
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statHandle.Index >= 0 && statHandle.Index < dirtyStatsMask.StatsCount)
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
                        MarkStatForBatchRecompute(statHandle.Index, ref dirtyStatsMask, hasDirtyStatsEnabledRefRW);
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
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statOwnerLookup.TryGetComponent(statHandle.Entity, out StatOwner statOwner) &&
                statModifiersBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                statObserversBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                ref DirtyStatsMask dirtyStatsMask = ref dirtyStatsMaskLookup.GetRefRW(statHandle.Entity).ValueRW;
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(statHandle.Entity);
                RemoveModifier<TStatModifier, TStatModifierStack>(
                    statHandle,
                    modifierHandle,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref dirtyStatsMask,
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
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<HasDirtyStats> observerHasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup)    
        {
            // TODO: observer loop detection?

            if (observerStatHandle.Entity == observedStatHandle.Entity)
            {
                AddObserverToBuffer(observerStatHandle, observedStatHandle, ref observerStatObserversBuffer);
                MarkStatForBatchRecompute(observedStatHandle.Index, ref dirtyStatsMask, observerHasDirtyStatsEnabledRefRW);
            }
            else if (statObserversBufferLookup.TryGetBuffer(observedStatHandle.Entity, out DynamicBuffer<StatObserver> observedStatObserversBuffer))
            {
                AddObserverToBuffer(observerStatHandle, observedStatHandle, ref observedStatObserversBuffer);
                MarkStatForBatchRecompute(observedStatHandle, ref dirtyStatsMaskLookup, ref hasDirtyStatsLookup);
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
