using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Assertions;

namespace Trove.Stats
{
    public struct StatsWorld<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        private ComponentLookup<StatsOwner> _statsOwnerLookup;
        private BufferLookup<Stat> _statsLookup;
        private BufferLookup<TStatModifier> _statModifiersLookup;
        private BufferLookup<StatObserver> _statObserversLookup;

        [NativeDisableContainerSafetyRestriction]
        private NativeList<StatHandle> _tmpModifierObservedStatsList;
        [NativeDisableContainerSafetyRestriction]
        private NativeList<StatObserver> _tmpStatObserversList;
        [NativeDisableContainerSafetyRestriction]
        private NativeList<StatHandle> _tmpUpdatedStatsList;
        [NativeDisableContainerSafetyRestriction]
        private NativeList<int> _tmpLastIndexesList;

        [NativeDisableContainerSafetyRestriction] // TODO: I may not want disabled safeties for this one
        private NativeList<StatChangeEvent> _statChangeEventsList;
        public NativeList<StatChangeEvent> StatChangeEventsList
        {
            get { return _statChangeEventsList; }
            set { _statChangeEventsList = value; }
        }

        private Stat _nullStat;

        public StatsWorld(ref SystemState state)
        {
            _statsOwnerLookup = state.GetComponentLookup<StatsOwner>(false);
            _statsLookup = state.GetBufferLookup<Stat>(false);
            _statModifiersLookup = state.GetBufferLookup<TStatModifier>(false);
            _statObserversLookup = state.GetBufferLookup<StatObserver>(false);

            _tmpModifierObservedStatsList = default;
            _tmpStatObserversList = default;
            _tmpUpdatedStatsList = default;
            _tmpLastIndexesList = default;
            
            _statChangeEventsList = default;
            
            _nullStat = default;
        }

        public void OnUpdate(ref SystemState state)
        {
            _statsOwnerLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _statModifiersLookup.Update(ref state);
            _statObserversLookup.Update(ref state);

            _tmpModifierObservedStatsList = default;
            _tmpStatObserversList = default;
            _tmpUpdatedStatsList = default;
            _tmpLastIndexesList = default;
            
            _nullStat = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out Stat stat)
        {
            if (_statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    stat = statsBuffer[statHandle.Index];
                    return true;
                }
            }

            stat = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetStatAndBuffer(StatHandle statHandle, out Stat stat, out DynamicBuffer<Stat> statsBuffer)
        {
            if (_statsLookup.TryGetBuffer(statHandle.Entity, out statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    stat = statsBuffer[statHandle.Index];
                    return true;
                }
            }

            statsBuffer = default;
            stat = default;
            return false;
        }
        
