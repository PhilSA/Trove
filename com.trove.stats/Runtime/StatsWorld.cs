using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Assertions;
using UnityEngine;

namespace Trove.Stats
{
    public struct StatsWorld<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        internal ComponentLookup<StatsOwner> _statsOwnerLookup;
        internal BufferLookup<Stat> _statsLookup;
        internal BufferLookup<StatModifier<TStatModifier, TStatModifierStack>> _statModifiersLookup;
        internal BufferLookup<StatObserver> _statObserversLookup;
        
        // TODO: review what happens with these lists if created during main thread stat operations, but then StatsWorld is passed to job
        private UnsafeList<StatHandle> _tmpModifierObservedStatsList;
        private UnsafeList<StatObserver> _tmpStatObserversList;
        private UnsafeList<StatHandle> _tmpUpdatedStatsList;
        private UnsafeList<int> _tmpLastIndexesList;

        public bool SupportStatChangeEvents { get; set; }
        private NativeList<StatChangeEvent> _statChangeEventsList;
        public NativeList<StatChangeEvent> StatChangeEventsList
        {
            get { return _statChangeEventsList; }
            set { _statChangeEventsList = value; }
        }

        public bool SupportModifierTriggerEvents { get; set; }
        private NativeList<StatModifierHandle> _modifierTriggerEventsList;
        public NativeList<StatModifierHandle> ModifierTriggerEventsList
        {
            get { return _modifierTriggerEventsList; }
            set { _modifierTriggerEventsList = value; }
        }

        private Stat _nullStat;

        public StatsWorld(ref SystemState state)
        {
            _statsOwnerLookup = state.GetComponentLookup<StatsOwner>(false);
            _statsLookup = state.GetBufferLookup<Stat>(false);
            _statModifiersLookup = state.GetBufferLookup<StatModifier<TStatModifier, TStatModifierStack>>(false);
            _statObserversLookup = state.GetBufferLookup<StatObserver>(false);

            _tmpModifierObservedStatsList = default;
            _tmpStatObserversList = default;
            _tmpUpdatedStatsList = default;
            _tmpLastIndexesList = default;
 
            SupportStatChangeEvents = false;
            _statChangeEventsList = default;
            SupportModifierTriggerEvents = false;
            _modifierTriggerEventsList = default;
             
            _nullStat = default;
        }

