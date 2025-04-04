using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Assertions;
using Unity.Jobs;

namespace Trove.Stats
{
    public struct StatsAccessor<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        internal ComponentLookup<StatsOwner> _statsOwnerLookup;
        internal BufferLookup<Stat> _statsLookup;
        internal BufferLookup<StatModifier<TStatModifier, TStatModifierStack>> _statModifiersLookup;
        internal BufferLookup<StatObserver> _statObserversLookup;

        private Stat _nullStat;

        public StatsAccessor(ref SystemState state)
        {
            _statsOwnerLookup = state.GetComponentLookup<StatsOwner>(false);
            _statsLookup = state.GetBufferLookup<Stat>(false);
            _statModifiersLookup = state.GetBufferLookup<StatModifier<TStatModifier, TStatModifierStack>>(false);
            _statObserversLookup = state.GetBufferLookup<StatObserver>(false);

            _nullStat = default;
        }

        internal static StatsAccessor<TStatModifier, TStatModifierStack> CreateForBaking()
        {
            return new StatsAccessor<TStatModifier, TStatModifierStack>
            {
                _statsOwnerLookup = default,
                _statsLookup = default,
                _statModifiersLookup = default,
                _statObserversLookup = default,

                _nullStat = default,
            };
        }

        public void Update(ref SystemState state)
        {
            _statsOwnerLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _statModifiersLookup.Update(ref state);
            _statObserversLookup.Update(ref state);
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
            return StatsUtilities.TryGetStat(statHandle, in _statsLookup, out value, out baseValue);
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, ref DynamicBuffer<Stat> statsBuffer, out float value, out float baseValue)
        {
            return StatsUtilities.TryGetStat(statHandle, in statsBuffer, out value, out baseValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatBaseValue(StatHandle statHandle, float baseValue, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue = baseValue;
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatBaseValue(StatHandle statHandle, float baseValue, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue = baseValue;
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatBaseValueInt(StatHandle statHandle, int baseValue, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue = baseValue.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatBaseValueInt(StatHandle statHandle, int baseValue, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue = baseValue.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValue(StatHandle statHandle, float baseValueAdd, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue += baseValueAdd;
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValue(StatHandle statHandle, float baseValueAdd, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue += baseValueAdd;
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValueInt(StatHandle statHandle, int baseValueAdd, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                int baseValueAsInt = statRef.BaseValue.AsInt();
                baseValueAsInt += baseValueAdd;
                statRef.BaseValue = baseValueAsInt.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValueInt(StatHandle statHandle, int baseValueAdd, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success)
            {
                int baseValueAsInt = statRef.BaseValue.AsInt();
                baseValueAsInt += baseValueAdd;
                statRef.BaseValue = baseValueAsInt.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValue(StatHandle statHandle, float baseValueMul, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue *= baseValueMul;
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValue(StatHandle statHandle, float baseValueMul, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue *= baseValueMul;
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValueInt(StatHandle statHandle, float baseValueMul, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue = ((float)statRef.BaseValue.AsInt() * baseValueMul);
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValueInt(StatHandle statHandle, float baseValueMul, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success)
            {
                statRef.BaseValue = ((float)statRef.BaseValue.AsInt() * baseValueMul);
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValueInt(StatHandle statHandle, int baseValueMul, int baseValueDiv, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref _statsLookup, out bool success, ref _nullStat);
            if (success)
            {
                int baseValueAsInt = statRef.BaseValue.AsInt();
                baseValueAsInt *= baseValueMul;
                baseValueAsInt /= baseValueDiv;
                statRef.BaseValue = baseValueAsInt.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValueInt(StatHandle statHandle, int baseValueMul, int baseValueDiv, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success)
            {
                int baseValueAsInt = statRef.BaseValue.AsInt();
                baseValueAsInt *= baseValueMul;
                baseValueAsInt /= baseValueDiv;
                statRef.BaseValue = baseValueAsInt.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsWorldData);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryUpdateAllStats(Entity entity, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            if (_statsLookup.TryGetBuffer(entity, out DynamicBuffer<Stat> statsBuffer))
            {
                for (int i = 0; i < statsBuffer.Length; i++)
                {
                    TryUpdateStat(new StatHandle(entity, i), ref statsWorldData);
                }
            }
        }
        
        /// <summary>
        /// Note: if the stat doesn't exist, it just does nothing (no error).
        /// </summary>
        /// <param name="statHandle"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryUpdateStat(StatHandle statHandle, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            StatsReader statsReader = new StatsReader(in _statsLookup);

            statsWorldData._tmpUpdatedStatsList.Clear();
            statsWorldData._tmpUpdatedStatsList.Add(statHandle);

            DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer = default;
            DynamicBuffer<StatObserver> statObserversBuffer = default;
            for (int i = 0; i < statsWorldData._tmpUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = statsWorldData._tmpUpdatedStatsList[i];

                ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(newStatHandle, ref _statsLookup, out bool getStatSuccess, ref _nullStat);
                if (getStatSuccess)
                {
                    if (statRef.LastModifierIndex >= 0)
                    {
                        bool success = _statModifiersLookup.TryGetBuffer(newStatHandle.Entity, out statModifiersBuffer);
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
                        ref statsWorldData);
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryUpdateStatSingleEntity(
            StatHandle statHandle, 
            ref DynamicBuffer<Stat> statsBuffer,
            ref DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            StatsReader statsReader = new StatsReader(in statsBuffer);

            statsWorldData._tmpUpdatedStatsList.Clear();
            statsWorldData._tmpUpdatedStatsList.Add(statHandle);

            for (int i = 0; i < statsWorldData._tmpUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = statsWorldData._tmpUpdatedStatsList[i];

                if (newStatHandle.Entity == statHandle.Entity)
                {
                    ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(newStatHandle, ref statsBuffer,
                        out bool getStatSuccess, ref _nullStat);
                    if (getStatSuccess)
                    {
                        StatsUtilities.UpdateSingleStatCommon(
                            newStatHandle,
                            ref statsReader,
                            ref statRef,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsWorldData);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStatRef(StatHandle statHandle, ref Stat initialStatRef, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            StatsReader statsReader = new StatsReader(in _statsLookup);

            statsWorldData._tmpUpdatedStatsList.Clear();

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
                ref statsWorldData);

            // Then update following stats
            DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer = default;
            DynamicBuffer<StatObserver> statObserversBuffer = default;
            for (int i = 0; i < statsWorldData._tmpUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = statsWorldData._tmpUpdatedStatsList[i];

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
                        ref statsWorldData);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCalculateStatModifiersCount(StatHandle statHandle, out int modifiersCount)
        {
            return StatsUtilities.TryCalculateModifiersCount(statHandle, ref _statsLookup, ref _statModifiersLookup, out modifiersCount);
        }

        /// <summary>
        /// Note: does not clear the supplied list
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStatModifiersOfStat(StatHandle statHandle, ref NativeList<StatModifierAndHandle<TStatModifier, TStatModifierStack>> modifiers)
        {
            return StatsUtilities.TryGetModifiersOfStat(statHandle, ref _statsLookup, ref _statModifiersLookup, ref modifiers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCalculateObserversCount(StatHandle statHandle, out int observersCount)
        {
            return StatsUtilities.TryCalculateObserversCount(statHandle, ref _statsLookup, ref _statObserversLookup, out observersCount);
        }

        /// <summary>
        /// Note: does not clear the supplied list
        /// Note: useful to store observers before destroying an entity, and then manually update all observers after
        /// destroy. An observers update isn't automatically called when a stats entity is destroyed. (TODO:?) 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAllObservers(Entity entity, ref NativeList<StatObserver> observersList)
        {
            return StatsUtilities.TryGetAllObservers(entity, ref _statObserversLookup, ref observersList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatModifier(StatHandle affectedStatHandle, TStatModifier modifier, out StatModifierHandle statModifierHandle, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat affectedStatRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(affectedStatHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool getStatSuccess, ref _nullStat);
            if (getStatSuccess &&
                _statsOwnerLookup.HasComponent(affectedStatHandle.Entity) &&
                _statModifiersLookup.TryGetBuffer(affectedStatHandle.Entity,
                    out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>>
                        statModifiersBufferOnAffectedStatEntity) &&
                _statObserversLookup.TryGetBuffer(affectedStatHandle.Entity,
                    out DynamicBuffer<StatObserver> statObserversBufferOnAffectedStatEntity))
            {
                ref StatsOwner affectStatsOwnerRef = ref _statsOwnerLookup.GetRefRW(affectedStatHandle.Entity).ValueRW;

                return TryAddStatModifier(
                    affectedStatHandle,
                    modifier,
                    ref affectStatsOwnerRef,
                    ref affectedStatRef,
                    ref statsBuffer,
                    ref statModifiersBufferOnAffectedStatEntity,
                    ref statObserversBufferOnAffectedStatEntity,
                    out statModifierHandle,
                    ref statsWorldData,
                    false);
            }

            statModifierHandle = default;
            return false;
        }

        internal bool TryAddStatModifierSingleEntity(
            StatHandle affectedStatHandle, 
            TStatModifier modifier, 
            ref StatsOwner affectStatsOwnerRef,
            ref DynamicBuffer<Stat> statsBufferOnAffectedStatEntity,
            ref DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBufferOnAffectedStatEntity,
            ref DynamicBuffer<StatObserver> statObserversBufferOnAffectedStatEntity,
            out StatModifierHandle statModifierHandle, 
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat affectedStatRef = ref StatsUtilities.GetStatRefUnsafe(affectedStatHandle, ref statsBufferOnAffectedStatEntity, out bool getStatSuccess, ref _nullStat);
            if (getStatSuccess)
            {
                return TryAddStatModifier(
                    affectedStatHandle,
                    modifier,
                    ref affectStatsOwnerRef,
                    ref affectedStatRef,
                    ref statsBufferOnAffectedStatEntity,
                    ref statModifiersBufferOnAffectedStatEntity,
                    ref statObserversBufferOnAffectedStatEntity,
                    out statModifierHandle,
                    ref statsWorldData,
                    true);
            }

            statModifierHandle = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryAddStatModifier(
            StatHandle affectedStatHandle,
            TStatModifier modifier,
            ref StatsOwner affectStatsOwnerRef,
            ref Stat affectedStatRef,
            ref DynamicBuffer<Stat> statsBufferOnAffectedStatEntity,
            ref DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBufferOnAffectedStatEntity,
            ref DynamicBuffer<StatObserver> statObserversBufferOnAffectedStatEntity,
            out StatModifierHandle statModifierHandle,
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData,
            bool isGuaranteedSingleEntity)
        {
            // Ensure lists are created and cleared
            statsWorldData._tmpModifierObservedStatsList.Clear();
            statsWorldData._tmpStatObserversList.Clear();

            statModifierHandle = new StatModifierHandle
            {
                AffectedStatHandle = affectedStatHandle,
            };

            StatModifier<TStatModifier, TStatModifierStack> modifierElement =
                new StatModifier<TStatModifier, TStatModifierStack>();
            modifierElement.Modifier = modifier;

            // Increment modifier Id (local to entity)
            affectStatsOwnerRef.ModifierIDCounter++;
            modifierElement.ID = affectStatsOwnerRef.ModifierIDCounter;
            statModifierHandle.ModifierID = modifierElement.ID;

            // Get observed stats of modifier
            modifier.AddObservedStatsToList(ref statsWorldData._tmpModifierObservedStatsList);

            bool modifierCanBeAdded = true;
            {
                // Make sure the modifier wouldn't make the stat observe itself (would cause infinite loop)
                for (int j = 0; j < statsWorldData._tmpModifierObservedStatsList.Length; j++)
                {
                    StatHandle modifierObservedStatHandle = statsWorldData._tmpModifierObservedStatsList[j];
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
                        ref statsWorldData._tmpStatObserversList);

                    // TODO: make sure this verification loop can't possibly end up being infinite either. It could be infinite if we haven't guaranteed loop detection for other modifier adds...
                    for (int i = 0; i < statsWorldData._tmpStatObserversList.Length; i++)
                    {
                        StatHandle iteratedObserverStatHandle = statsWorldData._tmpStatObserversList[i].ObserverHandle;

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
                        for (int j = 0; j < statsWorldData._tmpModifierObservedStatsList.Length; j++)
                        {
                            StatHandle modifierObservedStatHandle = statsWorldData._tmpModifierObservedStatsList[j];
                            if (iteratedObserverStatHandle == modifierObservedStatHandle)
                            {
                                statsWorldData._tmpStatObserversList.Add(new StatObserver
                                {
                                    ObserverHandle = affectedStatHandle,
                                });
                            }
                        }

                        // Update buffers so they represent the ones on the observer entity
                        if (isGuaranteedSingleEntity)
                        {
                            if (StatsUtilities.TryGetStat(iteratedObserverStatHandle, in statsBufferOnAffectedStatEntity,
                                    out Stat observerStat))
                            {
                                StatsUtilities.AddObserversOfStatToList(
                                    in observerStat,
                                    in statObserversBufferOnAffectedStatEntity,
                                    ref statsWorldData._tmpStatObserversList);
                            }
                        }
                        else
                        {
                            if (StatsUtilities.TryGetStat(iteratedObserverStatHandle, in _statsLookup,
                                    out Stat observerStat) &&
                                _statObserversLookup.TryGetBuffer(iteratedObserverStatHandle.Entity,
                                    out DynamicBuffer<StatObserver> observerStatObserversBuffer))
                            {
                                StatsUtilities.AddObserversOfStatToList(
                                    in observerStat,
                                    in observerStatObserversBuffer,
                                    ref statsWorldData._tmpStatObserversList);
                            }
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
                for (int i = 0; i < statsWorldData._tmpModifierObservedStatsList.Length; i++)
                {
                    StatHandle observedStatHandle = statsWorldData._tmpModifierObservedStatsList[i];

                    // Update buffers so they represent the ones on the observer entity
                    if (isGuaranteedSingleEntity)
                    {
                        StatsUtilities.AddStatAsObserverOfOtherStat(
                            affectedStatHandle,
                            observedStatHandle,
                            ref statsBufferOnAffectedStatEntity,
                            ref statObserversBufferOnAffectedStatEntity);
                    }
                    else
                    {
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
                }

                // Update stat following modifier add
                if (isGuaranteedSingleEntity)
                {
                    TryUpdateStatSingleEntity(
                        affectedStatHandle, 
                        ref statsBufferOnAffectedStatEntity,
                        ref statModifiersBufferOnAffectedStatEntity,
                        ref statObserversBufferOnAffectedStatEntity,
                        ref statsWorldData);
                }
                else
                {
                    TryUpdateStat(affectedStatHandle, ref statsWorldData);
                }
                return true;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveStatModifier(StatModifierHandle modifierHandle, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
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
                        statsWorldData._tmpLastIndexesList.Clear();

                        // Remove modifier
                        {
                            // Build the last indexes array
                            statsWorldData._tmpLastIndexesList.Resize(statsBufferOnAffectedStatEntity.Length, NativeArrayOptions.ClearMemory);
                            for (int i = 0; i < statsBufferOnAffectedStatEntity.Length; i++)
                            {
                                statsWorldData._tmpLastIndexesList[i] = statsBufferOnAffectedStatEntity[i].LastModifierIndex;
                            }

                            modifiersIterator.RemoveCurrentIteratedElementAndUpdateIndexes(
                                ref statModifiersBufferOnAffectedStatEntity,
                                ref statsWorldData._tmpLastIndexesList,
                                out int firstUpdatedLastIndexIndex);

                            // Write back updated last indexes
                            for (int i = firstUpdatedLastIndexIndex; i < statsBufferOnAffectedStatEntity.Length; i++)
                            {
                                Stat tmpStatOnAffectedEntity = statsBufferOnAffectedStatEntity[i];
                                tmpStatOnAffectedEntity.LastModifierIndex = statsWorldData._tmpLastIndexesList[i];
                                statsBufferOnAffectedStatEntity[i] = tmpStatOnAffectedEntity;
                            }
                        }

                        // Remove the modifier's affected stat as an observer of modifier observed stats
                        {
                            statsWorldData._tmpModifierObservedStatsList.Clear();
                            modifier.Modifier.AddObservedStatsToList(ref statsWorldData._tmpModifierObservedStatsList);

                            for (int a = 0; a < statsWorldData._tmpModifierObservedStatsList.Length; a++)
                            {
                                StatHandle observedStatHandle = statsWorldData._tmpModifierObservedStatsList[a];

                                if (_statsLookup.TryGetBuffer(observedStatHandle.Entity,
                                        out DynamicBuffer<Stat> statsBufferOnObservedStatEntity) &&
                                    _statObserversLookup.TryGetBuffer(observedStatHandle.Entity,
                                        out DynamicBuffer<StatObserver> statObserversBufferOnObservedStatEntity) &&
                                    observedStatHandle.Index < statsBufferOnObservedStatEntity.Length)
                                {
                                    Stat observedStat = statsBufferOnObservedStatEntity[observedStatHandle.Index];

                                    // Build the last indexes array
                                    statsWorldData._tmpLastIndexesList.Resize(statsBufferOnObservedStatEntity.Length, NativeArrayOptions.ClearMemory);
                                    for (int i = 0; i < statsBufferOnObservedStatEntity.Length; i++)
                                    {
                                        statsWorldData._tmpLastIndexesList[i] = statsBufferOnObservedStatEntity[i].LastObserverIndex;
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
                                                ref statsWorldData._tmpLastIndexesList,
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
                                            tmpStatOnObservedEntity.LastObserverIndex = statsWorldData._tmpLastIndexesList[i];
                                            statsBufferOnObservedStatEntity[i] = tmpStatOnObservedEntity;
                                        }
                                    }
                                }
                            }
                        }

                        // Stat update following modifier remove
                        TryUpdateStat(modifierHandle.AffectedStatHandle, ref statsWorldData);

                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveAllStatModifiersOfStat(StatHandle statHandle, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
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
                    },
                    ref statsWorldData);
                }
                return true;
            }

            return false;
        }
    }
}