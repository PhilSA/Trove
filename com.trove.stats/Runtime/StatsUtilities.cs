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
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            baker.AddComponent(entity,new StatsOwner());
            statsBaker =  new StatsBaker<TStatModifier, TStatModifierStack>
            {
                Baker = baker,
                Entity = entity,

                StatsOwner = default,
                StatsBuffer = baker.AddBuffer<Stat>(entity),
                StatModifiersBuffer = baker.AddBuffer<StatModifier<TStatModifier, TStatModifierStack>>(entity),
                StatObserversBuffer = baker.AddBuffer<StatObserver>(entity),
            };
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityManager entityManager)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            entityManager.AddComponentData(entity, new StatsOwner());
            entityManager.AddBuffer<Stat>(entity);
            entityManager.AddBuffer<StatModifier<TStatModifier, TStatModifierStack>>(entity);
            entityManager.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer ecb)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(entity, new StatsOwner());
            ecb.AddBuffer<Stat>(entity);
            ecb.AddBuffer<StatModifier<TStatModifier, TStatModifierStack>>(entity);
            ecb.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer.ParallelWriter ecb, int sortKey)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(sortKey, entity, new StatsOwner());
            ecb.AddBuffer<Stat>(sortKey, entity);
            ecb.AddBuffer<StatModifier<TStatModifier, TStatModifierStack>>(sortKey, entity);
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
                
                ModifiersList = CompactLinkedSubList.Create(),
                ObserversList = CompactLinkedSubList.Create(),
                
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
        internal static void UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref StatsReader statsReader,
            ref Stat statRef,
            ref DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Stat nullStat = default;
            Stat initialStat = statRef;
            
            // Apply Modifiers
            statsWorldData._modifiersStack.Reset();
            if (statRef.ModifiersList.Length > 0)
            {
                CompactLinkedSubList.Iterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                    CompactLinkedSubList.GetIterator<StatModifier<TStatModifier, TStatModifierStack>>(
                        statRef.ModifiersList);
                ref StatModifier<TStatModifier, TStatModifierStack> modifierRef = ref modifiersIterator.GetNextRef(
                    ref statModifiersBuffer, out bool success, out int modifierIndex);
                while (success)
                {
                    // Modifier is applied by ref, so changes in the modifier struct done during Apply() are saved
                    modifierRef.Modifier.Apply(
                        ref statsReader,
                        ref statsWorldData._modifiersStack,
                        out bool addModifierTriggerEvent);

                    // Handle modifier trigger events
                    if (addModifierTriggerEvent && statsWorldData.ModifierTriggerEventsList.IsCreated)
                    {
                        statsWorldData.ModifierTriggerEventsList.Add(new ModifierTriggerEvent<TStatModifier, TStatModifierStack>
                        {
                            Modifier = modifierRef.Modifier,
                            ModifierHandle = new StatModifierHandle
                            {
                                ModifierID = modifierRef.ID,
                                AffectedStatHandle = statHandle,
                            }
                        });
                    }

                    modifierRef = ref modifiersIterator.GetNextRef(
                        ref statModifiersBuffer, out success, out modifierIndex);
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
                if (statRef.ObserversList.Length > 0)
                {
                    CompactLinkedSubList.Iterator<StatObserver> observersIterator =
                        CompactLinkedSubList.GetIterator<StatObserver>(statRef.ObserversList);
                    while (observersIterator.GetNext(in statObserversBuffer, out StatObserver observer,
                               out int observerIndex))
                    {
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
            in Stat stat,
            in DynamicBuffer<StatObserver> statObserversBufferOnStatEntity,
            ref NativeList<StatObserver> statObserversList)
        {
            if (stat.ObserversList.Length > 0)
            {
                CompactLinkedSubList.Iterator<StatObserver> observersIterator =
                    CompactLinkedSubList.GetIterator<StatObserver>(stat.ObserversList);
                while (observersIterator.GetNext(in statObserversBufferOnStatEntity,
                           out StatObserver observerOfStat, out int observerIndex))
                {
                    statObserversList.Add(observerOfStat);
                }
            }
        }

        internal static unsafe void AddStatAsObserverOfOtherStat(
            StatHandle observerStatHandle, 
            StatHandle observedStatHandle,
            ref DynamicBuffer<Stat> statsBufferOnObservedStat,
            ref DynamicBuffer<StatObserver> statObserversBufferOnObservedStatEntity)
        {
            Assert.IsTrue(observerStatHandle.Entity != Entity.Null);

            if (observedStatHandle.Index < statsBufferOnObservedStat.Length)
            {
                Stat observedStat = statsBufferOnObservedStat[observedStatHandle.Index];
                
                CompactLinkedSubList.Add(ref observedStat.ObserversList,
                    ref statObserversBufferOnObservedStatEntity,
                    new StatObserver { ObserverHandle = observerStatHandle });
                
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
                    modifiersCount = stat.ModifiersList.Length;
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
            ref BufferLookup<StatModifier<TStatModifier, TStatModifierStack>> statModifiersLookup, 
            ref NativeList<StatModifierAndModifierHandle<TStatModifier, TStatModifierStack>> modifiers)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer) &&
                statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    Stat stat = statsBuffer[statHandle.Index];

                    CompactLinkedSubList.Iterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                        CompactLinkedSubList.GetIterator<StatModifier<TStatModifier, TStatModifierStack>>(stat.ModifiersList);
                    while (modifiersIterator.GetNext(in statModifiersBuffer, out StatModifier<TStatModifier, TStatModifierStack> modifier, out int modifierIndex))
                    {
                        modifiers.Add(new StatModifierAndModifierHandle<TStatModifier, TStatModifierStack>()
                        {
                            Modifier = modifier.Modifier,
                            ModifierHandle = new StatModifierHandle
                            {
                                AffectedStatHandle = statHandle,
                                ModifierID = modifier.ID,
                            }
                        });
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
                    observersCount = stat.ObserversList.Length;
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
            ref BufferLookup<StatModifier<TStatModifier, TStatModifierStack>> statObserversLookup, 
            ref NativeHashSet<Entity> dependsOnEntities,
            ref NativeList<StatHandle> tmpObserverStatHandles)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            if (statObserversLookup.TryGetBuffer(entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer))
            {
                GetStatEntitiesThatEntityDependsOn(entity, ref statModifiersBuffer, ref dependsOnEntities, ref tmpObserverStatHandles);
            }
        }
        
        /// <summary>
        /// Returns all entities that have stats and depend on stats present on the specified entity.
        /// Excludes the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetStatEntitiesThatEntityDependsOn<TStatModifier, TStatModifierStack>(Entity entity, 
            ref DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBufferOnEntity, 
            ref NativeHashSet<Entity> dependsOnEntities,
            ref NativeList<StatHandle> tmpObserverStatHandles)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            tmpObserverStatHandles.Clear();
            for (int i = 0; i < statModifiersBufferOnEntity.Length; i++)
            {
                StatModifier<TStatModifier, TStatModifierStack> modifier = statModifiersBufferOnEntity[i];
                modifier.Modifier.AddObservedStatsToList(ref tmpObserverStatHandles);
            }

            for (int i = 0; i < tmpObserverStatHandles.Length; i++)
            {
                dependsOnEntities.Add(tmpObserverStatHandles[i].Entity);
            }
        }
    }
}