        public void UpdateDataAndLookups(ref SystemState state)
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
        public bool TryCalculateModifiersCount(StatHandle statHandle, out int modifiersCount)
        {
            modifiersCount = 0;
            
            if (_statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer) &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    Stat stat = statsBuffer[statHandle.Index];
                    
                    CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                        new CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>>(stat.LastModifierIndex);
                    while (modifiersIterator.GetNext(in statModifiersBuffer, out _, out _))
                    {
                        modifiersCount++;
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
        public bool TryGetModifiersOfStat(StatHandle statHandle, ref NativeList<StatModifierAndHandle<TStatModifier, TStatModifierStack>> modifiers)
        {
            if (_statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer) &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    Stat stat = statsBuffer[statHandle.Index];
                    
                    CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                        new CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>>(stat.LastModifierIndex);
                    while (modifiersIterator.GetNext(in statModifiersBuffer, out StatModifier<TStatModifier, TStatModifierStack> modifier, out int modifierIndex))
                    {
                        modifiers.Add(new StatModifierAndHandle<TStatModifier, TStatModifierStack>()
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
        public bool TryCalculateObserversCount(StatHandle statHandle, out int observersCount)
        {
            observersCount = 0;
            
            if (_statsLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer) &&
                (_statObserversLookup).TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    Stat stat = statsBuffer[statHandle.Index];
                    
                    CompactMultiLinkedListIterator<StatObserver> observersIterator =
                        new CompactMultiLinkedListIterator<StatObserver>(stat.LastObserverIndex);
                    while (observersIterator.GetNext(in statObserversBuffer, out StatObserver observer, out int observerIndex))
                    {
                        observersCount++;
                    }

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
        public bool TryGetAllObservers(Entity entity, ref NativeList<StatObserver> observersList)
        {
            if (_statObserversLookup.TryGetBuffer(entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                for (int i = 0; i < statObserversBuffer.Length; i++)
                {
                    observersList.Add(statObserversBuffer[i]);
                }

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCreateStat(Entity entity, float baseValue, bool produceChangeEvents, out StatHandle statHandle)
        {
            if (_statsLookup.TryGetBuffer(entity, out DynamicBuffer<Stat> statsBuffer))
            {
                StatsUtilities.CreateStat(
                    entity,
                    baseValue,
                    produceChangeEvents,
                    ref statsBuffer,
                    out statHandle);
                return true;
            }

            statHandle = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out float value, out float baseValue)
        {
            return StatsUtilities.TryGetStat(statHandle, ref _statsLookup, out value, out baseValue);
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, ref DynamicBuffer<Stat> statsBuffer, out float value, out float baseValue)
        {
            return StatsUtilities.TryGetStat(statHandle, ref statsBuffer, out value, out baseValue);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatBaseValue(StatHandle statHandle, float baseValue)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue = baseValue;
                UpdateStatRef(statHandle, ref statRef);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatBaseValue(StatHandle statHandle, float baseValue, ref DynamicBuffer<Stat> statsBuffer)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
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
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue += baseValueAdd;
                UpdateStatRef(statHandle, ref statRef);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValue(StatHandle statHandle, float baseValueAdd, ref DynamicBuffer<Stat> statsBuffer)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
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
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue *= baseValueMul;
                UpdateStatRef(statHandle, ref statRef);
                return true;
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValue(StatHandle statHandle, float baseValueMul, ref DynamicBuffer<Stat> statsBuffer)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
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
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.ProduceChangeEvents = value ? (byte)1 : (byte)0;
                return true;
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatProduceChangeEvents(StatHandle statHandle, bool value, ref DynamicBuffer<Stat> statsBuffer)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success)
            {
                statRef.ProduceChangeEvents = value ? (byte)1 : (byte)0;
                return true;
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
            StatsReader statsReader = new StatsReader(in _statsLookup);
              
            StatsUtilities.EnsureClearedValidTempList(ref _tmpUpdatedStatsList);
            _tmpUpdatedStatsList.AddWithGrowFactor(statHandle);
        
            DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer = default;
            DynamicBuffer<StatObserver> statObserversBuffer = default;
            for (int i = 0; i < _tmpUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = _tmpUpdatedStatsList[i];
                
                ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(newStatHandle, ref _statsLookup, out bool getStatSuccess, ref _nullStat);
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
                    
                    StatsUtilities.UpdateSingleStatCommon(
                        newStatHandle, 
                        ref statsReader,
                        ref statRef, 
                        ref statModifiersBuffer, 
                        ref statObserversBuffer, 
                        ref _statChangeEventsList,
                        ref _tmpUpdatedStatsList,
                        ref _modifierTriggerEventsList,
                        SupportStatChangeEvents,
                        SupportModifierTriggerEvents);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStatRef(StatHandle statHandle, ref Stat initialStatRef)
        {
            StatsReader statsReader = new StatsReader(in _statsLookup);
            
            StatsUtilities.EnsureClearedValidTempList(ref _tmpUpdatedStatsList);
            
            DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> initialStatModifiersBuffer = default;
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
            StatsUtilities.UpdateSingleStatCommon(
                statHandle, 
                ref statsReader,
                ref initialStatRef, 
                ref initialStatModifiersBuffer, 
                ref initialStatObserversBuffer, 
                ref _statChangeEventsList,
                ref _tmpUpdatedStatsList,
                ref _modifierTriggerEventsList,
                SupportStatChangeEvents,
                SupportModifierTriggerEvents);
            
            // Then update following stats
            DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer = default;
            DynamicBuffer<StatObserver> statObserversBuffer = default;
            for (int i = 0; i < _tmpUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = _tmpUpdatedStatsList[i];
                
                ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(newStatHandle, ref _statsLookup, out bool getStatSuccess, ref _nullStat);
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
                    
                    StatsUtilities.UpdateSingleStatCommon(
                        newStatHandle, 
                        ref statsReader,
                        ref statRef, 
                        ref statModifiersBuffer, 
                        ref statObserversBuffer, 
                        ref _statChangeEventsList,
                        ref _tmpUpdatedStatsList,
                        ref _modifierTriggerEventsList,
                        SupportStatChangeEvents,
                        SupportModifierTriggerEvents);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatModifier(StatHandle affectedStatHandle, TStatModifier modifier, out StatModifierHandle statModifierHandle)
        {
            ref Stat affectedStatRef = ref StatsUtilities.GetStatRefUnsafe(affectedStatHandle, ref _statsLookup, out bool getStatSuccess, ref _nullStat);
            if (getStatSuccess &&
                _statModifiersLookup.TryGetBuffer(affectedStatHandle.Entity,
                    out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>>
                        statModifiersBufferOnAffectedStatEntity) &&
                _statObserversLookup.TryGetBuffer(affectedStatHandle.Entity,
                    out DynamicBuffer<StatObserver> statObserversBufferOnAffectedStatEntity))
            {
                // Ensure lists are created and cleared
                StatsUtilities.EnsureClearedValidTempList(ref _tmpModifierObservedStatsList);
                StatsUtilities.EnsureClearedValidTempList(ref _tmpStatObserversList);

                statModifierHandle = new StatModifierHandle
                {
                    AffectedStatHandle = affectedStatHandle,
                };

                StatModifier<TStatModifier, TStatModifierStack> modifierElement =
                    new StatModifier<TStatModifier, TStatModifierStack>();
                modifierElement.Modifier = modifier;

                // Increment modifier Id (local to entity)
                ref StatsOwner affectStatsOwnerRef = ref _statsOwnerLookup.GetRefRW(affectedStatHandle.Entity).ValueRW;
                affectStatsOwnerRef.ModifierIDCounter++;
                modifierElement.ID = affectStatsOwnerRef.ModifierIDCounter;
                statModifierHandle.ModifierID = modifierElement.ID;

                // Get observed stats of modifier
                modifier.AddObservedStatsToList(ref _tmpModifierObservedStatsList);

                bool modifierCanBeAdded = true;
                {
                    // Make sure the modifier wouldn't make the stat observe itself (would cause infinite loop)
                    for (int j = 0; j < _tmpModifierObservedStatsList.Length; j++)
                    {
                        StatHandle modifierObservedStatHandle = _tmpModifierObservedStatsList[j];
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
                            in affectedStatRef,
                            in statObserversBufferOnAffectedStatEntity,
                            ref _tmpStatObserversList);

                        // TODO: make sure this verification loop can't possibly end up being infinite either. It could be infinite if we haven't guaranteed loop detection for other modifier adds...
                        for (int i = 0; i < _tmpStatObserversList.Length; i++)
                        {
                            StatHandle iteratedObserverStatHandle = _tmpStatObserversList[i].ObserverHandle;

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
                            for (int j = 0; j < _tmpModifierObservedStatsList.Length; j++)
                            {
                                StatHandle modifierObservedStatHandle = _tmpModifierObservedStatsList[j];
                                if (iteratedObserverStatHandle == modifierObservedStatHandle)
                                {
                                    _tmpStatObserversList.AddWithGrowFactor(new StatObserver
                                    {
                                        ObserverHandle = affectedStatHandle,
                                    });
                                }
                            }

                            // Update buffers so they represent the ones on the observer entity
                            if (StatsUtilities.TryGetStat(iteratedObserverStatHandle, ref _statsLookup,
                                    out Stat observerStat) &&
                                _statObserversLookup.TryGetBuffer(iteratedObserverStatHandle.Entity,
                                    out DynamicBuffer<StatObserver> observerStatObserversBuffer))
                            {
                                StatsUtilities.AddObserversOfStatToList(
                                    in observerStat,
                                    in observerStatObserversBuffer,
                                    ref _tmpStatObserversList);
                            }
                        }
                    }
                }

                if (modifierCanBeAdded)
                {
                    // Add modifier
                    {
                        CollectionUtilities.AddToCompactMultiLinkedList(ref statModifiersBufferOnAffectedStatEntity,
                            ref affectedStatRef.LastModifierIndex, modifierElement);
                    }

                    // Add affected stat as observer of all observed stats
                    for (int i = 0; i < _tmpModifierObservedStatsList.Length; i++)
                    {
                        StatHandle observedStatHandle = _tmpModifierObservedStatsList[i];

                        // Update buffers so they represent the ones on the observer entity
                        if (_statsLookup.TryGetBuffer(observedStatHandle.Entity,
                                out DynamicBuffer<Stat> observedStatsBuffer) &&
                            _statObserversLookup.TryGetBuffer(observedStatHandle.Entity,
                                out DynamicBuffer<StatObserver> observedStatObserversBuffer))
                        {
                            StatsUtilities.AddStatAsObserverOfOtherStat(
                                affectedStatHandle,
                                observedStatHandle,
                                ref observedStatsBuffer,
                                ref observedStatObserversBuffer);
                        }
                    }

                    // Update stat following modifier add
                    TryUpdateStat(affectedStatHandle);
                    return true;
                }
            }

            statModifierHandle = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStatModifier(StatModifierHandle modifierHandle, out TStatModifier statModifier)
        {
            if (_statsLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<Stat> statsBuffer) && 
                _statModifiersLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) &&
                modifierHandle.AffectedStatHandle.Index < statsBuffer.Length)
            {
                Stat affectedStat = statsBuffer[modifierHandle.AffectedStatHandle.Index];
                
                CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                    new CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>>(affectedStat.LastModifierIndex);
                while (modifiersIterator.GetNext(in statModifiersBuffer, out StatModifier<TStatModifier, TStatModifierStack> modifier, out int modifierIndex))
                {
                    if (modifier.ID == modifierHandle.ModifierID)
                    {
                        statModifier = modifier.Modifier;
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
            if (_statsLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<Stat> statsBufferOnAffectedStatEntity) && 
                _statModifiersLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBufferOnAffectedStatEntity) &&
                modifierHandle.AffectedStatHandle.Index < statsBufferOnAffectedStatEntity.Length)
            {       
                Stat affectedStat = statsBufferOnAffectedStatEntity[modifierHandle.AffectedStatHandle.Index];
                
                CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                    new CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>>(affectedStat.LastModifierIndex);
                while (modifiersIterator.GetNext(in statModifiersBufferOnAffectedStatEntity, out StatModifier<TStatModifier, TStatModifierStack> modifier, out int modifierIndex))
                {
                    if (modifier.ID == modifierHandle.ModifierID)
                    {
                        StatsUtilities.EnsureClearedValidTempList(ref _tmpLastIndexesList);
                        
                        // Remove modifier
                        {
                            // Build the last indexes array
                            _tmpLastIndexesList.Resize(statsBufferOnAffectedStatEntity.Length, NativeArrayOptions.ClearMemory);
                            for (int i = 0; i < statsBufferOnAffectedStatEntity.Length; i++)
                            {
                                _tmpLastIndexesList[i] = statsBufferOnAffectedStatEntity[i].LastModifierIndex;
                            }
                            
                            modifiersIterator.RemoveCurrentIteratedElementAndUpdateIndexes(
                                ref statModifiersBufferOnAffectedStatEntity,
                                ref _tmpLastIndexesList, 
                                out int firstUpdatedLastIndexIndex);

                            // Write back updated last indexes
                            for (int i = firstUpdatedLastIndexIndex; i < statsBufferOnAffectedStatEntity.Length; i++)
                            {
                                Stat tmpStatOnAffectedEntity = statsBufferOnAffectedStatEntity[i];
                                tmpStatOnAffectedEntity.LastModifierIndex = _tmpLastIndexesList[i];
                                statsBufferOnAffectedStatEntity[i] = tmpStatOnAffectedEntity;
                            }
                        }

                        // Remove the modifier's affected stat as an observer of modifier observed stats
                        {
                            StatsUtilities.EnsureClearedValidTempList(ref _tmpModifierObservedStatsList);
                            modifier.Modifier.AddObservedStatsToList(ref _tmpModifierObservedStatsList);

                            for (int a = 0; a < _tmpModifierObservedStatsList.Length; a++)
                            {
                                StatHandle observedStatHandle = _tmpModifierObservedStatsList[a];
                                
                                if (_statsLookup.TryGetBuffer(observedStatHandle.Entity, 
                                        out DynamicBuffer<Stat> statsBufferOnObservedStatEntity) &&
                                    _statObserversLookup.TryGetBuffer(observedStatHandle.Entity,
                                        out DynamicBuffer<StatObserver> statObserversBufferOnObservedStatEntity) &&
                                    observedStatHandle.Index < statsBufferOnObservedStatEntity.Length)
                                {
                                    Stat observedStat = statsBufferOnObservedStatEntity[observedStatHandle.Index];
                                    
                                    // Build the last indexes array
                                    _tmpLastIndexesList.Resize(statsBufferOnObservedStatEntity.Length, NativeArrayOptions.ClearMemory);
                                    for (int i = 0; i < statsBufferOnObservedStatEntity.Length; i++)
                                    {
                                        _tmpLastIndexesList[i] = statsBufferOnObservedStatEntity[i].LastObserverIndex;
                                    }

                                    int firstUpdatedLastIndexIndex = int.MaxValue;

                                    // Iterate observers of the observed stat and try to remove the affected stat
                                    CompactMultiLinkedListIterator<StatObserver> observersIterator =
                                        new CompactMultiLinkedListIterator<StatObserver>(observedStat
                                            .LastObserverIndex);
                                    while (observersIterator.GetNext(in statObserversBufferOnObservedStatEntity,
                                               out StatObserver observerOfObservedStat, out int observerIndex))
                                    {
                                        if (observerOfObservedStat.ObserverHandle == modifierHandle.AffectedStatHandle)
                                        {
                                            observersIterator.RemoveCurrentIteratedElementAndUpdateIndexes(
                                                ref statObserversBufferOnObservedStatEntity,
                                                ref _tmpLastIndexesList,
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
                                        for (int i = firstUpdatedLastIndexIndex; i < statsBufferOnObservedStatEntity.Length; i++)
                                        {
                                            Stat tmpStatOnObservedEntity = statsBufferOnObservedStatEntity[i];
                                            tmpStatOnObservedEntity.LastObserverIndex = _tmpLastIndexesList[i];
                                            statsBufferOnObservedStatEntity[i] = tmpStatOnObservedEntity;
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
        public bool TryRemoveAllStatModifiersOfStat(StatHandle statHandle)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool getStatSuccess, ref _nullStat);
            if (getStatSuccess &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer))
            {
                // Note: at each removal, the LastModifierIndex is updated
                while (statRef.LastModifierIndex >= 0)
                {
                    StatModifier<TStatModifier, TStatModifierStack> modifier = statModifiersBuffer[statRef.LastModifierIndex];

                    TryRemoveStatModifier(new StatModifierHandle
                    {
                        ModifierID = modifier.ID,
                        AffectedStatHandle = statHandle,
                    });
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