        /// <summary>
        /// Private because we always have to manually call UpdateStat after modification
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe ref Stat TryGetStatRefUnsafe(StatHandle statHandle, out bool success, ref Stat failResult)
        {
            if (_statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), statHandle.Index);
                }
            }

            success = false;
            return ref failResult;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatBaseValue(StatHandle statHandle, float baseValue)
        {
            ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue = baseValue;
                UpdateStatRef(statHandle, ref statRef);
                return true;
            }

            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValue(StatHandle statHandle, float baseValueAdd)
        {
            ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue += baseValueAdd;
                UpdateStatRef(statHandle, ref statRef);
                return true;
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValue(StatHandle statHandle, float baseValueMul)
        {
            ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue *= baseValueMul;
                UpdateStatRef(statHandle, ref statRef);
                return true;
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatProduceChangeEvents(StatHandle statHandle, bool value)
        {
            ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool success, ref _nullStat);
            if (success)
            {
                statRef.ProduceChangeEvents = value ? (byte)1 : (byte)0;
                return true;
            }
            
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StatExists(StatHandle statHandle)
        {
            if (_statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Note: if the stat doesn't exist, it just does nothing (no error).
        /// </summary>
        /// <param name="statHandle"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryUpdateStat(StatHandle statHandle)
        {
            StatsUtilities.EnsureClearedValidTempList(ref _tmpUpdatedStatsList);
            _tmpUpdatedStatsList.Add(statHandle);
        
            StatValueReader statValueReader = new StatValueReader(_statsLookup);

            DynamicBuffer<TStatModifier> statModifiersBuffer = default;
            DynamicBuffer<StatObserver> statObserversBuffer = default;
            for (int i = 0; i < _tmpUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = _tmpUpdatedStatsList[i];
                
                ref Stat statRef = ref TryGetStatRefUnsafe(newStatHandle, out bool getStatSuccess, ref _nullStat);
                if (getStatSuccess)
                {
                    if (statRef.LastModifierIndex >= 0)
                    {
                        bool success = _statModifiersLookup.TryGetBuffer(newStatHandle.Entity,
                            out statModifiersBuffer);
                        Assert.IsTrue(success);
                    }
                    if (statRef.LastObserverIndex >= 0)
                    {
                        bool success = _statObserversLookup.TryGetBuffer(newStatHandle.Entity, out statObserversBuffer);
                        Assert.IsTrue(success);
                    }
                    
                    StatsUtilities.UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
                        newStatHandle, 
                        ref statValueReader,
                        ref statRef, 
                        ref statModifiersBuffer, 
                        ref statObserversBuffer, 
                        ref _statChangeEventsList,
                        ref _tmpUpdatedStatsList);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStatRef(StatHandle statHandle, ref Stat initialStatRef)
        {
            StatsUtilities.EnsureClearedValidTempList(ref _tmpUpdatedStatsList);
            _tmpUpdatedStatsList.Add(statHandle);
            
            StatValueReader statValueReader = new StatValueReader(_statsLookup);

            DynamicBuffer<TStatModifier> initialStatModifiersBuffer = default;
            DynamicBuffer<StatObserver> initialStatObserversBuffer = default;
            if (initialStatRef.LastModifierIndex >= 0)
            {
                bool success = _statModifiersLookup.TryGetBuffer(statHandle.Entity,
                    out initialStatModifiersBuffer);
                Assert.IsTrue(success);
            }
            if (initialStatRef.LastObserverIndex >= 0)
            {
                bool success = _statObserversLookup.TryGetBuffer(statHandle.Entity, out initialStatObserversBuffer);
                Assert.IsTrue(success);
            }
            
            // First update the current stat ref
            StatsUtilities.UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
                statHandle, 
                ref statValueReader,
                ref initialStatRef, 
                ref initialStatModifiersBuffer, 
                ref initialStatObserversBuffer, 
                ref _statChangeEventsList,
                ref _tmpUpdatedStatsList);
            
            // Then update following stats
            DynamicBuffer<TStatModifier> statModifiersBuffer = default;
            DynamicBuffer<StatObserver> statObserversBuffer = default;
            for (int i = 0; i < _tmpUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = _tmpUpdatedStatsList[i];
                
                ref Stat statRef = ref TryGetStatRefUnsafe(newStatHandle, out bool getStatSuccess, ref _nullStat);
                if (getStatSuccess)
                {
                    if (statRef.LastModifierIndex >= 0)
                    {
                        bool success = _statModifiersLookup.TryGetBuffer(newStatHandle.Entity,
                            out statModifiersBuffer);
                        Assert.IsTrue(success);
                    }
                    if (statRef.LastObserverIndex >= 0)
                    {
                        bool success = _statObserversLookup.TryGetBuffer(newStatHandle.Entity, out statObserversBuffer);
                        Assert.IsTrue(success);
                    }
                    
                    StatsUtilities.UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
                        newStatHandle, 
                        ref statValueReader,
                        ref statRef, 
                        ref statModifiersBuffer, 
                        ref statObserversBuffer, 
                        ref _statChangeEventsList,
                        ref _tmpUpdatedStatsList);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCreateStat(Entity entity, float baseValue, bool produceChangeEvents, out StatHandle statHandle)
        {
            if (_statsLookup.TryGetBuffer(entity, out DynamicBuffer<Stat> statsBuffer))
            {
                StatsUtilities.CreateStatCommon(entity, baseValue, produceChangeEvents, out statHandle,
                    ref statsBuffer);
                return true;
            }

            statHandle = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatModifier(StatHandle affectedStatHandle, TStatModifier modifier, out StatModifierHandle statModifierHandle)
        {
            if (TryGetStatAndBuffer(affectedStatHandle, out Stat stat, out DynamicBuffer<Stat> statsBufferOnAffectedStatEntity) &&
                _statModifiersLookup.TryGetBuffer(affectedStatHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBufferOnAffectedStatEntity) &&
                _statObserversLookup.TryGetBuffer(affectedStatHandle.Entity, out DynamicBuffer<StatObserver> statObserversBufferOnAffectedStatEntity))
            {
                // Ensure lists are created and cleared
                StatsUtilities.EnsureClearedValidTempList(ref _tmpModifierObservedStatsList);
                StatsUtilities.EnsureClearedValidTempList(ref _tmpStatObserversList);
                
                ref StatsOwner statsOwnerRef = ref _statsOwnerLookup.GetRefRW(affectedStatHandle.Entity).ValueRW;

                StatsUtilities.AddModifierPhase1<TStatModifier, TStatModifierStack>(
                    affectedStatHandle,
                    ref statsOwnerRef,
                    ref modifier,
                    ref _tmpModifierObservedStatsList,
                    out statModifierHandle);
                
                bool modifierAdded = StatsUtilities.AddModifierPhase2<TStatModifier, TStatModifierStack>(
                    false,
                    affectedStatHandle,
                    in modifier,
                    ref statsBufferOnAffectedStatEntity,
                    ref statModifiersBufferOnAffectedStatEntity,
                    ref statObserversBufferOnAffectedStatEntity,
                    ref _statsLookup,
                    ref _statObserversLookup,
                    ref _tmpModifierObservedStatsList,
                    ref _tmpStatObserversList);

                if (modifierAdded)
                {
                    // Update stat following modifier add
                    ref Stat statRef = ref TryGetStatRefUnsafe(affectedStatHandle, out bool success, ref _nullStat);
                    UpdateStatRef(affectedStatHandle, ref statRef);
                    
                    return true;
                }
            }

            statModifierHandle = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStatModifier(StatModifierHandle modifierHandle, out TStatModifier statModifier)
        {
            if (_statModifiersLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<TStatModifier> statModifiersBuffer))
            {
                for (int i = 0; i < statModifiersBuffer.Length; i++)
                {
                    TStatModifier modifier = statModifiersBuffer[i];
                    if (modifier.ID == modifierHandle.ModifierID)
                    {
                        statModifier = modifier;
                        return true;
                    }
                }
            }

            statModifier = default;
            return false;
        }

        // TODO: make sure to review this whole thing
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryRemoveStatModifier(StatModifierHandle modifierHandle)
        {
            if (TryGetStatAndBuffer(modifierHandle.AffectedStatHandle, out Stat stat, out DynamicBuffer<Stat> statsBuffer) && 
                _statModifiersLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                _statObserversLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<StatObserver> statObserversBuffer))
            {       
                CompactMultiLinkedListIterator<TStatModifier> modifiersIterator =
                    new CompactMultiLinkedListIterator<TStatModifier>(stat.LastModifierIndex);
                while (modifiersIterator.GetNext(in statModifiersBuffer, out TStatModifier modifier, out int modifierIndex))
                {
                    if (modifier.ID == modifierHandle.ModifierID)
                    {
                        StatsUtilities.EnsureClearedValidTempList(ref _tmpLastIndexesList);
                        
                        // Remove modifier
                        // NOTE: must be done before observers remove, since observers removal changes the cached
                        // buffers and las index arrays
                        {
                            // Build the last indexes array
                            _tmpLastIndexesList.Resize(statsBuffer.Length, NativeArrayOptions.ClearMemory);
                            NativeArray<int> lastModifierIndexesArray = _tmpLastIndexesList.AsArray();
                            for (int i = 0; i < statsBuffer.Length; i++)
                            {
                                lastModifierIndexesArray[i] = statsBuffer[i].LastModifierIndex;
                            }
                            
                            modifiersIterator.RemoveCurrentIteratedElementAndUpdateIndexes(
                                ref statModifiersBuffer,
                                ref lastModifierIndexesArray, 
                                out int firstUpdatedLastIndexIndex);

                            // Write back updated last indexes
                            for (int i = firstUpdatedLastIndexIndex; i < statsBuffer.Length; i++)
                            {
                                ref Stat tmpStatRef =
                                    ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), i);
                                tmpStatRef.LastModifierIndex = lastModifierIndexesArray[i];
                            }
                        }

                        // Remove the modifier's affected stat as an observer of modifier observed stats
                        {
                            StatsUtilities.EnsureClearedValidTempList(ref _tmpModifierObservedStatsList);
                            modifier.AddObservedStatsToList(ref _tmpModifierObservedStatsList);

                            for (int a = 0; a < _tmpModifierObservedStatsList.Length; a++)
                            {
                                StatHandle observedStatHandle = _tmpModifierObservedStatsList[a];

                                if (TryGetStatAndBuffer(observedStatHandle, out Stat observedStat,
                                        out statsBuffer) &&
                                    _statModifiersLookup.TryGetBuffer(observedStatHandle.Entity,
                                        out statModifiersBuffer) &&
                                    _statObserversLookup.TryGetBuffer(observedStatHandle.Entity,
                                        out statObserversBuffer))
                                {
                                    // Build the last indexes array
                                    _tmpLastIndexesList.Resize(statsBuffer.Length, NativeArrayOptions.ClearMemory);
                                    NativeArray<int> lastObserverIndexesArray = _tmpLastIndexesList.AsArray();
                                    for (int i = 0; i < statsBuffer.Length; i++)
                                    {
                                        lastObserverIndexesArray[i] = statsBuffer[i].LastObserverIndex;
                                    }

                                    int firstUpdatedLastIndexIndex = int.MaxValue;

                                    // Iterate observers of the observed stat and try to remove the affected stat
                                    CompactMultiLinkedListIterator<StatObserver> observersIterator =
                                        new CompactMultiLinkedListIterator<StatObserver>(observedStat
                                            .LastObserverIndex);
                                    while (observersIterator.GetNext(in statObserversBuffer,
                                               out StatObserver observerOfObservedStat, out int observerIndex))
                                    {
                                        if (observerOfObservedStat.ObserverHandle == modifierHandle.AffectedStatHandle)
                                        {
                                            observersIterator.RemoveCurrentIteratedElementAndUpdateIndexes(
                                                ref statObserversBuffer,
                                                ref lastObserverIndexesArray,
                                                out int tmpFirstUpdatedLastIndexIndex);

                                            // Remember the lowest valid updated last index index
                                            if (tmpFirstUpdatedLastIndexIndex >= 0)
                                            {
                                                firstUpdatedLastIndexIndex = math.min(firstUpdatedLastIndexIndex, tmpFirstUpdatedLastIndexIndex);
                                            }

                                            // Break so we don't remove all observer instances of this stat
                                            break;
                                        }
                                    }

                                    // Write back updated last indexes
                                    if (firstUpdatedLastIndexIndex >= 0)
                                    {
                                        for (int i = firstUpdatedLastIndexIndex; i < statsBuffer.Length; i++)
                                        {
                                            ref Stat tmpStatRef =
                                                ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(),
                                                    i);
                                            tmpStatRef.LastObserverIndex = lastObserverIndexesArray[i];
                                        }
                                    }
                                }
                            }
                        }

                        // Stat update following modifier remove
                        TryUpdateStat(modifierHandle.AffectedStatHandle);

                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAllStatModifiersOfStat(StatHandle statHandle)
        {
            ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool getStatSuccess, ref _nullStat);
            if (getStatSuccess &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer))
            {
                // Note: at each removal, the LastModifierIndex is updated
                while (statRef.LastModifierIndex >= 0)
                {
                    TStatModifier modifier = statModifiersBuffer[statRef.LastModifierIndex];

                    TryRemoveStatModifier(new StatModifierHandle
                    {
                        ModifierID = modifier.ID,
                        AffectedStatHandle = statHandle,
                    });
                }
            }
        }

        /// <summary>
        /// Returns true if any entity other than the specified one depends on stats present on the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EntityHasAnyOtherDependantStatEntities(Entity entity)
        {
            // TODO
            return false;
        }

        /// <summary>
        /// Returns all entities that have stats and depend on stats present on the specified entity.
        /// Excludes the specified entity.
        /// Useful for netcode relevancy
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetOtherDependantStatEntitiesOfEntity(Entity entity, ref NativeHashSet<Entity> dependentEntities)
        {
            // TODO
            // TODO: remember to exclude specified entity
        }
    }
}