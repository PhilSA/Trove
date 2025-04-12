using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Trove.Stats
{
    public static class StatsUtilities
    {
        public static void BakeStatsComponents<TStatModifier, TStatModifierStack>(IBaker baker, Entity entity, out StatsBaker<TStatModifier, TStatModifierStack> statsBaker)
            where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
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
            where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            entityManager.AddComponentData(entity, new StatsOwner());
            entityManager.AddBuffer<Stat>(entity);
            entityManager.AddBuffer<TStatModifier>(entity);
            entityManager.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer ecb)
            where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(entity, new StatsOwner());
            ecb.AddBuffer<Stat>(entity);
            ecb.AddBuffer<TStatModifier>(entity);
            ecb.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer.ParallelWriter ecb, int sortKey)
            where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(sortKey, entity, new StatsOwner());
            ecb.AddBuffer<Stat>(sortKey, entity);
            ecb.AddBuffer<TStatModifier>(sortKey, entity);
            ecb.AddBuffer<StatObserver>(sortKey, entity);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AsInt(this float floatValue)
        {
            return UnsafeUtility.As<float, int>(ref floatValue);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AsFloat(this int intValue)
        {
            return UnsafeUtility.As<int, float>(ref intValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CreateStatCommon(
            Entity entity, 
            float baseValue, 
            bool produceChangeEvents,
            out Stat newStat,
            out StatHandle statHandle)
        {
            statHandle = new StatHandle
            {
                Entity = entity,
                Index = -1,
            };

            newStat = new Stat
            {
                BaseValue = baseValue,
                Value = baseValue,
                
                ModifiersCount = 0,
                ObserversCount = 0,
                
                ProduceChangeEvents = produceChangeEvents ? (byte)1 : (byte)0,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateStat(
            Entity entity, 
            float baseValue, 
            bool produceChangeEvents, 
            ref DynamicBuffer<Stat> statsBuffer,
            out StatHandle statHandle)
        {
            CreateStatCommon(entity, baseValue, produceChangeEvents, out Stat newStat, out statHandle);
            
            statHandle.Index = statsBuffer.Length;
            statsBuffer.Add(newStat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref StatsReader statsReader,
            ref Stat statRef,
            in DynamicBuffer<Stat> statsBuffer,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
            where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Stat nullStat = default;
            Stat initialStat = statRef;
            
            // Apply Modifiers
            statsWorldData._modifiersStack.Reset();
            if (statRef.ModifiersCount > 0)
            {
                int modifiersStartIndex = StatsUtilities.GetModifiersStartIndexForStat(in statsBuffer, statHandle.Index);
                Assert.IsTrue(modifiersStartIndex + statRef.ModifiersCount <= statModifiersBuffer.Length);

                for (int i = modifiersStartIndex; i < modifiersStartIndex + statRef.ModifiersCount; i++)
                {
                    ref TStatModifier modifierRef =
                        ref UnsafeUtility.ArrayElementAsRef<TStatModifier>(
                            statModifiersBuffer.GetUnsafePtr(), i);

                    // Modifier is applied by ref, so changes in the modifier struct done during Apply() are saved
                    modifierRef.Apply(
                        ref statsReader,
                        ref statsWorldData._modifiersStack,
                        out bool addModifierTriggerEvent);

                    // Handle modifier trigger events
                    if (addModifierTriggerEvent && statsWorldData.ModifierTriggerEventsList.IsCreated)
                    {
                        statsWorldData.ModifierTriggerEventsList.Add(
                            new ModifierTriggerEvent<TStatModifier, TStatModifierStack>
                            {
                                Modifier = modifierRef,
                                ModifierHandle = new StatModifierHandle
                                {
                                    ModifierID = modifierRef.Id,
                                    AffectedStatHandle = statHandle,
                                }
                            });
                    }
                }
            }

            statsWorldData._modifiersStack.Apply(in statRef.BaseValue, ref statRef.Value);

            // If the stat value really changed
            if (initialStat.Value != statRef.Value)
            {
                // Stat change events
                if (statRef.ProduceChangeEvents == 1 && statsWorldData.StatChangeEventsList.IsCreated)
                {
                    statsWorldData.StatChangeEventsList.Add(new StatChangeEvent
                    {
                        StatHandle = statHandle,
                        PrevValue = initialStat,
                        NewValue = statRef,
                    });
                }

                // Notify Observers (add to update list)
                if (statRef.ObserversCount > 0)
                {
                    int observersStartIndex = StatsUtilities.GetObserversStartIndexForStat(in statsBuffer, statHandle.Index);
                    Assert.IsTrue(observersStartIndex + statRef.ObserversCount <= statObserversBuffer.Length);
                    
                    for (int i = observersStartIndex; i < observersStartIndex + statRef.ObserversCount; i++)
                    {
                        StatObserver observer = statObserversBuffer[i];
                        if (observer.ObserverHandle.Entity == statHandle.Entity)
                        {
                            // Same-entity observers will be processed next, while we have all the buffers
                            statsWorldData._tmpSameEntityUpdatedStatsList.Add(observer.ObserverHandle);
                        }
                        else
                        {
                            // Other-entity observers will be processed later
                            statsWorldData._tmpGlobalUpdatedStatsList.Add(observer.ObserverHandle);
                        }
                    }
                }
            }
        }

        internal static void AddObserversOfStatToList(
            in StatHandle statHandle,
            in Stat stat,
            in DynamicBuffer<Stat> statsBuffer,
            in DynamicBuffer<StatObserver> statObserversBuffer,
            ref NativeList<StatObserver> statObserversList)
        {
            int observersStartIndex = StatsUtilities.GetObserversStartIndexForStat(in statsBuffer, statHandle.Index);
            Assert.IsTrue(observersStartIndex + stat.ObserversCount <= statObserversBuffer.Length);
            
            for (int i = observersStartIndex; i < observersStartIndex + stat.ObserversCount; i++)
            {
                statObserversList.Add(statObserversBuffer[i]);
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
                // IMPORTANT: observers must be sorted in affected stat order
                int observersEndIndex = StatsUtilities.GetObserversEndIndexForStat(in statsBufferOnObservedStat,
                    observedStatHandle.Index);
                statObserversBufferOnObservedStatEntity.Insert(observersEndIndex, new StatObserver { ObserverHandle = observerStatHandle });
                
                Stat observedStat = statsBufferOnObservedStat[observedStatHandle.Index];
                observedStat.ObserversCount++;
                statsBufferOnObservedStat[observedStatHandle.Index] = observedStat;
            }
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// Note: Assumes index is valid
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetStat(StatHandle statHandle, in DynamicBuffer<Stat> statsBuffer, out float value, out float baseValue)
        {
            Stat stat = statsBuffer[statHandle.Index];
            value = stat.Value;
            baseValue = stat.BaseValue;
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetStat(StatHandle statHandle, in DynamicBuffer<Stat> statsBuffer, out float value, out float baseValue)
        {
            if (statHandle.Index < statsBuffer.Length)
            {
                Stat stat = statsBuffer[statHandle.Index];
                value = stat.Value;
                baseValue = stat.BaseValue;
                return true;
            }

            value = default;
            baseValue = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetStat(StatHandle statHandle, in DynamicBuffer<Stat> statsBuffer, out Stat stat)
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
        internal static bool TryGetStat(StatHandle statHandle, in BufferLookup<Stat> statsBufferLookup, out Stat stat)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                return TryGetStat(statHandle, statsBuffer, out stat);
            }

            stat = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetStat(StatHandle statHandle, in BufferLookup<Stat> statsBufferLookup, out float value, out float baseValue)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                return TryGetStat(statHandle, in statsBuffer, out value, out baseValue);
            }

            value = default;
            baseValue = default;
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
        internal static unsafe ref Stat GetStatRefUnsafe(StatHandle statHandle, ref DynamicBuffer<Stat> statsBuffer, out bool success, ref Stat nullResult)
        {
            if (statHandle.Index < statsBuffer.Length)
            {
                success = true;
                return ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), statHandle.Index);
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ref Stat GetStatRefWithBufferUnsafe(StatHandle statHandle, ref BufferLookup<Stat> statsBufferLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref Stat nullResult)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out statsBuffer))
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
        public static bool TryGetModifiersCount(
            StatHandle statHandle, 
            ref BufferLookup<Stat> statsLookup, 
            out int modifiersCount)
        {
            modifiersCount = 0;
            
            if (statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    Stat stat = statsBuffer[statHandle.Index];
                    modifiersCount = stat.ModifiersCount;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Note: does not clear the supplied list
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetModifiersOfStat<TStatModifier, TStatModifierStack>(StatHandle statHandle,
            ref BufferLookup<Stat> statsLookup, 
            ref BufferLookup<TStatModifier> statModifiersLookup, 
            ref NativeList<StatModifierAndModifierHandle<TStatModifier, TStatModifierStack>> modifiers)
            where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer) &&
                statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    Stat stat = statsBuffer[statHandle.Index];

                    if (stat.ModifiersCount > 0)
                    {
                        int modifiersStartIndex =
                            StatsUtilities.GetModifiersStartIndexForStat(in statsBuffer, statHandle.Index);
                        Assert.IsTrue(modifiersStartIndex + stat.ModifiersCount <= statModifiersBuffer.Length);

                        for (int i = modifiersStartIndex; i < modifiersStartIndex + stat.ModifiersCount; i++)
                        {
                            TStatModifier modifier = statModifiersBuffer[i];
                            modifiers.Add(new StatModifierAndModifierHandle<TStatModifier, TStatModifierStack>()
                            {
                                Modifier = modifier,
                                ModifierHandle = new StatModifierHandle
                                {
                                    AffectedStatHandle = statHandle,
                                    ModifierID = modifier.Id,
                                }
                            });
                        }
                    }
                    
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Note: does not clear the supplied list
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObserversOfStat(StatHandle statHandle,
            ref BufferLookup<Stat> statsLookup, 
            ref BufferLookup<StatObserver> statObserversLookup, 
            ref NativeList<StatObserver> observers)
        {
            if (statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer) &&
                statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    Stat stat = statsBuffer[statHandle.Index];

                    if (stat.ObserversCount > 0)
                    {
                        int observersStartIndex =
                            StatsUtilities.GetObserversStartIndexForStat(in statsBuffer, statHandle.Index);
                        Assert.IsTrue(observersStartIndex + stat.ObserversCount <= statObserversBuffer.Length);

                        for (int i = observersStartIndex; i < observersStartIndex + stat.ObserversCount; i++)
                        {
                            observers.Add(statObserversBuffer[i]);
                        }
                    }
                    
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetObserversCount(
            StatHandle statHandle,
            ref BufferLookup<Stat> statsLookup, 
            out int observersCount)
        {
            observersCount = 0;
            
            if (statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    Stat stat = statsBuffer[statHandle.Index];
                    observersCount = stat.ObserversCount;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Note: does not clear the supplied list
        /// Note: useful to store observers before destroying an entity, and then manually update all observers after
        /// destroy. An observers update isn't automatically called when a stats entity is destroyed. (TODO:?) 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAllObservers(Entity entity,
            ref BufferLookup<StatObserver> statObserversLookup, 
            ref NativeList<StatObserver> observersList)
        {
            if (statObserversLookup.TryGetBuffer(entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                for (int i = 0; i < statObserversBuffer.Length; i++)
                {
                    observersList.Add(statObserversBuffer[i]);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if any entity other than the specified one depends on stats present on the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EntityHasAnyOtherDependantStatEntities(Entity entity, ref BufferLookup<StatObserver> statObserversLookup)
        {
            if (statObserversLookup.TryGetBuffer(entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                return EntityHasAnyOtherDependantStatEntities(entity, ref statObserversBuffer);
            }
            
            return false;
        }
        
        /// <summary>
        /// Returns true if any entity other than the specified one depends on stats present on the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EntityHasAnyOtherDependantStatEntities(Entity entity, ref DynamicBuffer<StatObserver> statObserversBufferOnEntity)
        {
            for (int i = 0; i < statObserversBufferOnEntity.Length; i++)
            {
                StatObserver observer = statObserversBufferOnEntity[i];
                if (observer.ObserverHandle.Entity != entity)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns all entities that have stats and depend on stats present on the specified entity.
        /// Excludes the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetOtherDependantStatsOfEntity(Entity entity, ref BufferLookup<StatObserver> statObserversLookup, ref NativeList<StatHandle> dependentStats)
        {
            if (statObserversLookup.TryGetBuffer(entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                GetOtherDependantStatsOfEntity(entity, ref statObserversBuffer, ref dependentStats);
            }
        }
        
        /// <summary>
        /// Returns all entities that have stats and depend on stats present on the specified entity.
        /// Excludes the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetOtherDependantStatsOfEntity(Entity entity, ref DynamicBuffer<StatObserver> statObserversBufferOnEntity, ref NativeList<StatHandle> dependentStats)
        {
            for (int i = 0; i < statObserversBufferOnEntity.Length; i++)
            {
                StatObserver observer = statObserversBufferOnEntity[i];
                if (observer.ObserverHandle.Entity != entity)
                {
                    dependentStats.Add(observer.ObserverHandle);
                }
            }
        }

        /// <summary>
        /// Returns all entities that have stats and depend on stats present on the specified entity.
        /// Excludes the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetOtherDependantStatEntitiesOfEntity(Entity entity, ref BufferLookup<StatObserver> statObserversLookup, ref NativeHashSet<Entity> dependentEntities)
        {
            if (statObserversLookup.TryGetBuffer(entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                GetOtherDependantStatEntitiesOfEntity(entity, ref statObserversBuffer, ref dependentEntities);
            }
        }
        
        /// <summary>
        /// Returns all entities that have stats and depend on stats present on the specified entity.
        /// Excludes the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetOtherDependantStatEntitiesOfEntity(Entity entity, ref DynamicBuffer<StatObserver> statObserversBufferOnEntity, ref NativeHashSet<Entity> dependentEntities)
        {
            for (int i = 0; i < statObserversBufferOnEntity.Length; i++)
            {
                StatObserver observer = statObserversBufferOnEntity[i];
                if (observer.ObserverHandle.Entity != entity)
                {
                    dependentEntities.Add(observer.ObserverHandle.Entity);
                }
            }
        }

        /// <summary>
        /// Returns all entities that have stats and depend on stats present on the specified entity.
        /// Excludes the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetStatEntitiesThatEntityDependsOn<TStatModifier, TStatModifierStack>(Entity entity, 
            ref BufferLookup<TStatModifier> statObserversLookup, 
            ref NativeHashSet<Entity> dependsOnEntities,
            ref NativeList<StatHandle> tmpObserverStatHandles)
            where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statObserversLookup.TryGetBuffer(entity, out DynamicBuffer<TStatModifier> statModifiersBuffer))
            {
                GetStatEntitiesThatEntityDependsOn<TStatModifier, TStatModifierStack>(entity, ref statModifiersBuffer, ref dependsOnEntities, ref tmpObserverStatHandles);
            }
        }
        
        /// <summary>
        /// Returns all entities that have stats and depend on stats present on the specified entity.
        /// Excludes the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetStatEntitiesThatEntityDependsOn<TStatModifier, TStatModifierStack>(Entity entity, 
            ref DynamicBuffer<TStatModifier> statModifiersBufferOnEntity, 
            ref NativeHashSet<Entity> dependsOnEntities,
            ref NativeList<StatHandle> tmpObserverStatHandles)
            where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            tmpObserverStatHandles.Clear();
            for (int i = 0; i < statModifiersBufferOnEntity.Length; i++)
            {
                TStatModifier modifier = statModifiersBufferOnEntity[i];
                modifier.AddObservedStatsToList(ref tmpObserverStatHandles);
            }

            for (int i = 0; i < tmpObserverStatHandles.Length; i++)
            {
                dependsOnEntities.Add(tmpObserverStatHandles[i].Entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetModifiersStartIndexForStat(in DynamicBuffer<Stat> statsBuffer, int statIndex)
        {
            int modifiersStartIndex = 0;
            for (int i = 0; i < statIndex; i++)
            {
                modifiersStartIndex += statsBuffer[i].ModifiersCount;
            }
            return modifiersStartIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetModifiersEndIndexForStat(in DynamicBuffer<Stat> statsBuffer, int statIndex)
        {
            return GetModifiersStartIndexForStat(in statsBuffer, statIndex) + statsBuffer[statIndex].ModifiersCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetObserversStartIndexForStat(in DynamicBuffer<Stat> statsBuffer, int statIndex)
        {
            int observersStartIndex = 0;
            for (int i = 0; i < statIndex; i++)
            {
                observersStartIndex += statsBuffer[i].ObserversCount;
            }
            return observersStartIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetObserversEndIndexForStat(in DynamicBuffer<Stat> statsBuffer, int statIndex)
        {
            return GetObserversStartIndexForStat(in statsBuffer, statIndex) + statsBuffer[statIndex].ObserversCount;
        }
    }
}