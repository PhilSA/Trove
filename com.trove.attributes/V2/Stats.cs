using System.Runtime.CompilerServices;
using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Collections;
using Unity.Entities.Content;
using Unity.Jobs;
using UnityEngine;

namespace Trove.Stats
{
    public interface IStatsModifierStack
    {
        public void Reset();
        public void Apply(ref float statBaseValue, ref float statValue);
    }

    public interface IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public uint ID { get; set; } // TODO: is it ok for set to be public
        public void AddObservedStatsToList(ref NativeList<StatHandle> observedStatHandles);
        public void Apply(
            in StatsReader statsReader,
            ref TStatModifierStack stack);
    }

    public struct StatsReader
    {
        private BufferLookup<Stat> StatsLookup;
        
        private Entity _latestStatsEntity;
        private DynamicBuffer<Stat> _latestStatsBuffer;

        internal StatsReader(ref BufferLookup<Stat> statsLookup)
        {
            StatsLookup = statsLookup;
            
            _latestStatsEntity = Entity.Null;
            _latestStatsBuffer = default;
        }

        internal void UpdateCacheData(Entity latestStatsEntity, DynamicBuffer<Stat> latestStatsBuffer)
        {
            _latestStatsEntity = latestStatsEntity;
            _latestStatsBuffer = latestStatsBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetStatsBuffer(StatHandle statHandle, out DynamicBuffer<Stat> statsBuffer)
        {
            if (statHandle.Entity != Entity.Null &&
                (statHandle.Entity == _latestStatsEntity ||
                 StatsLookup.TryGetBuffer(statHandle.Entity, out _latestStatsBuffer)))
            {
                statsBuffer = _latestStatsBuffer;
                return true;
            }

            statsBuffer = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out Stat stat)
        {
            if (TryGetStatsBuffer(statHandle, out DynamicBuffer<Stat> statsBuffer))
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
    }

    public struct StatsWriter<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        private ComponentLookup<StatsOwner> StatsOwnerLookup;
        private BufferLookup<Stat> StatsLookup;
        private BufferLookup<TStatModifier> StatModifiersLookup;
        private BufferLookup<StatObserver> StatObserversLookup;
        
        private StatsReader _statsReader;
        
        private NativeList<StatHandle> _tmpStatsList;
        public NativeList<StatChangeEvent> StatChangeEventsList { get; set; }

        private Stat _nullStat;
        private Entity _latestStatsEntity;
        private Entity _latestStatModifiersEntity;
        private Entity _latestStatObserversEntity;
        private DynamicBuffer<Stat> _latestStatsBuffer;
        private DynamicBuffer<TStatModifier> _latestStatModifiersBuffer;
        private DynamicBuffer<StatObserver> _latestStatObserverBuffer;

        public StatsWriter(ref SystemState state)
        {
            StatsOwnerLookup = state.GetComponentLookup<StatsOwner>(false);
            StatsLookup = state.GetBufferLookup<Stat>(false);
            StatModifiersLookup = state.GetBufferLookup<TStatModifier>(false);
            StatObserversLookup = state.GetBufferLookup<StatObserver>(false);

            _statsReader = new StatsReader(ref StatsLookup);

            _tmpStatsList = default;
            StatChangeEventsList = default;
            
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
            StatsOwnerLookup.Update(ref state);
            StatsLookup.Update(ref state);
            StatModifiersLookup.Update(ref state);
            StatObserversLookup.Update(ref state);

            _statsReader = new StatsReader(ref StatsLookup);
            
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
                 StatsLookup.TryGetBuffer(statEntity, out _latestStatsBuffer)))
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
                 StatModifiersLookup.TryGetBuffer(statEntity, out _latestStatModifiersBuffer)))
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
                 StatObserversLookup.TryGetBuffer(statEntity, out _latestStatObserverBuffer)))
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
        
        /// <summary>
        /// Private because we always have to manually call UpdateStat after modification
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe ref Stat TryGetStatRef(StatHandle statHandle, out bool success, ref Stat failResult)
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
            ref Stat statRef = ref TryGetStatRef(statHandle, out bool success, ref _nullStat);
            if (success &&
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue = baseValue;
                UpdateStat(statHandle, ref statRef, ref statModifiersBuffer, ref statObserversBuffer);
                return true;
            }

            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatBaseValue(StatHandle statHandle, float baseValueAdd)
        {
            ref Stat statRef = ref TryGetStatRef(statHandle, out bool success, ref _nullStat);
            if (success &&
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue += baseValueAdd;
                UpdateStat(statHandle, ref statRef, ref statModifiersBuffer, ref statObserversBuffer);
                return true;
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMultiplyStatBaseValue(StatHandle statHandle, float baseValueMul)
        {
            ref Stat statRef = ref TryGetStatRef(statHandle, out bool success, ref _nullStat);
            if (success &&
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                statRef.BaseValue *= baseValueMul;
                UpdateStat(statHandle, ref statRef, ref statModifiersBuffer, ref statObserversBuffer);
                return true;
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetStatProduceChangeEvents(StatHandle statHandle, bool value)
        {
            ref Stat statRef = ref TryGetStatRef(statHandle, out bool success, ref _nullStat);
            if (success)
            {
                statRef.ProduceChangeEvents = value ? (byte)1 : (byte)0;
                return true;
            }
            
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryUpdateStat(StatHandle statHandle)
        {
            ref Stat statRef = ref TryGetStatRef(statHandle, out bool success, ref _nullStat);
            if (success &&
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                UpdateStat(statHandle, ref statRef, ref statModifiersBuffer, ref statObserversBuffer);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStat(StatHandle statHandle,
            ref Stat statRef,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer)
        {
            Stat initialStat = statRef;

            _statsReader.UpdateCacheData(_latestStatsEntity, _latestStatsBuffer);
            
            // Apply Modifiers
            TStatModifierStack modifierStack = new TStatModifierStack();
            modifierStack.Reset();
            for (int m = statRef.ModifiersStartIndex; m < statRef.ModifiersStartIndex + statRef.ModifiersCount; m++)
            {
                TStatModifier modifier = statModifiersBuffer[m];
                modifier.Apply(
                    in _statsReader,
                    ref modifierStack);
                // TODO: give a way to say "the modifier depends on a now invalid stat and must be removed"
            }

            modifierStack.Apply(ref statRef.BaseValue, ref statRef.Value);

            // Stat change events
            if (statRef.ProduceChangeEvents == 1 && StatChangeEventsList.IsCreated)
            {
                StatChangeEventsList.Add(new StatChangeEvent
                {
                    StatHandle = statHandle,
                    PrevValue = initialStat,
                    NewValue = statRef,
                });
            }

            // Notify Observers
            for (int o = statRef.ObserversStartIndex; o < statRef.ObserversStartIndex + statRef.ObserversCount; o++)
            {
                StatObserver observer = statObserversBuffer[o];
                TryUpdateStat(observer.ObserverHandle); // TODO: try not to have a recursive update. Add to some list then update the list instead
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

        /// <summary>
        /// Note: modifiers are inserted in affected stat index order. Adding a modifier changes the stored
        /// modifiers start index and count in stats buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddStatModifier(StatHandle statHandle, TStatModifier modifier, out StatModifierHandle statModifierHandle)
        {
            if (TryGetStat(statHandle, out Stat stat) &&
                StatsOwnerLookup.TryGetComponent(statHandle.Entity, out StatsOwner statsOwner) &&
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                StatsUtilities.AddModifierCommonPhase1<TStatModifier, TStatModifierStack>(
                    statHandle, 
                    ref modifier, 
                    ref statsOwner,
                    out statModifierHandle,
                    ref _tmpStatsList);
                
                StatsOwnerLookup[statHandle.Entity] = statsOwner;
                
                // First, do a check to prevent self-observing stats and infinite observers loops
                bool modifierCanBeAdded = true;
                for (int i = 0; i < _tmpStatsList.Length; i++)
                {
                    StatHandle observedStatHandle = _tmpStatsList[i];
                    if (IsStatHandlePresentDownObserversChain(observedStatHandle, statHandle, ref statObserversBuffer))
                    {
                        modifierCanBeAdded = false;
                        break;
                    }
                }
                
                // Only add modifier if no infinite loop would be created
                if (modifierCanBeAdded)
                {
                    TryGetStatsBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer);

                    StatsUtilities.AddModifierCommonPhase2<TStatModifier, TStatModifierStack>(
                        statHandle,
                        ref stat,
                        modifier,
                        in _tmpStatsList,
                        ref statsBuffer,
                        in statModifiersBuffer,
                        in statObserversBuffer);

                    // Write back stat data
                    statsBuffer[statHandle.Index] = stat;

                    // Stat update
                    ref Stat statRef = ref TryGetStatRef(statHandle, out bool statSuccess, ref _nullStat);
                    UpdateStat(statHandle,
                        ref statRef,
                        ref statModifiersBuffer,
                        ref statObserversBuffer);

                    return true;
                }
                else
                {
                    Debug.Log("Warning: stat modifier couldn't be added because it would've created an infinite stats update loop. The stat it affects would either directly or indirectly react to its own changes down the reactive stats chain.");
                }
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
        public bool TryRemoveStatModifier(StatModifierHandle modifierHandle)
        {
            if (TryGetStat(modifierHandle.AffectedStatHandle, out Stat stat) && 
                TryGetStatModifiersBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(modifierHandle.AffectedStatHandle.Entity,
                    out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                for (int i = stat.ModifiersStartIndex; i < stat.ModifiersStartIndex + stat.ModifiersCount; i++)
                {
                    TStatModifier modifier = statModifiersBuffer[i];
                    if (modifier.ID == modifierHandle.ModifierID)
                    {
                        TryGetStatsBuffer(modifierHandle.AffectedStatHandle.Entity, out DynamicBuffer<Stat> statsBuffer);

                        // Remove observed stats of the modifier as observers of the affected stat
                        {
                            EnsureClearedValidTmpStatsList();
                            modifier.AddObservedStatsToList(ref _tmpStatsList);

                            int removedObserversCounter = 0;
                            for (int a = 0; a < _tmpStatsList.Length; a++)
                            {
                                StatHandle observerToRemove = _tmpStatsList[a];
                                for (int b = stat.ObserversStartIndex + stat.ObserversCount - 1;
                                     b >= stat.ObserversStartIndex;
                                     b--)
                                {
                                    StatObserver existingObserver = statObserversBuffer[b];
                                    if (existingObserver.ObserverHandle == observerToRemove)
                                    {
                                        // TODO: do the thing where we keep observers counts instead of multiple observer elements?
                                        // TODO: but if we do that we have to remember that the "removedObserversCounter" only increments when a full buffer element is removed
                                        statObserversBuffer.RemoveAt(b);
                                        removedObserversCounter++;
                                        break;
                                    }
                                }
                            }

                            Assert.AreEqual(_tmpStatsList.Length, removedObserversCounter);

                            StatsUtilities.OnObserversChanged(ref stat, modifierHandle.AffectedStatHandle, -removedObserversCounter,
                                ref statsBuffer);
                        }
                        
                        // Remove modifier
                        {
                            statModifiersBuffer.RemoveAt(i);
                            StatsUtilities.OnModifiersChanged(ref stat, modifierHandle.AffectedStatHandle, -1, ref statsBuffer);
                        }

                        // Write back stat data
                        statsBuffer[modifierHandle.AffectedStatHandle.Index] = stat;
                        
                        // Stat update
                        ref Stat statRef = ref TryGetStatRef(modifierHandle.AffectedStatHandle, out bool statSuccess, ref _nullStat);
                        UpdateStat(modifierHandle.AffectedStatHandle,
                            ref statRef,
                            ref statModifiersBuffer,
                            ref statObserversBuffer);

                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveAllStatModifiersOfStat(StatHandle statHandle)
        {
            if (TryGetStat(statHandle, out Stat stat) && 
                TryGetStatModifiersBuffer(statHandle.Entity, out DynamicBuffer<TStatModifier> statModifiersBuffer) &&
                TryGetStatObserversBuffer(statHandle.Entity, out DynamicBuffer<StatObserver> statObserversBuffer))
            {
                TryGetStatsBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer);
                
                // Remove all observers
                {
                    statObserversBuffer.RemoveRange(stat.ObserversStartIndex, stat.ObserversCount);
                    StatsUtilities.OnObserversChanged(ref stat, statHandle, -stat.ObserversCount, ref statsBuffer);
                }
                
                // Remove all modifiers
                {
                    statModifiersBuffer.RemoveRange(stat.ModifiersStartIndex, stat.ModifiersCount);
                    StatsUtilities.OnModifiersChanged(ref stat, statHandle, -stat.ModifiersCount, ref statsBuffer);
                }
                
                // Write back stat data
                statsBuffer[statHandle.Index] = stat;
                        
                // Stat update
                ref Stat statRef = ref TryGetStatRef(statHandle, out bool statSuccess, ref _nullStat);
                UpdateStat(statHandle,
                    ref statRef,
                    ref statModifiersBuffer,
                    ref statObserversBuffer);

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStatHandlePresentDownObserversChain(StatHandle statHandle, StatHandle observerStatHandle, ref DynamicBuffer<StatObserver> statObserversBuffer)
        {
            if (statHandle != observerStatHandle)
            {
                // TODO

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureClearedValidTmpStatsList()
        {
            if (!_tmpStatsList.IsCreated)
            {
                _tmpStatsList = new NativeList<StatHandle>(Allocator.Temp);
            }
            _tmpStatsList.Clear();
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
        
        // TODO:
        // RemoveAllModifiersForStat
    }

    [InternalBufferCapacity(0)]
    public struct StatsOwner : IComponentData
    {
        public uint ModifierIDCounter;
    }

    [InternalBufferCapacity(0)]
    public struct Stat : IBufferElementData
    {
        public float BaseValue;
        public float Value;

        // TODO: how to prevent users from touching any of these fields
        public int ModifiersStartIndex;
        public int ModifiersCount;
        public int ObserversStartIndex;
        public int ObserversCount;
        
        public byte ProduceChangeEvents;
    }

    [InternalBufferCapacity(0)]
    public struct StatObserver : IBufferElementData
    {
        public StatHandle ObserverHandle;
    }

    [InternalBufferCapacity(0)]
    public struct StatChangeEvent : IBufferElementData
    {
        public StatHandle StatHandle;
        public Stat PrevValue;
        public Stat NewValue;
    }

    public struct StatHandle : IEquatable<StatHandle>
    {
        public int Index;
        public Entity Entity;
        
        public bool Equals(StatHandle other)
        {
            return Index == other.Index && Entity.Equals(other.Entity);
        }

        public override bool Equals(object obj)
        {
            return obj is StatHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Entity);
        }

        public static bool operator ==(StatHandle left, StatHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StatHandle left, StatHandle right)
        {
            return !left.Equals(right);
        }
    }
    
    public struct StatModifierHandle : IEquatable<StatModifierHandle>
    {
        public StatHandle AffectedStatHandle;
        public uint ModifierID;
        
        public bool Equals(StatModifierHandle other)
        {
            return AffectedStatHandle == other.AffectedStatHandle && ModifierID == other.ModifierID;
        }

        public override bool Equals(object obj)
        {
            return obj is StatModifierHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AffectedStatHandle, ModifierID);
        }

        public static bool operator ==(StatModifierHandle left, StatModifierHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StatModifierHandle left, StatModifierHandle right)
        {
            return !left.Equals(right);
        }
    }

    public struct StatsBakingWorld<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        internal IBaker Baker;
        internal Entity Entity;
        internal StatsOwner StatsOwner;
        internal DynamicBuffer<Stat> StatsBuffer;
        internal DynamicBuffer<TStatModifier> StatModifiersBuffer;
        internal DynamicBuffer<StatObserver> StatObserversBuffer;
        internal NativeList<StatHandle> TmpObserversStatHandles;

        public StatsBakingWorld(IBaker baker, Entity entity)
        {
            Baker = baker;
            Entity = entity;
            TmpObserversStatHandles = new NativeList<StatHandle>(Allocator.Temp);
            
            StatsOwner = new StatsOwner();
            StatsBuffer = baker.AddBuffer<Stat>(entity);
            StatModifiersBuffer = baker.AddBuffer<TStatModifier>(entity);
            StatObserversBuffer = baker.AddBuffer<StatObserver>(entity);
        }
        
        public void CreateStat(float baseValue, bool produceChangeEvents, out StatHandle statHandle)
        {
            StatsUtilities.CreateStatCommon(Entity, baseValue, produceChangeEvents, out statHandle, ref StatsBuffer);
        }

        public bool TryAddModifier(StatHandle statHandle, TStatModifier modifier,
            out StatModifierHandle statModifierHandle)
        {
            StatsUtilities.AddModifierCommonPhase1<TStatModifier, TStatModifierStack>(
                statHandle,
                ref modifier,
                ref StatsOwner,
                out statModifierHandle,
                ref TmpObserversStatHandles);

            bool modifierCanBeAdded = true;
            for (int i = 0; i < TmpObserversStatHandles.Length; i++)
            {
                StatHandle observedStatHandle = TmpObserversStatHandles[i];
                
                if (observedStatHandle.Entity != Entity)
                {
                    throw new Exception(
                        "Modifiers added during baking cannot observe stats on entities other than the baked entity");
                }
                
                if (IsStatHandlePresentDownObserversChain(observedStatHandle, statHandle))
                {
                    modifierCanBeAdded = false;
                    break;
                }
            }

            // Only add modifier if no infinite loop would be created
            if (modifierCanBeAdded)
            {
                Stat stat = StatsBuffer[statHandle.Index];
                
                StatsUtilities.AddModifierCommonPhase2<TStatModifier, TStatModifierStack>(
                    statHandle,
                    ref stat,
                    modifier,
                    in TmpObserversStatHandles,
                    ref StatsBuffer,
                    in StatModifiersBuffer,
                    in StatObserversBuffer);

                // Write back stat data
                StatsBuffer[statHandle.Index] = stat;

                // Stat update
                ref Stat statRef = ref TryGetStatRef(statHandle, out bool statSuccess, ref _nullStat);
                UpdateStat(statHandle,
                    ref statRef,
                    ref statModifiersBuffer,
                    ref statObserversBuffer);

                return true;
            }
            else
            {
                Debug.Log(
                    "Warning: stat modifier couldn't be added because it would've created an infinite stats update loop. The stat it affects would either directly or indirectly react to its own changes down the reactive stats chain.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsStatHandlePresentDownObserversChain(StatHandle statHandle, StatHandle observerStatHandle)
        {
            if (statHandle != observerStatHandle)
            {
                // TODO

                return true;
            }

            return false;
        }

        public void Finalize()
        {
            Baker.AddComponent(Entity, StatsOwner);
        }
    }

    public static class StatsUtilities
    {
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityManager entityManager)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            entityManager.AddComponentData(entity, new StatsOwner());
            entityManager.AddBuffer<Stat>(entity);
            entityManager.AddBuffer<TStatModifier>(entity);
            entityManager.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer ecb)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(entity, new StatsOwner());
            ecb.AddBuffer<Stat>(entity);
            ecb.AddBuffer<TStatModifier>(entity);
            ecb.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer.ParallelWriter ecb, int sortKey)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
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
        internal static int GetFirstModifierIndexOfStat(in Stat stat, int statIndex, in DynamicBuffer<Stat> statsBuffer)
        {
            int firstIndex = 0;
            if (stat.ModifiersStartIndex >= 0)
            {
                firstIndex = stat.ModifiersStartIndex;
            }
            // Otherwise, search the previous stats in stats buffer for the insertion stat index
            else
            {
                for (int i = statIndex - 1; i >= 0; i--)
                {
                    Stat previousStat = statsBuffer[i];
                    if (previousStat.ModifiersStartIndex >= 0)
                    {
                        Assert.IsTrue(previousStat.ModifiersCount > 0);
                        firstIndex = previousStat.ModifiersStartIndex + previousStat.ModifiersCount;
                        break;
                    }
                }
            }

            return firstIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetFirstObserverIndexOfStat(in Stat stat, int statIndex, in DynamicBuffer<Stat> statsBuffer)
        {
            int firstIndex = 0;
            if (stat.ObserversStartIndex >= 0)
            {
                firstIndex = stat.ObserversStartIndex;
            }
            // Otherwise, search the previous stats in stats buffer for the insertion stat index
            else
            {
                for (int i = statIndex - 1; i >= 0; i--)
                {
                    Stat previousStat = statsBuffer[i];
                    if (previousStat.ObserversStartIndex >= 0)
                    {
                        Assert.IsTrue(previousStat.ObserversCount > 0);
                        firstIndex = previousStat.ObserversStartIndex + previousStat.ObserversCount;
                        break;
                    }
                }
            }

            return firstIndex;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void OnModifiersChanged(ref Stat stat, StatHandle statHandle, int countChange, 
            ref DynamicBuffer<Stat> statsBuffer)
        {
            // Update count for this stat
            stat.ModifiersCount += countChange;
            Assert.IsTrue(stat.ModifiersCount >= 0);

            // Update modifier start indexes for all following stats
            for (int i = statHandle.Index + 1; i < statsBuffer.Length; i++)
            {
                ref Stat nextStatRef = ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), i);
                nextStatRef.ModifiersStartIndex += countChange;
                Assert.IsTrue(nextStatRef.ModifiersStartIndex >= 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void OnObserversChanged(ref Stat stat, StatHandle statHandle, int countChange,
            ref DynamicBuffer<Stat> statsBuffer)
        {
            // Update start index and count for this stat
            stat.ObserversCount += countChange;
            Assert.IsTrue(stat.ObserversCount >= 0);

            // Update observer start indexes for all following stats
            for (int i = statHandle.Index + 1; i < statsBuffer.Length; i++)
            {
                ref Stat nextStatRef = ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), i);
                nextStatRef.ObserversStartIndex += countChange;
                Assert.IsTrue(nextStatRef.ObserversStartIndex >= 0);
            }
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
                
                ModifiersCount = 0,
                ObserversCount = 0,
                
                ProduceChangeEvents = produceChangeEvents ? (byte)1 : (byte)0,
            };
            statHandle.Index = StatsBuffer.Length;
            
            newStat.ModifiersStartIndex = StatsUtilities.GetFirstModifierIndexOfStat(in newStat, statHandle.Index, in StatsBuffer);
            newStat.ObserversStartIndex = StatsUtilities.GetFirstObserverIndexOfStat(in newStat, statHandle.Index, in StatsBuffer);
            
            StatsBuffer.Add(newStat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddModifierCommonPhase1<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref TStatModifier modifier, 
            ref StatsOwner statsOwner,
            out StatModifierHandle statModifierHandle,
            ref NativeList<StatHandle> modifierObservedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            statModifierHandle = new StatModifierHandle
            {
                AffectedStatHandle = statHandle,
            };
                
            // Increment modifier Id (local to entity)
            statsOwner.ModifierIDCounter++;
            modifier.ID = statsOwner.ModifierIDCounter;
            statModifierHandle.ModifierID = modifier.ID;

            // Register observers on observed stats
            if (!modifierObservedStatsList.IsCreated)
            {
                modifierObservedStatsList = new NativeList<StatHandle>(Allocator.Temp);
            }
            modifierObservedStatsList.Clear();
            modifier.AddObservedStatsToList(ref modifierObservedStatsList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddModifierCommonPhase2<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref Stat stat,
            TStatModifier modifier, 
            in NativeList<StatHandle> filledModifierObservedStatsList,
            ref DynamicBuffer<Stat> statsBuffer,
            in DynamicBuffer<TStatModifier> statModifiersBuffer,
            in DynamicBuffer<StatObserver> statObserversBuffer)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            // Add modifier to the affected stat.
            {
                if (stat.ModifiersStartIndex < statModifiersBuffer.Length)
                {
                    statModifiersBuffer.Insert(stat.ModifiersStartIndex, modifier);
                }
                else
                {
                    statModifiersBuffer.Add(modifier);
                }

                StatsUtilities.OnModifiersChanged(ref stat, statHandle, 1, ref statsBuffer);
            }

            // Add observed stats of the modifier as observers of the affected stat
            {
                // Reminder: the tmp stats list still holds all observed stats of the modifier
                int addedObserversCount = filledModifierObservedStatsList.Length;
                for (int i = 0; i < filledModifierObservedStatsList.Length; i++)
                {
                    StatHandle observedStatHandle = filledModifierObservedStatsList[i];
                    if (stat.ObserversStartIndex < statObserversBuffer.Length)
                    {
                        statObserversBuffer.Insert(stat.ObserversStartIndex, new StatObserver
                        {
                            ObserverHandle = observedStatHandle,
                        });
                    }
                    else
                    {
                        statObserversBuffer.Add(new StatObserver
                        {
                            ObserverHandle = observedStatHandle,
                        });
                    }
                }

                StatsUtilities.OnObserversChanged(ref stat, statHandle, addedObserversCount, ref statsBuffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateStatCommon<TStatModifier, TStatModifierStack>(StatHandle statHandle,
            ref Stat statRef,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Stat initialStat = statRef;

            _statsReader.UpdateCacheData(_latestStatsEntity, _latestStatsBuffer);
            
            // Apply Modifiers
            TStatModifierStack modifierStack = new TStatModifierStack();
            modifierStack.Reset();
            for (int m = statRef.ModifiersStartIndex; m < statRef.ModifiersStartIndex + statRef.ModifiersCount; m++)
            {
                TStatModifier modifier = statModifiersBuffer[m];
                modifier.Apply(
                    in _statsReader,
                    ref modifierStack);
                // TODO: give a way to say "the modifier depends on a now invalid stat and must be removed"
            }

            modifierStack.Apply(ref statRef.BaseValue, ref statRef.Value);

            // Stat change events
            if (statRef.ProduceChangeEvents == 1 && StatChangeEventsList.IsCreated)
            {
                StatChangeEventsList.Add(new StatChangeEvent
                {
                    StatHandle = statHandle,
                    PrevValue = initialStat,
                    NewValue = statRef,
                });
            }

            // Notify Observers
            for (int o = statRef.ObserversStartIndex; o < statRef.ObserversStartIndex + statRef.ObserversCount; o++)
            {
                StatObserver observer = statObserversBuffer[o];
                TryUpdateStat(observer.ObserverHandle); // TODO: try not to have a recursive update. Add to some list then update the list instead
            }
        }
    }

    /// <summary>
    /// Useful for making fast stat changes, potentially in parallel,
    /// and then deferring the stats update to a later single-thread job
    /// NOTE: clears the list.
    /// </summary>
    [BurstCompile]
    public struct DeferredStatsUpdateListJob<TStatModifier, TStatModifierStack> : IJob
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public StatsWriter<TStatModifier, TStatModifierStack> StatsWriter;
        public NativeList<StatHandle> StatsToUpdate;
        
        public void Execute()
        {
            for (int i = 0; i < StatsToUpdate.Length; i++)
            {
                StatsWriter.TryUpdateStat(StatsToUpdate[i]);
            }
            StatsToUpdate.Clear();
        }
    }

    /// <summary>
    /// Useful for making fast stat changes, potentially in parallel,
    /// and then deferring the stats update to a later single-thread job.
    /// NOTE: clears the queue.
    /// </summary>
    [BurstCompile]
    public struct DeferredStatsUpdateQueueJob<TStatModifier, TStatModifierStack> : IJob
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public StatsWriter<TStatModifier, TStatModifierStack> StatsWriter;
        public NativeQueue<StatHandle> StatsToUpdate;
        
        public void Execute()
        {
            while(StatsToUpdate.TryDequeue(out StatHandle statHandle))
            {
                StatsWriter.TryUpdateStat(statHandle);
            }
            StatsToUpdate.Clear();
        }
    }

    /// <summary>
    /// Useful for making fast stat changes, potentially in parallel,
    /// and then deferring the stats update to a later single-thread job.
    /// NOTE: you must dispose the stream afterwards.
    /// </summary>
    [BurstCompile]
    public struct DeferredStatsUpdateStreamJob<TStatModifier, TStatModifierStack> : IJob
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public StatsWriter<TStatModifier, TStatModifierStack> StatsWriter;
        public NativeStream.Reader StatsToUpdate;
        
        public void Execute()
        {
            for (int i = 0; i < StatsToUpdate.ForEachCount; i++)
            {
                StatsToUpdate.BeginForEachIndex(i);
                while (StatsToUpdate.RemainingItemCount > 0)
                {
                    StatHandle statHandle = StatsToUpdate.Read<StatHandle>();
                    StatsWriter.TryUpdateStat(statHandle);
                }
                StatsToUpdate.EndForEachIndex();
            }
        }
    }
}