using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

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
        
        private StatValueReader _statValueReader;

        private NativeList<StatChangeEvent> _statChangeEventsList;
        private NativeList<StatHandle> _tmpModifierObservedStatsList;
        private NativeList<StatObserver> _tmpStatObserversList;
        private NativeList<StatHandle> _tmpUpdatedStatsList;
        private NativeList<int> _tmpLastIndexesList;

        public NativeList<StatChangeEvent> StatChangeEventsList
        {
            get { return _statChangeEventsList; }
            set { _statChangeEventsList = value; }
        }

        private Stat _nullStat;
        private Entity _latestStatsEntity;
        private Entity _latestStatModifiersEntity;
        private Entity _latestStatObserversEntity;
        private DynamicBuffer<Stat> _latestStatsBuffer;
        private DynamicBuffer<TStatModifier> _latestStatModifiersBuffer;
        private DynamicBuffer<StatObserver> _latestStatObserverBuffer;

        public StatsWorld(ref SystemState state)
        {
            _statsOwnerLookup = state.GetComponentLookup<StatsOwner>(false);
            _statsLookup = state.GetBufferLookup<Stat>(false);
            _statModifiersLookup = state.GetBufferLookup<TStatModifier>(false);
            _statObserversLookup = state.GetBufferLookup<StatObserver>(false);

            _statValueReader = new StatValueReader(ref _statsLookup);

            _tmpModifierObservedStatsList = default;
            _tmpStatObserversList = default;
            _tmpUpdatedStatsList = default;
            _tmpLastIndexesList = default;
            
            _statChangeEventsList = default;
            
            _nullStat = default;
            _latestStatsEntity = Entity.Null;
            _latestStatModifiersEntity = Entity.Null;
            _latestStatObserversEntity = Entity.Null;
            _latestStatsBuffer = default;
            _latestStatModifiersBuffer = default;
            _latestStatObserverBuffer = default;
        }

        public void OnUpdate(ref SystemState state)
        {
            _statsOwnerLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _statModifiersLookup.Update(ref state);
            _statObserversLookup.Update(ref state);

            _statValueReader = new StatValueReader(ref _statsLookup);
            
            _tmpModifierObservedStatsList = default;
            _tmpStatObserversList = default;
            _tmpUpdatedStatsList = default;
            _tmpLastIndexesList = default;
            
            _nullStat = default;
            _latestStatsEntity = Entity.Null;
            _latestStatModifiersEntity = Entity.Null;
            _latestStatObserversEntity = Entity.Null;
            _latestStatsBuffer = default;
            _latestStatModifiersBuffer = default;
            _latestStatObserverBuffer = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetStatsBuffer(Entity statEntity, out DynamicBuffer<Stat> statsBuffer)
        {
            if (statEntity != Entity.Null &&
                (statEntity == _latestStatsEntity ||
                 _statsLookup.TryGetBuffer(statEntity, out _latestStatsBuffer)))
            {
                statsBuffer = _latestStatsBuffer;
                return true;
            }

            statsBuffer = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetStatModifiersBuffer(Entity statEntity, out DynamicBuffer<TStatModifier> statModifiersBuffer)
        {
            if (statEntity != Entity.Null &&
                (statEntity == _latestStatModifiersEntity ||
                 _statModifiersLookup.TryGetBuffer(statEntity, out _latestStatModifiersBuffer)))
            {
                statModifiersBuffer = _latestStatModifiersBuffer;
                return true;
            }

            statModifiersBuffer = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetStatObserversBuffer(Entity statEntity, out DynamicBuffer<StatObserver> statObserversBuffer)
        {
            if (statEntity != Entity.Null &&
                (statEntity == _latestStatObserversEntity ||
                 _statObserversLookup.TryGetBuffer(statEntity, out _latestStatObserverBuffer)))
            {
                statObserversBuffer = _latestStatObserverBuffer;
                return true;
            }

            statObserversBuffer = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out Stat stat)
        {
            if (TryGetStatsBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
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
            if (TryGetStatsBuffer(statHandle.Entity, out statsBuffer))
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
            if (TryGetStatsBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
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
            if (success &&
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue = baseValue;
                UpdateStatRef(statHandle, ref statRef, ref statModifiersBuffer, ref statObserversBuffer);
                return true;
            }

            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValue(StatHandle statHandle, float baseValueAdd)
        {
            ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool success, ref _nullStat);
            if (success &&
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue += baseValueAdd;
                UpdateStatRef(statHandle, ref statRef, ref statModifiersBuffer, ref statObserversBuffer);
                return true;
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValue(StatHandle statHandle, float baseValueMul)
        {
            ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool success, ref _nullStat);
            if (success &&
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue *= baseValueMul;
                UpdateStatRef(statHandle, ref statRef, ref statModifiersBuffer, ref statObserversBuffer);
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
            if (TryGetStatsBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
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

            for (int i = 0; i < _tmpUpdatedStatsList.Length; i++)
            {
                ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool getStatSuccess, ref _nullStat);
                
                // TODO: if stat has no modifiers or observers, no need to get modifiers/observers buffers
                
                if (getStatSuccess &&
                    TryGetStatModifiersBuffer(statHandle.Entity,
                        out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                    TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
                {
                    _statValueReader.UpdateCacheData(_latestStatsEntity, _latestStatsBuffer);
                    StatsUtilities.UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
                        statHandle, 
                        ref _statValueReader,
                        ref statRef, 
                        ref statModifiersBuffer, 
                        ref statObserversBuffer, 
                        ref _statChangeEventsList,
                        ref _tmpUpdatedStatsList);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStatRef(
            StatHandle statHandle,
            ref Stat initialStatRef,
            ref DynamicBuffer<TStatModifier> initialStatModifiersBuffer,
            ref DynamicBuffer<StatObserver> initialStatObserversBuffer)
        {
            StatsUtilities.EnsureClearedValidTempList(ref _tmpUpdatedStatsList);
            _tmpUpdatedStatsList.Add(statHandle);
            
            // First update the current stat ref
            _statValueReader.UpdateCacheData(_latestStatsEntity, _latestStatsBuffer);
            StatsUtilities.UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
                statHandle, 
                ref _statValueReader,
                ref initialStatRef, 
                ref initialStatModifiersBuffer, 
                ref initialStatObserversBuffer, 
                ref _statChangeEventsList,
                ref _tmpUpdatedStatsList);
            
            // Then update following stats
            for (int i = 0; i < _tmpUpdatedStatsList.Length; i++)
            {
                ref Stat statRef = ref TryGetStatRefUnsafe(statHandle, out bool getStatSuccess, ref _nullStat);
                
                // TODO: if stat has no modifiers or observers, no need to get modifiers/observers buffers
                
                if (getStatSuccess &&
                    TryGetStatModifiersBuffer(statHandle.Entity,
                        out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                    TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
                {
                    _statValueReader.UpdateCacheData(_latestStatsEntity, _latestStatsBuffer);
                    StatsUtilities.UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
                        statHandle, 
                        ref _statValueReader,
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
            if (TryGetStatsBuffer(entity, out DynamicBuffer<Stat> statsBuffer))
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
            if (TryGetStatAndBuffer(affectedStatHandle, out Stat stat, out DynamicBuffer<Stat> statsBuffer) &&
                TryGetStatModifiersBuffer(affectedStatHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(affectedStatHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                ref StatsOwner statsOwnerRef = ref _statsOwnerLookup.GetRefRW(affectedStatHandle.Entity).ValueRW;

                StatObserversHandler statObserversHandler = new StatObserversHandler(
                    _statsLookup,
                    _latestStatsEntity,
                    _latestStatsBuffer,
                    _statObserversLookup,
                    _latestStatObserversEntity,
                    _latestStatObserverBuffer);
                
                StatsUtilities.AddModifierCommon<TStatModifier, TStatModifierStack>(
                    false,
                    true,
                    affectedStatHandle, 
                    ref modifier, 
                    ref statObserversHandler,
                    ref statsOwnerRef,
                    out statModifierHandle,
                    ref statsBuffer,
                    ref statModifiersBuffer,
                    ref _tmpModifierObservedStatsList,
                    ref _tmpStatObserversList);
                
                // Update stat following modifier add
                ref Stat statRef = ref TryGetStatRefUnsafe(affectedStatHandle, out bool success, ref _nullStat);
                UpdateStatRef(affectedStatHandle, ref statRef, ref statModifiersBuffer, ref statObserversBuffer);
            }

            statModifierHandle = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStatModifier(StatModifierHandle modifierHandle, out TStatModifier statModifier)
        {
            if (TryGetStatModifiersBuffer(modifierHandle.AffectedStatHandle.Entity,
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryRemoveStatModifier(StatModifierHandle modifierHandle)
        {
            if (TryGetStatAndBuffer(modifierHandle.AffectedStatHandle, out Stat stat, out DynamicBuffer<Stat> statsBuffer) && 
                TryGetStatModifiersBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<StatObserver> statObserversBuffer))
            {       
                CompactMultiLinkedListIterator<TStatModifier> modifiersIterator =
                    new CompactMultiLinkedListIterator<TStatModifier>(stat.LastModifierIndex);
                while (modifiersIterator.GetNext(ref statModifiersBuffer, out TStatModifier modifier, out int modifierIndex))
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
                                    TryGetStatModifiersBuffer(observedStatHandle.Entity,
                                        out statModifiersBuffer) &&
                                    TryGetStatObserversBuffer(observedStatHandle.Entity,
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
                                    while (observersIterator.GetNext(ref statObserversBuffer,
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
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer))
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