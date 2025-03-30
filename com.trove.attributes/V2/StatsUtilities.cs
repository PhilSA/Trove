using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Trove.Stats
{
    public static class StatsUtilities
    {
        public static void BakeStatsComponents<TStatModifier, TStatModifierStack>(IBaker baker, Entity entity, out StatsBaker<TStatModifier, TStatModifierStack> statsBaker)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            baker.AddComponent(entity,new StatsOwner());
            statsBaker =  new StatsBaker<TStatModifier, TStatModifierStack>
            {
                Baker = baker,
                Entity = entity,

                StatsOwner = default,
                StatsBuffer = baker.AddBuffer<Stat>(entity),
                StatModifiersBuffer = baker.AddBuffer<TStatModifier>(entity),
                StatObserversBuffer = baker.AddBuffer<StatObserver>(entity),
            };
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityManager entityManager)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            entityManager.AddComponentData(entity, new StatsOwner());
            entityManager.AddBuffer<Stat>(entity);
            entityManager.AddBuffer<TStatModifier>(entity);
            entityManager.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer ecb)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(entity, new StatsOwner());
            ecb.AddBuffer<Stat>(entity);
            ecb.AddBuffer<TStatModifier>(entity);
            ecb.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer.ParallelWriter ecb, int sortKey)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(sortKey, entity, new StatsOwner());
            ecb.AddBuffer<Stat>(sortKey, entity);
            ecb.AddBuffer<TStatModifier>(sortKey, entity);
            ecb.AddBuffer<StatObserver>(sortKey, entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CreateStat(
            Entity entity, 
            float baseValue, 
            bool produceChangeEvents, 
            ref DynamicBuffer<Stat> statsBuffer,
            out StatHandle statHandle)
        {
            statHandle = new StatHandle
            {
                Entity = entity,
                Index = -1,
            };

            Stat newStat = new Stat
            {
                BaseValue = baseValue,
                Value = baseValue,
                
                LastModifierIndex = -1,
                LastObserverIndex = -1,
                
                ProduceChangeEvents = produceChangeEvents ? (byte)1 : (byte)0,
            };
            
            statHandle.Index = statsBuffer.Length;
            statsBuffer.Add(newStat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref StatsReader statsReader,
            ref Stat statRef,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref NativeList<StatChangeEvent> statChangeEventsList,
            ref NativeList<StatHandle> tmpUpdatedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Stat initialStat = statRef;
            
            // Apply Modifiers
            TStatModifierStack modifierStack = new TStatModifierStack();
            modifierStack.Reset();
            if (statRef.LastModifierIndex >= 0)
            {
                CompactMultiLinkedListIterator<TStatModifier> modifiersIterator =
                    new CompactMultiLinkedListIterator<TStatModifier>(statRef.LastModifierIndex);
                while (modifiersIterator.GetNext(in statModifiersBuffer, out TStatModifier modifier,
                           out int modifierIndex))
                {
                    modifier.Apply(
                        ref statsReader,
                        ref modifierStack);
                    // TODO: give a way to say "the modifier depends on a now invalid stat and must be removed"
                }
            }
            modifierStack.Apply(in statRef.BaseValue, ref statRef.Value);

            // If the stat value really changed
            if (initialStat.Value != statRef.Value)
            {
                // Stat change events
                if (statRef.ProduceChangeEvents == 1 && statChangeEventsList.IsCreated)
                {
                    statChangeEventsList.Add(new StatChangeEvent
                    {
                        StatHandle = statHandle,
                        PrevValue = initialStat,
                        NewValue = statRef,
                    });
                }

                // Notify Observers (add to update list)
                if (statRef.LastObserverIndex >= 0)
                {
                    CompactMultiLinkedListIterator<StatObserver> observersIterator =
                        new CompactMultiLinkedListIterator<StatObserver>(statRef.LastObserverIndex);
                    while (observersIterator.GetNext(in statObserversBuffer, out StatObserver observer,
                               out int observerIndex))
                    {
                        tmpUpdatedStatsList.Add(observer.ObserverHandle);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void EnsureClearedValidTempList<T>(ref NativeList<T> list) where T : unmanaged
        {
            if (!list.IsCreated)
            {
                list = new NativeList<T>(Allocator.Temp);
            }
            list.Clear();
        }

        internal static void AddObserversOfStatToList(
            in Stat stat,
            in DynamicBuffer<StatObserver> statObserversBufferOnStatEntity,
            ref NativeList<StatObserver> statObserversList)
        {
            if (stat.LastObserverIndex >= 0)
            {
                CompactMultiLinkedListIterator<StatObserver> observersIterator =
                    new CompactMultiLinkedListIterator<StatObserver>(stat.LastObserverIndex);
                while (observersIterator.GetNext(in statObserversBufferOnStatEntity,
                           out StatObserver observerOfStat, out int observerIndex))
                {
                    statObserversList.Add(observerOfStat);
                }
            }
        }

        internal static void AddStatAsObserverOfOtherStat(
            StatHandle observerStatHandle, 
            StatHandle observedStatHandle,
            ref DynamicBuffer<Stat> statsBufferOnObservedStat,
            ref DynamicBuffer<StatObserver> statObserversBufferOnObservedStatEntity)
        {
            Assert.IsTrue(observerStatHandle.Entity != Entity.Null);

            if (observedStatHandle.Index < statsBufferOnObservedStat.Length)
            {
                Stat observedStat = statsBufferOnObservedStat[observedStatHandle.Index];
                
                CollectionUtilities.AddToCompactMultiLinkedList(
                    ref statObserversBufferOnObservedStatEntity,
                    ref observedStat.LastObserverIndex, 
                    new StatObserver { ObserverHandle = observerStatHandle });
                
                statsBufferOnObservedStat[observedStatHandle.Index] = observedStat;
            }
            // TODO: else?
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetStat(StatHandle statHandle, ref BufferLookup<Stat> statsBufferLookup, out Stat stat)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> datasBuffer))
            {
                if (statHandle.Index < datasBuffer.Length)
                {
                    stat = datasBuffer[statHandle.Index];
                    return true;
                }
            }

            stat = default;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ref Stat GetStatRefUnsafe(StatHandle statHandle, ref BufferLookup<Stat> statsBufferLookup, out bool success, ref Stat nullResult)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), statHandle.Index);
                }
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TrySetStat(StatHandle statHandle, Stat stat, ref BufferLookup<Stat> statsBufferLookup)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> datasBuffer))
            {
                if (statHandle.Index < datasBuffer.Length)
                {
                    datasBuffer[statHandle.Index] = stat;
                    return true;
                }
            }

            stat = default;
            return false;
        }
    }
}