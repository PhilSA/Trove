using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Assertions;

namespace Trove.Stats
{
    public static class StatsUtilities
    {
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
        public static float GetStatValue(
            StatHandle statHandle, 
            in DynamicBuffer<Stat> statsBuffer)
        {
            return statsBuffer[statHandle.Index].Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Stat GetStat(
            StatHandle statHandle, 
            in DynamicBuffer<Stat> statsBuffer)
        {
            return statsBuffer[statHandle.Index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetStatValue(
            StatHandle statHandle, 
            in DynamicBuffer<Stat> statsBuffer,
            out float value)
        {
            if (statHandle.Index < statsBuffer.Length)
            {
                Stat stat = statsBuffer[statHandle.Index];
                value = stat.Value;
                return true;
            }

            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetStat(
            StatHandle statHandle, 
            in DynamicBuffer<Stat> statsBuffer,
            out Stat stat)
        {
            if (statHandle.Index < statsBuffer.Length)
            {
                stat = statsBuffer[statHandle.Index];
                return true;
            }

            stat = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetStatValue(
            StatHandle statHandle, 
            in BufferLookup<Stat> statsLookup,
            out float value)
        {
            if (statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                return TryGetStatValue(statHandle, in statsBuffer, out value);
            }
            
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetStat(
            StatHandle statHandle, 
            in BufferLookup<Stat> statsLookup,
            out Stat stat)
        {
            if (statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                return TryGetStat(statHandle, in statsBuffer, out stat);
            }

            stat = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CreateStatCommon(Entity entity, float baseValue, bool produceChangeEvents, out StatHandle statHandle, ref DynamicBuffer<Stat> StatsBuffer)
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
            statHandle.Index = StatsBuffer.Length;
            
            StatsBuffer.Add(newStat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref StatValueReader statValueReader,
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
            CompactMultiLinkedListIterator<TStatModifier> modifiersIterator =
                new CompactMultiLinkedListIterator<TStatModifier>(statRef.LastModifierIndex);
            while (modifiersIterator.GetNext(in statModifiersBuffer, out TStatModifier modifier, out int modifierIndex))
            {
                modifier.Apply(
                    in statValueReader,
                    ref modifierStack);
                // TODO: give a way to say "the modifier depends on a now invalid stat and must be removed"
            }
            modifierStack.Apply(ref statRef.BaseValue, ref statRef.Value);

            // TODO: what if a modifier stack changes base value? Would be good to make that impossible
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
                CompactMultiLinkedListIterator<StatObserver> observersIterator =
                    new CompactMultiLinkedListIterator<StatObserver>(statRef.LastObserverIndex);
                while (observersIterator.GetNext(in statObserversBuffer, out StatObserver observer,
                           out int observerIndex))
                {
                    tmpUpdatedStatsList.Add(observer.ObserverHandle);
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
            StatHandle statHandle, 
            in DynamicBuffer<Stat> statsBufferOnStatEntity,
            in DynamicBuffer<StatObserver> statObserversBufferOnStatEntity,
            ref NativeList<StatObserver> statObserversList)
        {
            Assert.IsTrue(statHandle.Entity != Entity.Null);

            if (statHandle.Index < statsBufferOnStatEntity.Length)
            {
                Stat stat = statsBufferOnStatEntity[statHandle.Index];

                CompactMultiLinkedListIterator<StatObserver> observersIterator =
                    new CompactMultiLinkedListIterator<StatObserver>(stat.LastObserverIndex);
                while (observersIterator.GetNext(in statObserversBufferOnStatEntity,
                           out StatObserver observerOfStat, out int observerIndex))
                {
                    statObserversList.Add(observerOfStat);
                }
            }
            // TODO: else? 
        }

        internal static void AddStatAsObserverOfOtherStat(
            StatHandle observerStatHandle, 
            StatHandle observedStatHandle,
            ref DynamicBuffer<Stat> statsBufferOnObservedStatEntity,
            ref DynamicBuffer<StatObserver> statObserversBufferOnObservedStatEntity)
        {
            Assert.IsTrue(observerStatHandle.Entity != Entity.Null);

            Stat observedStat = statsBufferOnObservedStatEntity[observedStatHandle.Index];

            CollectionUtilities.AddToCompactMultiLinkedList(
                ref statObserversBufferOnObservedStatEntity,
                ref observedStat.LastObserverIndex, 
                new StatObserver { ObserverHandle = observerStatHandle });

            statsBufferOnObservedStatEntity[observedStatHandle.Index] = observedStat;
        }
    }
}