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
            ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(statHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue = baseValue;
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
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
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue = baseValue;
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatBaseValueInt(StatHandle statHandle, int baseValue, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(statHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue = baseValue.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
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
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue = baseValue.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValue(StatHandle statHandle, float baseValueAdd, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(statHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue += baseValueAdd;
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
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
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue += baseValueAdd;
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValueInt(StatHandle statHandle, int baseValueAdd, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(statHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                int baseValueAsInt = statRef.BaseValue.AsInt();
                baseValueAsInt += baseValueAdd;
                statRef.BaseValue = baseValueAsInt.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
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
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                int baseValueAsInt = statRef.BaseValue.AsInt();
                baseValueAsInt += baseValueAdd;
                statRef.BaseValue = baseValueAsInt.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValue(StatHandle statHandle, float baseValueMul, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(statHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue *= baseValueMul;
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValue(StatHandle statHandle, float baseValueMul, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue *= baseValueMul;
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValueInt(StatHandle statHandle, float baseValueMul, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(statHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue = ((float)statRef.BaseValue.AsInt() * baseValueMul);
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValueInt(StatHandle statHandle, float baseValueMul, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue = ((float)statRef.BaseValue.AsInt() * baseValueMul);
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValueInt(StatHandle statHandle, int baseValueMul, int baseValueDiv, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(statHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                int baseValueAsInt = statRef.BaseValue.AsInt();
                baseValueAsInt *= baseValueMul;
                baseValueAsInt /= baseValueDiv;
                statRef.BaseValue = baseValueAsInt.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValueInt(StatHandle statHandle, int baseValueMul, int baseValueDiv, ref DynamicBuffer<Stat> statsBuffer, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer, out bool success, ref _nullStat);
            if (success &&
                _statModifiersLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) && 
                _statObserversLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                int baseValueAsInt = statRef.BaseValue.AsInt();
                baseValueAsInt *= baseValueMul;
                baseValueAsInt /= baseValueDiv;
                statRef.BaseValue = baseValueAsInt.AsFloat();
                UpdateStatRef(statHandle, ref statRef, ref statsBuffer, ref statModifiersBuffer, ref statObserversBuffer, ref statsWorldData);
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

            statsWorldData._tmpGlobalUpdatedStatsList.Clear();
            statsWorldData._tmpGlobalUpdatedStatsList.Add(statHandle);

            for (int i = 0; i < statsWorldData._tmpGlobalUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = statsWorldData._tmpGlobalUpdatedStatsList[i];

                ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(newStatHandle, ref _statsLookup, out DynamicBuffer <Stat> statsBuffer, out bool getStatSuccess, ref _nullStat);
                if (getStatSuccess &&
                    _statModifiersLookup.TryGetBuffer(newStatHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) &&
                    _statObserversLookup.TryGetBuffer(newStatHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
                {
                    statsWorldData._tmpSameEntityUpdatedStatsList.Clear();
                    
                    StatsUtilities.UpdateSingleStatCommon(
                        newStatHandle,
                        ref statsReader,
                        ref statRef,
                        ref statModifiersBuffer,
                        ref statObserversBuffer,
                        ref statsWorldData);
            
                    // Then update same-entity list
                    for (int s = 0; s < statsWorldData._tmpSameEntityUpdatedStatsList.Length; s++)
                    {
                        StatHandle sameEntityStatHandle = statsWorldData._tmpSameEntityUpdatedStatsList[s];
                        ref Stat sameEntityStatRef = ref StatsUtilities.GetStatRefUnsafe(sameEntityStatHandle, ref statsBuffer, out getStatSuccess, ref _nullStat);
                        StatsUtilities.UpdateSingleStatCommon(
                            sameEntityStatHandle,
                            ref statsReader,
                            ref sameEntityStatRef,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsWorldData);
                    }
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

            statsWorldData._tmpGlobalUpdatedStatsList.Clear();
            statsWorldData._tmpSameEntityUpdatedStatsList.Clear();
            
            ref Stat statRef = ref StatsUtilities.GetStatRefUnsafe(statHandle, ref statsBuffer,
                out bool getStatSuccess, ref _nullStat);
            if (getStatSuccess)
            {
                StatsUtilities.UpdateSingleStatCommon(
                    statHandle,
                    ref statsReader,
                    ref statRef,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref statsWorldData);
            
                // Then update same-entity list
                for (int s = 0; s < statsWorldData._tmpSameEntityUpdatedStatsList.Length; s++)
                {
                    StatHandle sameEntityStatHandle = statsWorldData._tmpSameEntityUpdatedStatsList[s];
                    ref Stat sameEntityStatRef = ref StatsUtilities.GetStatRefUnsafe(sameEntityStatHandle, ref statsBuffer, out getStatSuccess, ref _nullStat);
                    StatsUtilities.UpdateSingleStatCommon(
                        sameEntityStatHandle,
                        ref statsReader,
                        ref sameEntityStatRef,
                        ref statModifiersBuffer,
                        ref statObserversBuffer,
                        ref statsWorldData);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateStatRef(
            StatHandle statHandle, 
            ref Stat initialStatRef, 
            ref DynamicBuffer<Stat> initialStatsBuffer, 
            ref DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> initialStatModifiersBuffer,
            ref DynamicBuffer<StatObserver> initialStatObserversBuffer,
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            StatsReader statsReader = new StatsReader(in _statsLookup);

            statsWorldData._tmpGlobalUpdatedStatsList.Clear();
            statsWorldData._tmpSameEntityUpdatedStatsList.Clear();

            // First update the current stat ref
            StatsUtilities.UpdateSingleStatCommon(
                statHandle,
                ref statsReader,
                ref initialStatRef,
                ref initialStatModifiersBuffer,
                ref initialStatObserversBuffer,
                ref statsWorldData);
            
            // Then update same-entity list
            for (int s = 0; s < statsWorldData._tmpSameEntityUpdatedStatsList.Length; s++)
            {
                StatHandle sameEntityStatHandle = statsWorldData._tmpSameEntityUpdatedStatsList[s];
                ref Stat sameEntityStatRef = ref StatsUtilities.GetStatRefUnsafe(sameEntityStatHandle, ref initialStatsBuffer, out bool getStatSuccess, ref _nullStat);
                StatsUtilities.UpdateSingleStatCommon(
                    sameEntityStatHandle,
                    ref statsReader,
                    ref sameEntityStatRef,
                    ref initialStatModifiersBuffer,
                    ref initialStatObserversBuffer,
                    ref statsWorldData);
            }

            // Then update following stats
            for (int i = 0; i < statsWorldData._tmpGlobalUpdatedStatsList.Length; i++)
            {
                StatHandle newStatHandle = statsWorldData._tmpGlobalUpdatedStatsList[i];

                ref Stat statRef = ref StatsUtilities.GetStatRefWithBufferUnsafe(newStatHandle, ref _statsLookup, out DynamicBuffer<Stat> statsBuffer, out bool getStatSuccess, ref _nullStat);
                if (getStatSuccess &&
                    _statModifiersLookup.TryGetBuffer(newStatHandle.Entity, out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer) &&
                    _statObserversLookup.TryGetBuffer(newStatHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
                {
                    statsWorldData._tmpSameEntityUpdatedStatsList.Clear();
                    
                    StatsUtilities.UpdateSingleStatCommon(
                        newStatHandle,
                        ref statsReader,
                        ref statRef,
                        ref statModifiersBuffer,
                        ref statObserversBuffer,
                        ref statsWorldData);
            
                    // Then update same-entity list
                    for (int s = 0; s < statsWorldData._tmpSameEntityUpdatedStatsList.Length; s++)
                    {
                        StatHandle sameEntityStatHandle = statsWorldData._tmpSameEntityUpdatedStatsList[s];
                        ref Stat sameEntityStatRef = ref StatsUtilities.GetStatRefUnsafe(sameEntityStatHandle, ref statsBuffer, out getStatSuccess, ref _nullStat);
                        StatsUtilities.UpdateSingleStatCommon(
                            sameEntityStatHandle,
                            ref statsReader,
                            ref sameEntityStatRef,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsWorldData);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStatModifiersCount(StatHandle statHandle, out int modifiersCount)
        {
            return StatsUtilities.TryGetModifiersCount(statHandle, ref _statsLookup, out modifiersCount);
        }

        /// <summary>
        /// Note: does not clear the supplied list
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStatModifiersOfStat(StatHandle statHandle, ref NativeList<StatModifierAndModifierHandle<TStatModifier, TStatModifierStack>> modifiers)
        {
            return StatsUtilities.TryGetModifiersOfStat(statHandle, ref _statsLookup, ref _statModifiersLookup, ref modifiers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetObserversCount(StatHandle statHandle, out int observersCount)
        {
            return StatsUtilities.TryGetObserversCount(statHandle, ref _statsLookup, out observersCount);
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
        public bool TryAddStatModifier(
            StatHandle affectedStatHandle, 
            TStatModifier modifier, 
            out StatModifierHandle statModifierHandle, 
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
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
                    false,
                    true);
            }

            statModifierHandle = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatModifiersBatch(
            StatHandle affectedStatHandle, 
            in UnsafeList<TStatModifier> modifiers, 
            ref UnsafeList<StatModifierHandle> statModifierHandles, 
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
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

                bool success = true;
                for (int i = 0; i < modifiers.Length; i++)
                {
                    success &= TryAddStatModifier(
                        affectedStatHandle,
                        modifiers[i],
                        ref affectStatsOwnerRef,
                        ref affectedStatRef,
                        ref statsBuffer,
                        ref statModifiersBufferOnAffectedStatEntity,
                        ref statObserversBufferOnAffectedStatEntity,
                        out StatModifierHandle statModifierHandle,
                        ref statsWorldData,
                        false,
                        false);
                    
                    statModifierHandles.Add(statModifierHandle);
                }
                
                UpdateStatRef(affectedStatHandle, ref affectedStatRef, ref statsBuffer, ref statModifiersBufferOnAffectedStatEntity, ref statObserversBufferOnAffectedStatEntity, ref statsWorldData);
                return success;
            }

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
                    true,
                    true);
            }

            statModifierHandle = default;
            return false;
        }

        internal bool TryAddStatModifiersBatchSingleEntity(
            StatHandle affectedStatHandle, 
            in UnsafeList<TStatModifier> modifiers, 
            ref StatsOwner affectStatsOwnerRef,
            ref DynamicBuffer<Stat> statsBufferOnAffectedStatEntity,
            ref DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBufferOnAffectedStatEntity,
            ref DynamicBuffer<StatObserver> statObserversBufferOnAffectedStatEntity,
            ref UnsafeList<StatModifierHandle> statModifierHandles, 
            ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            ref Stat affectedStatRef = ref StatsUtilities.GetStatRefUnsafe(affectedStatHandle, ref statsBufferOnAffectedStatEntity, out bool getStatSuccess, ref _nullStat);
            if (getStatSuccess)
            {
                bool success = true;
                for (int i = 0; i < modifiers.Length; i++)
                {
                    success &= TryAddStatModifier(
                        affectedStatHandle,
                        modifiers[i],
                        ref affectStatsOwnerRef,
                        ref affectedStatRef,
                        ref statsBufferOnAffectedStatEntity,
                        ref statModifiersBufferOnAffectedStatEntity,
                        ref statObserversBufferOnAffectedStatEntity,
                        out StatModifierHandle statModifierHandle,
                        ref statsWorldData,
                        true,
                        false);
                    
                    statModifierHandles.Add(statModifierHandle);
                }
                
                TryUpdateStatSingleEntity(
                    affectedStatHandle,
                    ref statsBufferOnAffectedStatEntity,
                    ref statModifiersBufferOnAffectedStatEntity,
                    ref statObserversBufferOnAffectedStatEntity,
                    ref statsWorldData);
                return success;
            }

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
            bool isGuaranteedSingleEntity,
            bool recalculateStat)
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
            // If the modifier observes any stats, handle infinite observer loops detection
            if(statsWorldData._tmpModifierObservedStatsList.Length > 0)
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
                    CompactSubList.Add(ref affectedStatRef.ModifiersList, ref statModifiersBufferOnAffectedStatEntity, modifierElement);
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
                if (recalculateStat)
                {
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

                CompactSubList.Iterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                    CompactSubList.GetIterator<StatModifier<TStatModifier, TStatModifierStack>>(
                        affectedStat.ModifiersList);
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
        public unsafe bool TryRemoveStatModifier(StatModifierHandle modifierHandle, ref StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData)
        {
            if (_statsLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<Stat> statsBufferOnAffectedStatEntity) &&
                _statModifiersLookup.TryGetBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBufferOnAffectedStatEntity) &&
                modifierHandle.AffectedStatHandle.Index < statsBufferOnAffectedStatEntity.Length)
            {
                ref Stat affectedStatRef = ref UnsafeUtility.ArrayElementAsRef<Stat>(
                    statsBufferOnAffectedStatEntity.GetUnsafePtr(), modifierHandle.AffectedStatHandle.Index);

                CompactSubList.Iterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                    CompactSubList.GetIterator<StatModifier<TStatModifier, TStatModifierStack>>(
                        affectedStatRef.ModifiersList);
                while (modifiersIterator.GetNext(in statModifiersBufferOnAffectedStatEntity, out StatModifier<TStatModifier, TStatModifierStack> modifier, out int modifierIndex))
                {
                    if (modifier.ID == modifierHandle.ModifierID)
                    {
                        // Remove modifier
                        modifiersIterator.RemoveIteratedElement(
                            ref affectedStatRef.ModifiersList,
                            ref statModifiersBufferOnAffectedStatEntity);

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
                                    ref Stat observedStatRef = ref UnsafeUtility.ArrayElementAsRef<Stat>(
                                        statsBufferOnObservedStatEntity.GetUnsafePtr(), observedStatHandle.Index);

                                    // Iterate observers of the observed stat and try to remove the affected stat
                                    CompactSubList.Iterator<StatObserver> observersIterator =
                                        CompactSubList.GetIterator<StatObserver>(observedStatRef.ObserversList);
                                    while (observersIterator.GetNext(in statObserversBufferOnObservedStatEntity,
                                               out StatObserver observerOfObservedStat, out int observerIndex))
                                    {
                                        if (observerOfObservedStat.ObserverHandle == modifierHandle.AffectedStatHandle)
                                        {
                                            observersIterator.RemoveIteratedElement(
                                                ref observedStatRef.ObserversList,
                                                ref statObserversBufferOnObservedStatEntity);

                                            // Break so we don't remove all observer instances of this stat
                                            break;
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
                while (statRef.ModifiersList.Count > 0)
                {
                    StatModifier<TStatModifier, TStatModifierStack> modifier = statModifiersBuffer[statRef.ModifiersList.FirstElementIndex];

                    bool success = TryRemoveStatModifier(new StatModifierHandle
                    {
                        ModifierID = modifier.ID,
                        AffectedStatHandle = statHandle,
                    },
                    ref statsWorldData);
                    Assert.IsTrue(success);
                }
                return true;
            }

            return false;
        }
    }
}