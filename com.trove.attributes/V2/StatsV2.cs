using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;

namespace Trove.Stats
{
    // TODO: configurable buffer capacities for all stat buffer types
    [InternalBufferCapacity(0)]
    public partial struct StatModifier : IBufferElementData
    {
        public enum Type
        {
            Add,
            AddFromStat,
            AddMultiplier,
            AddMultiplierFromStat,
        }

        public struct Stack
        {
            public float Add;
            public float AddMultiply;

            public static Stack New()
            {
                return new Stack
                {
                    Add = 0f,
                    AddMultiply = 1f,
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Apply(ref Stat stat)
            {
                stat.Value = stat.BaseValue;
                stat.Value += Add;
                stat.Value *= AddMultiply;
            }
        }

        // TODO: how to inform of the fact that it's not the user's job to assign ID and AffectedStat
        public uint Id;
        public StatHandle AffectedStat;

        public Type ModifierType;
        public float ValueA;
        public StatHandle StatA;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObservedStatsToList(ref UnsafeList<StatHandle> observedStats)
        {
            switch (ModifierType)
            {
                case (Type.AddFromStat):
                case (Type.AddMultiplierFromStat):
                    observedStats.Add(StatA);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(
            ref Stack stack,
            StatHandle selfStatHandle,
            ref DynamicBuffer<Stat> selfStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup)
        {
            switch (ModifierType)
            {
                case (Type.Add):
                    {
                        stack.Add += ValueA;
                        break;
                    }
                case (Type.AddFromStat):
                    {
                        if (StatUtilities.TryResolveStat(selfStatHandle, StatA, ref selfStatsBuffer, ref statsBufferLookup, out Stat resolvedStat))
                        {
                            stack.Add += resolvedStat.Value;
                        }
                        break;
                    }
                case (Type.AddMultiplier):
                    {
                        stack.AddMultiply += ValueA;
                        break;
                    }
                case (Type.AddMultiplierFromStat):
                    {
                        if (StatUtilities.TryResolveStat(selfStatHandle, StatA, ref selfStatsBuffer, ref statsBufferLookup, out Stat resolvedStat))
                        {
                            stack.AddMultiply += resolvedStat.Value;
                        }
                        break;
                    }
            }
        }
    }

    ///////////////////////////////////////////////////////////////////

    public struct StatOwner : IComponentData
    {
        public uint ModifierIdCounter;
    }

    [InternalBufferCapacity(0)]
    public partial struct Stat : IBufferElementData
    {
        public byte Exists;
        public float BaseValue;
        public float Value;
    }

    [InternalBufferCapacity(0)]
    public partial struct StatObserver : IBufferElementData
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

    public struct StatHandle
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
            return x.Entity == y.Entity && x.Index == y.Index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(StatHandle x, StatHandle y)
        {
            return x.Entity != y.Entity || x.Index != y.Index;
        }
    }

    public struct ModifierHandle
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
    public partial struct StatsUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: process stat commands

            int maxStatsDependencyDepth = 5; // TODO: make configurable or detected at runtime
            for (int i = 0; i < maxStatsDependencyDepth; i++)
            {
                // TODO: have a stats update group in which we can add systems that react to stat changes?

                state.Dependency = new RecomputeDirtyStatsJob
                {
                    HasDirtyStatsLookup = SystemAPI.GetComponentLookup<HasDirtyStats>(false),
                    StatsBufferLookup = SystemAPI.GetBufferLookup<Stat>(false),
                    DirtyStatsBufferLookup = SystemAPI.GetBufferLookup<DirtyStat>(false),
                }.ScheduleParallel(state.Dependency);
            }
        }

        [BurstCompile]
        [WithAll(typeof(HasDirtyStats))]
        public partial struct RecomputeDirtyStatsJob : IJobEntity
        {
            [NativeDisableParallelForRestriction]
            public ComponentLookup<HasDirtyStats> HasDirtyStatsLookup;
            [NativeDisableContainerSafetyRestriction]
            public BufferLookup<Stat> StatsBufferLookup;
            [NativeDisableContainerSafetyRestriction]
            public BufferLookup<DirtyStat> DirtyStatsBufferLookup;

            void Execute(
                Entity entity,
                ref DynamicBuffer<Stat> statsBuffer, 
                ref DynamicBuffer<StatModifier> statModifiersBuffer, 
                ref DynamicBuffer<StatObserver> statObserversBuffer,
                ref DynamicBuffer<DirtyStat> dirtyStatsBuffer)
            {
                for (int statIndex = 0; statIndex < dirtyStatsBuffer.Length; statIndex++)
                {
                    if (dirtyStatsBuffer[statIndex].Value == 1)
                    {
                        StatHandle selfStatHandle = new StatHandle(entity, statIndex);
                        Stat stat = statsBuffer[statIndex];

                        // Apply Modifiers
                        StatModifier.Stack modifierStack = StatModifier.Stack.New();
                        for (int m = 0; m < statModifiersBuffer.Length; m++)
                        {
                            StatModifier modifier = statModifiersBuffer[m];
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

                        // Notify Observers
                        for (int o = statObserversBuffer.Length - 1; o >= 0; o--)
                        {
                            StatObserver observer = statObserversBuffer[o];
                            StatUtilities.MarkStatForRecompute(
                                observer.ObserverStat,
                                ref DirtyStatsBufferLookup,
                                ref HasDirtyStatsLookup);
                        }

                        statsBuffer[statIndex] = stat;
                        dirtyStatsBuffer[statIndex] = default;
                        HasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(entity).ValueRW = false;
                    }
                }
            }
        }
    }

