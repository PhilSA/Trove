using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using NUnit.Framework;

namespace Trove.Stats
{
    public static class StatUtilities
    {
        public static void BakeStatsOwner<TStatModifier, TStatModifierStack>(
            IBaker baker,
            MonoBehaviour authoring,
            StatDefinition[] statDefinitions,
            bool supportEntityStatEvents)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Entity entity = baker.GetEntity(authoring, TransformUsageFlags.None);
            baker.AddComponent(entity, new StatOwner
            {
                ModifierIdCounter = 1,
            });
            baker.AddComponent(entity, new DirtyStatsMask
            {
                StatsCount = statDefinitions.Length,
            });
            baker.SetComponentEnabled<DirtyStatsMask>(entity, true);
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

            if (supportEntityStatEvents)
            {
                baker.AddComponent<HasEntityStatEvents>(entity);
                baker.SetComponentEnabled<HasEntityStatEvents>(entity, false);
                baker.AddBuffer<StatEvent<TStatModifier, TStatModifierStack>>(entity);
            }
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
        public static bool TryResolveStat(
            StatHandle statHandle,
            Entity cachedEntity,
            ref DynamicBuffer<Stat> cachedStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            out Stat result)
        {
            if (cachedEntity == statHandle.Entity)
            {
                if (statHandle.Index >= 0 && statHandle.Index < cachedStatsBuffer.Length)
                {
                    result = cachedStatsBuffer[statHandle.Index];
                    return true;
                }
            }
            else if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> resolvedStatsBuffer))
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
            StatHandle statHandle,
            ref BufferLookup<Stat> statsBufferLookup,
            out bool success)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> resolvedStatsBuffer))
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
        public unsafe static ref Stat TryResolveStatRef(
            StatHandle statHandle,
            Entity cachedEntity,
            ref DynamicBuffer<Stat> cachedStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            out bool success)
        {
            if (cachedEntity == statHandle.Entity)
            {
                if (statHandle.Index >= 0 && statHandle.Index < cachedStatsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(cachedStatsBuffer.GetUnsafePtr(), statHandle.Index);
                }
            }
            else if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> resolvedStatsBuffer))
            {
                if (statHandle.Index >= 0 && statHandle.Index < resolvedStatsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(resolvedStatsBuffer.GetUnsafePtr(), statHandle.Index);
                }
            }
            success = false;
            return ref UnsafeUtility.ArrayElementAsRef<Stat>(cachedStatsBuffer.GetUnsafePtr(), 0); ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkStatForBatchRecompute(
            int statIndex,
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW)
        {
            if (statIndex >= 0 && statIndex < dirtyStatsMask.StatsCount)
            {
                dirtyStatsMask.SetBit(statIndex);
                dirtyStatsMaskEnabledRefRW.ValueRW = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkStatForBatchRecompute(
            StatHandle statHandle,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup)
        {
            if (dirtyStatsMaskLookup.HasComponent(statHandle.Entity))
            {
                MarkStatForBatchRecompute_AssumeHasComponent(statHandle, ref dirtyStatsMaskLookup);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkStatForBatchRecompute_AssumeHasComponent(
            StatHandle statHandle,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup)
        {
            ref DirtyStatsMask dirtyStatsMask = ref dirtyStatsMaskLookup.GetRefRW(statHandle.Entity).ValueRW;
            EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW = dirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(statHandle.Entity);
            MarkStatForBatchRecompute(statHandle.Index, ref dirtyStatsMask, dirtyStatsMaskEnabledRefRW);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecomputeStatsAndObserversImmediate<TStatModifier, TStatModifierStack>(
            ref NativeQueue<StatHandle> statsQueue,
            ref BufferLookup<Stat> statsBufferLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            // TODO: infininte loop detection

            Entity cachedEntity = default;
            DynamicBuffer<Stat> cachedStatsBuffer = default;
            DynamicBuffer<TStatModifier> cachedStatModifiersBuffer = default;
            DynamicBuffer<StatObserver> cachedStatObserversBuffer = default;

            while (statsQueue.TryDequeue(out StatHandle dirtyStatHandle))
            {
                if (dirtyStatHandle.Entity != Entity.Null)
                {
                    if (dirtyStatHandle.Entity == cachedEntity)
                    {
                        RecomputeStatAndAddObserversToQueue<TStatModifier, TStatModifierStack>(
                            dirtyStatHandle,
                            ref cachedStatsBuffer,
                            ref cachedStatModifiersBuffer,
                            ref cachedStatObserversBuffer,
                            ref statsBufferLookup,
                            ref statsQueue);
                    }
                    else if (statsBufferLookup.TryGetBuffer(dirtyStatHandle.Entity, out cachedStatsBuffer))
                    {
                        cachedEntity = dirtyStatHandle.Entity;
                        cachedStatModifiersBuffer = statModifiersBufferLookup[dirtyStatHandle.Entity];
                        cachedStatObserversBuffer = statObserversBufferLookup[dirtyStatHandle.Entity];

                        RecomputeStatAndAddObserversToQueue<TStatModifier, TStatModifierStack>(
                            dirtyStatHandle,
                            ref cachedStatsBuffer,
                            ref cachedStatModifiersBuffer,
                            ref cachedStatObserversBuffer,
                            ref statsBufferLookup,
                            ref statsQueue);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecomputeStatAndObserversImmediate<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref DynamicBuffer<Stat> statsBuffer,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref NativeQueue<StatHandle> statsQueue)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            // TODO: infininte loop detection

            RecomputeStatAndAddObserversToQueue<TStatModifier, TStatModifierStack>(
                statHandle,
                ref statsBuffer,
                ref statModifiersBuffer,
                ref statObserversBuffer,
                ref statsBufferLookup,
                ref statsQueue);

            Entity cachedEntity = statHandle.Entity;
            DynamicBuffer<Stat> cachedStatsBuffer = statsBuffer;
            DynamicBuffer<TStatModifier> cachedStatModifiersBuffer = statModifiersBuffer;
            DynamicBuffer<StatObserver> cachedStatObserversBuffer = statObserversBuffer;

            while (statsQueue.TryDequeue(out StatHandle dirtyStatHandle))
            {
                if (dirtyStatHandle.Entity != Entity.Null)
                {
                    if (dirtyStatHandle.Entity == cachedEntity)
                    {
                        RecomputeStatAndAddObserversToQueue<TStatModifier, TStatModifierStack>(
                            dirtyStatHandle,
                            ref cachedStatsBuffer,
                            ref cachedStatModifiersBuffer,
                            ref cachedStatObserversBuffer,
                            ref statsBufferLookup,
                            ref statsQueue);
                    }
                    else if (statsBufferLookup.TryGetBuffer(dirtyStatHandle.Entity, out cachedStatsBuffer))
                    {
                        cachedEntity = dirtyStatHandle.Entity;
                        cachedStatModifiersBuffer = statModifiersBufferLookup[dirtyStatHandle.Entity];
                        cachedStatObserversBuffer = statObserversBufferLookup[dirtyStatHandle.Entity];

                        RecomputeStatAndAddObserversToQueue<TStatModifier, TStatModifierStack>(
                            dirtyStatHandle,
                            ref cachedStatsBuffer,
                            ref cachedStatModifiersBuffer,
                            ref cachedStatObserversBuffer,
                            ref statsBufferLookup,
                            ref statsQueue);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RecomputeStatAndAddObserversToQueue<TStatModifier, TStatModifierStack>(
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
                        statHandle.Entity,
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
        public static ModifierHandle AddModifier_ObservesSameEntity<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            TStatModifier modifier,
            ref StatOwner statOwner,
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ModifierHandle modifierHandle = new ModifierHandle(Entity.Null, 0);
            if (statHandle.Index >= 0 && statHandle.Index < dirtyStatsMask.StatsCount)
            {
                uint modifierId = statOwner.ModifierIdCounter++;
                modifierHandle = new ModifierHandle(statHandle.Entity, modifierId);
                modifier.Id = modifierId;
                modifier.AffectedStat = statHandle;

                statModifiersBuffer.Add(modifier);

                tmpObservedStatsList.Clear();
                modifier.AddObservedStatsToList(ref tmpObservedStatsList);
                for (int i = 0; i < tmpObservedStatsList.Length; i++)
                {
                    StatHandle observedStatHandle = tmpObservedStatsList[i];
                    Assert.AreEqual(observedStatHandle.Entity, statHandle.Entity);

                    AddObserverToBuffer(statHandle, observedStatHandle, ref statObserversBuffer);
                    MarkStatForBatchRecompute(observedStatHandle.Index, ref dirtyStatsMask, dirtyStatsMaskEnabledRefRW);
                }

                MarkStatForBatchRecompute(statHandle.Index, ref dirtyStatsMask, dirtyStatsMaskEnabledRefRW);
            }
            return modifierHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierHandle AddModifier<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            TStatModifier modifier,
            ref ComponentLookup<StatOwner> statOwnerLookup,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
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
                EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW = dirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(statHandle.Entity);
                modifierHandle = AddModifier<TStatModifier, TStatModifierStack>(
                    statHandle,
                    modifier,
                    ref statOwner,
                    ref dirtyStatsMask,
                    dirtyStatsMaskEnabledRefRW,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref dirtyStatsMaskLookup,
                    ref statObserversBufferLookup,
                    ref tmpObservedStatsList);

                statOwnerLookup[statHandle.Entity] = statOwner;
            }
            return modifierHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierHandle AddModifier<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            TStatModifier modifier,
            ref StatOwner statOwner,
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ModifierHandle modifierHandle = new ModifierHandle(Entity.Null, 0);
            if (statHandle.Index >= 0 && statHandle.Index < dirtyStatsMask.StatsCount)
            {
                uint modifierId = statOwner.ModifierIdCounter++;
                modifierHandle = new ModifierHandle(statHandle.Entity, modifierId);
                modifier.Id = modifierId;
                modifier.AffectedStat = statHandle;

                statModifiersBuffer.Add(modifier);

                tmpObservedStatsList.Clear();
                modifier.AddObservedStatsToList(ref tmpObservedStatsList);
                for (int i = 0; i < tmpObservedStatsList.Length; i++)
                {
                    AddAsObserverOf(
                        statHandle,
                        tmpObservedStatsList[i],
                        ref statObserversBuffer,
                        ref dirtyStatsMask,
                        dirtyStatsMaskEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsMaskLookup);
                }

                MarkStatForBatchRecompute(statHandle.Index, ref dirtyStatsMask, dirtyStatsMaskEnabledRefRW);
            }
            return modifierHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveModifier<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ModifierHandle modifierHandle,
            ref ComponentLookup<StatOwner> statOwnerLookup,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statOwnerLookup.TryGetComponent(statHandle.Entity, out StatOwner statOwner) &&
                statModifiersBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                statObserversBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                ref DirtyStatsMask dirtyStatsMask = ref dirtyStatsMaskLookup.GetRefRW(statHandle.Entity).ValueRW;
                EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW = dirtyStatsMaskLookup.GetEnabledRefRW<DirtyStatsMask>(statHandle.Entity);
                RemoveModifier<TStatModifier, TStatModifierStack>(
                    statHandle,
                    modifierHandle,
                    ref dirtyStatsMask,
                    dirtyStatsMaskEnabledRefRW,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref statObserversBufferLookup,
                    ref tmpObservedStatsList);

                statOwnerLookup[statHandle.Entity] = statOwner;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveModifier<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ModifierHandle modifierHandle,
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
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
                        MarkStatForBatchRecompute(statHandle.Index, ref dirtyStatsMask, dirtyStatsMaskEnabledRefRW);
                        return;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddAsObserverOf(
            StatHandle observerStatHandle,
            StatHandle observedStatHandle,
            ref DynamicBuffer<StatObserver> observerStatObserversBuffer,
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<DirtyStatsMask> observerDirtyStatsMaskEnabledRefRW,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup)
        {
            // TODO: observer loop detection?

            if (observerStatHandle.Entity == observedStatHandle.Entity)
            {
                AddObserverToBuffer(observerStatHandle, observedStatHandle, ref observerStatObserversBuffer);
                MarkStatForBatchRecompute(observedStatHandle.Index, ref dirtyStatsMask, observerDirtyStatsMaskEnabledRefRW);
            }
            else if (statObserversBufferLookup.TryGetBuffer(observedStatHandle.Entity, out DynamicBuffer<StatObserver> observedStatObserversBuffer))
            {
                AddObserverToBuffer(observerStatHandle, observedStatHandle, ref observedStatObserversBuffer);
                MarkStatForBatchRecompute(observedStatHandle, ref dirtyStatsMaskLookup);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddObserverToBuffer(
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
        internal static void RemoveAsObserverOf(
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
        internal static void RemoveObserverFromBuffer(
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