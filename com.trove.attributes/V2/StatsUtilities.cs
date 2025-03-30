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
            if (statRef.LastModifierIndex >= 0)
            {
                CompactMultiLinkedListIterator<TStatModifier> modifiersIterator =
                    new CompactMultiLinkedListIterator<TStatModifier>(statRef.LastModifierIndex);
                while (modifiersIterator.GetNext(in statModifiersBuffer, out TStatModifier modifier,
                           out int modifierIndex))
                {
                    modifier.Apply(
                        ref statValueReader,
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

        internal static void AddModifierPhase1<TStatModifier, TStatModifierStack>(
            StatHandle affectedStatHandle,
            ref StatsOwner statsOwnerRef,
            ref TStatModifier modifier,
            ref NativeList<StatHandle> tmpModifierObservedStatsList,
            out StatModifierHandle statModifierHandle)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            statModifierHandle = new StatModifierHandle
            {
                AffectedStatHandle = affectedStatHandle,
            };

            // Increment modifier Id (local to entity)
            statsOwnerRef.ModifierIDCounter++;
            modifier.ID = statsOwnerRef.ModifierIDCounter;
            statModifierHandle.ModifierID = modifier.ID;

            // Get observed stats of modifier
            modifier.AddObservedStatsToList(ref tmpModifierObservedStatsList);
        }

        internal static bool AddModifierPhase2<TStatModifier, TStatModifierStack>(
            bool allStatsAreOnAffectedStatEntity,
            StatHandle affectedStatHandle,
            in TStatModifier modifier,
            ref DynamicBuffer<Stat> statsBufferOnAffectedStatEntity,
            ref DynamicBuffer<TStatModifier> statModifiersBufferOnAffectedStatEntity,
            ref DynamicBuffer<StatObserver> statObserversBufferOnAffectedStatEntity,
            ref BufferLookup<Stat> statsLookup,
            ref BufferLookup<StatObserver> statObserversLookup,
            ref NativeList<StatHandle> tmpModifierObservedStatsList,
            ref NativeList<StatObserver> tmpStatObserversList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData,
            ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            bool modifierCanBeAdded = true;
            {
                // Make sure the modifier wouldn't make the stat observe itself (would cause infinite loop)
                for (int j = 0; j < tmpModifierObservedStatsList.Length; j++)
                {
                    StatHandle modifierObservedStatHandle = tmpModifierObservedStatsList[j];
                    if (affectedStatHandle == modifierObservedStatHandle)
                    {
                        modifierCanBeAdded = false;
                        break;
                    }
                }

                // Don't allow infinite observer loops.
                // Follow the chain of stats that would react to this stat's changes if the modifier was added (follow the 
                // observers chain). If we end up finding this stat anywhere in the chain, it would cause an infinite loop.
                // TODO: an alternative would be to configure a max stats update chain length and early exit an update if over limit
                if (modifierCanBeAdded)
                {
                    // Start by adding the affected stat's observers
                    StatsUtilities.AddObserversOfStatToList(
                        affectedStatHandle,
                        in statsBufferOnAffectedStatEntity,
                        in statObserversBufferOnAffectedStatEntity,
                        ref tmpStatObserversList);

                    // TODO: make sure this verification loop can't possibly end up being infinite either. It could be infinite if we haven't guaranteed loop detection for other modifier adds...
                    for (int i = 0; i < tmpStatObserversList.Length; i++)
                    {
                        StatHandle iteratedObserverStatHandle = tmpStatObserversList[i].ObserverHandle;

                        // If we find the affected stat down the chain of stats that it observes,
                        // it would create an infinite loop. Prevent adding modifier.
                        if (iteratedObserverStatHandle == affectedStatHandle)
                        {
                            modifierCanBeAdded = false;
                            break;
                        }

                        // Add the affected stat to the observers chain list if the iterated observer is
                        // an observed stat of the modifier. Because if we proceed with adding the modifier, the
                        // affected stat would be added as an observer of all modifier observed stats
                        for (int j = 0; j < tmpModifierObservedStatsList.Length; j++)
                        {
                            StatHandle modifierObservedStatHandle = tmpModifierObservedStatsList[j];
                            if (iteratedObserverStatHandle == modifierObservedStatHandle)
                            {
                                tmpModifierObservedStatsList.Add(affectedStatHandle);
                            }
                        }

                        // Add the observer's observers to the list
                        if (allStatsAreOnAffectedStatEntity)
                        {
                            StatsUtilities.AddObserversOfStatToList(
                                iteratedObserverStatHandle,
                                in statsBufferOnAffectedStatEntity,
                                in statObserversBufferOnAffectedStatEntity,
                                ref tmpStatObserversList);
                        }
                        // Update buffers so they represent the ones on the observer entity
                        else if (statsLookup.TryGetBuffer(iteratedObserverStatHandle.Entity, out DynamicBuffer<Stat> observerStatsBuffer) &&
                                 statObserversLookup.TryGetBuffer(iteratedObserverStatHandle.Entity, out DynamicBuffer<StatObserver> observerStatObserversBuffer))
                        {
                            StatsUtilities.AddObserversOfStatToList(
                                iteratedObserverStatHandle,
                                in observerStatsBuffer,
                                in observerStatObserversBuffer,
                                ref tmpStatObserversList);
                        }
                    }
                }
            }

            if (modifierCanBeAdded)
            {
                // Add modifier
                {
                    Stat affectedStat = statsBufferOnAffectedStatEntity[affectedStatHandle.Index];
                    CollectionUtilities.AddToCompactMultiLinkedList(ref statModifiersBufferOnAffectedStatEntity,
                        ref affectedStat.LastModifierIndex, modifier);
                    statsBufferOnAffectedStatEntity[affectedStatHandle.Index] = affectedStat;
                }

                // Add affected stat as observer of all observed stats
                for (int i = 0; i < tmpModifierObservedStatsList.Length; i++)
                {
                    StatHandle observedStatHandle = tmpModifierObservedStatsList[i];
                    
                    if (allStatsAreOnAffectedStatEntity)
                    {
                        Assert.IsTrue(observedStatHandle.Entity == affectedStatHandle.Entity);
                        
                        StatsUtilities.AddStatAsObserverOfOtherStat(
                            affectedStatHandle,
                            observedStatHandle,
                            ref statsBufferOnAffectedStatEntity,
                            ref statObserversBufferOnAffectedStatEntity);
                    }
                    // Update buffers so they represent the ones on the observer entity
                    else if (statsLookup.TryGetBuffer(observedStatHandle.Entity, out DynamicBuffer<Stat> observedStatsBuffer) &&
                               statObserversLookup.TryGetBuffer(observedStatHandle.Entity,
                                   out DynamicBuffer<StatObserver> observedStatObserversBuffer))
                    {
                        StatsUtilities.AddStatAsObserverOfOtherStat(
                            affectedStatHandle,
                            observedStatHandle,
                            ref observedStatsBuffer,
                            ref observedStatObserversBuffer);
                    }
                }

                return true;
            }

            return false;
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