    public static class StatUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryResolveStat(
            StatHandle selfStat,
            StatHandle resolvedStat,
            ref DynamicBuffer<Stat> selfStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            out Stat result)
        {
            if (selfStat.Entity == resolvedStat.Entity)
            {
                if (resolvedStat.Index >= 0 && resolvedStat.Index < selfStatsBuffer.Length)
                {
                    result = selfStatsBuffer[resolvedStat.Index]; 
                    return true;
                }
            }
            else if (statsBufferLookup.TryGetBuffer(resolvedStat.Entity, out DynamicBuffer<Stat> resolvedStatsBuffer))
            {
                if (resolvedStat.Index >= 0 && resolvedStat.Index < resolvedStatsBuffer.Length)
                {
                    result = resolvedStatsBuffer[resolvedStat.Index];
                    return true;
                }
            }
            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static ref Stat TryResolveStatRef(
            StatHandle selfStat,
            StatHandle resolvedStat,
            ref DynamicBuffer<Stat> selfStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            out bool success)
        {
            if (selfStat.Entity == resolvedStat.Entity)
            {
                if (resolvedStat.Index >= 0 && resolvedStat.Index < selfStatsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(selfStatsBuffer.GetUnsafePtr(), resolvedStat.Index);
                }
            }
            else if (statsBufferLookup.TryGetBuffer(resolvedStat.Entity, out DynamicBuffer<Stat> resolvedStatsBuffer))
            {
                if (resolvedStat.Index >= 0 && resolvedStat.Index < resolvedStatsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(resolvedStatsBuffer.GetUnsafePtr(), resolvedStat.Index);
                }
            }
            success = false;
            return ref UnsafeUtility.ArrayElementAsRef<Stat>(selfStatsBuffer.GetUnsafePtr(), 0); ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkStatForRecompute(
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
        public static void MarkStatForRecompute(
            StatHandle stat,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup)
        {
            if (dirtyStatsBufferLookup.TryGetBuffer(stat.Entity, out DynamicBuffer<DirtyStat> dirtyStatsBuffer))
            {
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(stat.Entity);
                MarkStatForRecompute(stat.Index, ref dirtyStatsBuffer, hasDirtyStatsEnabledRefRW);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierHandle AddModifier(
            StatHandle affectedStat,
            StatModifier modifier,
            ref StatOwner statOwner,
            ref DynamicBuffer<StatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref DynamicBuffer<DirtyStat> dirtyStatsBuffer,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
        {
            ModifierHandle modifierHandle = new ModifierHandle(Entity.Null, 0);
            if (affectedStat.Index >= 0 && affectedStat.Index < dirtyStatsBuffer.Length)
            {
                uint modifierId = statOwner.ModifierIdCounter++;
                modifierHandle = new ModifierHandle(affectedStat.Entity, modifierId);
                modifier.Id = modifierId;
                modifier.AffectedStat = affectedStat;

                statModifiersBuffer.Add(modifier);

                tmpObservedStatsList.Clear();
                modifier.AddObservedStatsToList(ref tmpObservedStatsList);
                for (int i = 0; i < tmpObservedStatsList.Length; i++)
                {
                    AddAsObserverOf(
                        affectedStat,
                        tmpObservedStatsList[i],
                        ref statObserversBuffer,
                        ref dirtyStatsBuffer,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsBufferLookup,
                        ref hasDirtyStatsLookup);
                }

                MarkStatForRecompute(affectedStat.Index, ref dirtyStatsBuffer, hasDirtyStatsEnabledRefRW);
            }
            return modifierHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierHandle AddModifier(
            StatHandle stat,
            StatModifier modifier,
            ref ComponentLookup<StatOwner> statOwnerLookup,
            ref BufferLookup<StatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
        {
            ModifierHandle modifierHandle = new ModifierHandle(Entity.Null, 0);
            if (statOwnerLookup.TryGetComponent(stat.Entity, out StatOwner statOwner) &&
                statModifiersBufferLookup.TryGetBuffer(stat.Entity, out DynamicBuffer<StatModifier> statModifiersBuffer) &&
                statObserversBufferLookup.TryGetBuffer(stat.Entity, out DynamicBuffer<StatObserver> statObserversBuffer) &&
                dirtyStatsBufferLookup.TryGetBuffer(stat.Entity, out DynamicBuffer<DirtyStat> dirtyStatsBuffer))
            {
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(stat.Entity);
                modifierHandle = AddModifier(
                    stat,
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

                statOwnerLookup[stat.Entity] = statOwner;
            }
            return modifierHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveModifier(
            StatHandle stat,
            ModifierHandle modifierHandle,
            ref DynamicBuffer<StatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref DynamicBuffer<DirtyStat> dirtyStatsBuffer,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
        {
            if (stat.Index >= 0 && stat.Index < dirtyStatsBuffer.Length)
            {
                for (int i = statModifiersBuffer.Length - 1; i >= 0; i--)
                {
                    StatModifier statModifier = statModifiersBuffer[i];
                    if(modifierHandle.Id == statModifier.Id)
                    {
                        tmpObservedStatsList.Clear();
                        statModifier.AddObservedStatsToList(ref tmpObservedStatsList);
                        for (int o = 0; o < tmpObservedStatsList.Length; o++)
                        {
                            RemoveAsObserverOf(
                                stat,
                                tmpObservedStatsList[o],
                                ref statObserversBuffer,
                                ref statObserversBufferLookup);
                        }

                        statModifiersBuffer.RemoveAt(i);
                        MarkStatForRecompute(stat.Index, ref dirtyStatsBuffer, hasDirtyStatsEnabledRefRW);
                        return;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveModifier(
            StatHandle stat,
            ModifierHandle modifierHandle,
            ref ComponentLookup<StatOwner> statOwnerLookup,
            ref BufferLookup<StatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
        {
            if (statOwnerLookup.TryGetComponent(stat.Entity, out StatOwner statOwner) &&
                statModifiersBufferLookup.TryGetBuffer(stat.Entity, out DynamicBuffer<StatModifier> statModifiersBuffer) &&
                statObserversBufferLookup.TryGetBuffer(stat.Entity, out DynamicBuffer<StatObserver> statObserversBuffer) &&
                dirtyStatsBufferLookup.TryGetBuffer(stat.Entity, out DynamicBuffer<DirtyStat> dirtyStatsBuffer))
            {
                EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<HasDirtyStats>(stat.Entity);
                RemoveModifier(
                    stat,
                    modifierHandle,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref dirtyStatsBuffer,
                    hasDirtyStatsEnabledRefRW,
                    ref statObserversBufferLookup,
                    ref tmpObservedStatsList);

                statOwnerLookup[stat.Entity] = statOwner;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddAsObserverOf(
            StatHandle observerStat,
            StatHandle observedStat,
            ref DynamicBuffer<StatObserver> observerStatObserversBuffer,
            ref DynamicBuffer<DirtyStat> observerDirtyStatsBuffer,
            EnabledRefRW<HasDirtyStats> observerHasDirtyStatsEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<DirtyStat> dirtyStatsBufferLookup,
            ref ComponentLookup<HasDirtyStats> hasDirtyStatsLookup)
        {
            // TODO: observer loop detection?

            if (observerStat.Entity == observedStat.Entity)
            {
                AddObserverToBuffer(observerStat, observedStat, ref observerStatObserversBuffer);
                MarkStatForRecompute(observedStat.Index, ref observerDirtyStatsBuffer, observerHasDirtyStatsEnabledRefRW);
            }
            else if (statObserversBufferLookup.TryGetBuffer(observedStat.Entity, out DynamicBuffer<StatObserver> observedStatObserversBuffer))
            {
                AddObserverToBuffer(observerStat, observedStat, ref observedStatObserversBuffer);
                MarkStatForRecompute(observedStat, ref dirtyStatsBufferLookup, ref hasDirtyStatsLookup);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddObserverToBuffer(
            StatHandle observerStat,
            StatHandle observedStat,
            ref DynamicBuffer<StatObserver> observedStatObserversBuffer)
        {
            for (int i = 0; i < observedStatObserversBuffer.Length; i++)
            {
                StatObserver statObserver = observedStatObserversBuffer[i];
                if(statObserver.ObserverStat == observerStat)
                {
                    statObserver.Count++;
                    observedStatObserversBuffer[i] = statObserver;
                    return;
                }
            }

            observedStatObserversBuffer.Add(new StatObserver(observerStat, observedStat, 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RemoveAsObserverOf(
            StatHandle observerStat,
            StatHandle observedStat,
            ref DynamicBuffer<StatObserver> observerStatObserversBuffer,
            ref BufferLookup<StatObserver> statObserversBufferLookup)
        {
            if (observerStat.Entity == observedStat.Entity)
            {
                RemoveObserverFromBuffer(observerStat, ref observerStatObserversBuffer);
            }
            else if (statObserversBufferLookup.TryGetBuffer(observedStat.Entity, out DynamicBuffer<StatObserver> observedStatObserversBuffer))
            {
                RemoveObserverFromBuffer(observerStat, ref observedStatObserversBuffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RemoveObserverFromBuffer(
            StatHandle observerStat,
            ref DynamicBuffer<StatObserver> observedStatObserversBuffer)
        {
            for (int i = observedStatObserversBuffer.Length - 1; i >= 0; i--)
            {
                StatObserver statObserver = observedStatObserversBuffer[i];
                if (statObserver.ObserverStat == observerStat)
                {
                    statObserver.Count--;
                    if(statObserver.Count <= 0)
                    {
                        observedStatObserversBuffer.RemoveAt(i);
                    }
                    return;
                }
            }
        }
    }
}
