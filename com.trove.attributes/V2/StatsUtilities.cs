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
        internal static bool AddModifierCommon<TStatModifier, TStatModifierStack>(
            bool isForBaking,
            bool infiniteLoopsDetection,
            StatHandle affectedStatHandle,
            ref TStatModifier modifier,
            ref StatObserversHandler statObserversHandler,
            ref StatsOwner statsOwner,
            out StatModifierHandle statModifierHandle,
            ref DynamicBuffer<Stat> statsBufferOnAffectedStatEntity,
            ref DynamicBuffer<TStatModifier> statModifiersBufferOnAffectedStatEntity,
            ref NativeList<StatHandle> tmpModifierObservedStatsList,
            ref NativeList<StatObserver> tmpStatObserversList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            statModifierHandle = new StatModifierHandle
            {
                AffectedStatHandle = affectedStatHandle,
            };

            // Increment modifier Id (local to entity)
            statsOwner.ModifierIDCounter++;
            modifier.ID = statsOwner.ModifierIDCounter;
            statModifierHandle.ModifierID = modifier.ID;

            // Ensure lists are created and cleared
            EnsureClearedValidTempList(ref tmpModifierObservedStatsList);
            EnsureClearedValidTempList(ref tmpStatObserversList);

            // Get observed stats of modifier
            modifier.AddObservedStatsToList(ref tmpModifierObservedStatsList);

            // In baking, don't allow observing stats from other entities
            if (isForBaking)
            {
                for (int i = 0; i < tmpModifierObservedStatsList.Length; i++)
                {
                    StatHandle observedStatHandle = tmpModifierObservedStatsList[i];

                    if (observedStatHandle.Entity != affectedStatHandle.Entity)
                    {
                        throw new Exception(
                            "Adding stat modifiers that observe stats of entities other than the baked entity is not allowed during baking.");
                        return false;
                    }
                }
            }
            
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
                if (modifierCanBeAdded && infiniteLoopsDetection)
                {
                    statObserversHandler.AddObserversOfStatToList(affectedStatHandle, ref tmpStatObserversList);
                    
                    // TODO: make sure this verification loop can't possibly end up being infinite either. It could be infinite if we haven't guaranteed loop detection for other modifier adds...
                    for (int i = 0; i < tmpStatObserversList.Length; i++)
                    {
                        StatObserver statObserver = tmpStatObserversList[i];

                        // If we find the affected stat down the chain of stats that it observes,
                        // it would create an infinite loop. Prevent adding modifier.
                        if (statObserver.ObserverHandle == affectedStatHandle)
                        {
                            modifierCanBeAdded = false;
                            break;
                        }

                        // Check the all the observed stats of affected stat, and add them to the list if we're iterating
                        // their observed stat
                        for (int j = 0; j < tmpModifierObservedStatsList.Length; j++)
                        {
                            StatHandle modifierObservedStatHandle = tmpModifierObservedStatsList[j];
                            if (statObserver.ObserverHandle == modifierObservedStatHandle)
                            {
                                tmpModifierObservedStatsList.Add(modifierObservedStatHandle);
                            }
                        }

                        statObserversHandler.AddObserversOfStatToList(statObserver.ObserverHandle,
                            ref tmpStatObserversList);
                    }
                }
            }
            
            if (modifierCanBeAdded)
            {
                // Add modifier
                {
                    Stat affectedStat = statsBufferOnAffectedStatEntity[affectedStatHandle.Index];

                    // Add modifier at the end of the buffer, and remember the previous modifier index
                    int modifierAddIndex = statModifiersBufferOnAffectedStatEntity.Length;
                    modifier.PrevModifierIndex = affectedStat.LastObserverIndex;
                    statModifiersBufferOnAffectedStatEntity.Add(modifier);

                    // Update the last modifier index for the affected stat
                    affectedStat.LastObserverIndex = modifierAddIndex;

                    statsBufferOnAffectedStatEntity[affectedStatHandle.Index] = affectedStat;
                }
                
                // Add affected stat as observer of observed stats
                for (int i = 0; i < tmpModifierObservedStatsList.Length; i++)
                {
                    StatHandle observedStatHandle = tmpModifierObservedStatsList[i];
                    statObserversHandler.AddStatAsObserverOfOtherStat(affectedStatHandle, observedStatHandle);
                }

                return true;
            }

            return false;
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
            while (modifiersIterator.GetNext(ref statModifiersBuffer, out TStatModifier modifier, out int modifierIndex))
            {
                modifier.Apply(
                    in statValueReader,
                    ref modifierStack);
                // TODO: give a way to say "the modifier depends on a now invalid stat and must be removed"
            }
            modifierStack.Apply(ref statRef.BaseValue, ref statRef.Value);

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
            while (observersIterator.GetNext(ref statObserversBuffer, out StatObserver observer, out int observerIndex))
            {
                tmpUpdatedStatsList.Add(observer.ObserverHandle);
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

        internal static void AddObserversOfStatToList(StatHandle statHandle, ref NativeList<StatObserver> statObserversList)
        {
            Assert.IsTrue(statHandle.Entity != Entity.Null);

            if (!_isForBaking)
            {
                UpdateBuffers(statHandle.Entity);
            }

            if (statHandle.Index < _latestStatsBufferOnObservedStat.Length)
            {
                Stat stat = _latestStatsBufferOnObservedStat[statHandle.Index];

                int iteratedPrevObserverIndex = stat.LastObserverIndex;
                while (iteratedPrevObserverIndex >= 0)
                {
                    StatObserver statObserver = _latestStatObserversBufferOnObservedStat[iteratedPrevObserverIndex];
                    statObserversList.Add(statObserver);
                    iteratedPrevObserverIndex = statObserver.PrevObserverIndex;
                }
            }
            // TODO: else? 
        }

        internal static bool AddStatAsObserverOfOtherStat(StatHandle observerStatHandle, StatHandle observedStatHandle)
        {
            Assert.IsTrue(observerStatHandle.Entity != Entity.Null);

            // When we're not in baking, we have to update our observers buffer based on the observed stat entity.
            if (!_isForBaking)
            {
                bool updateBuffersSuccess = UpdateBuffers(observedStatHandle.Entity);
                if (!updateBuffersSuccess)
                {
                    return false;
                }
            }
            // In baking, we always assume we're staying on the same observers buffer.
            else
            {
                Assert.IsTrue(observerStatHandle.Entity == observedStatHandle.Entity);
            }

            Stat observedStat = _latestStatsBufferOnObservedStat[observedStatHandle.Index];

            // Add observer at the end of the buffer, and remember the previous observer index
            int observerAddIndex = _latestStatObserversBufferOnObservedStat.Length;
            _latestStatObserversBufferOnObservedStat.Add(new StatObserver
            {
                PrevObserverIndex = observedStat.LastObserverIndex,
                ObserverHandle = observerStatHandle,
            });

            // Update the last observer index for the observed stat
            observedStat.LastObserverIndex = observerAddIndex;
            _latestStatsBufferOnObservedStat[observedStatHandle.Index] = observedStat;

            return true;
        }
    }
}