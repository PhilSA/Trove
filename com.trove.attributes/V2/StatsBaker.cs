using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Trove.Stats
{
    public struct StatsBaker<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        private IBaker Baker;
        private Entity Entity;
        
        private StatsOwner StatsOwner;
        private DynamicBuffer<Stat> StatsBuffer;
        private DynamicBuffer<TStatModifier> StatModifiersBuffer;
        private DynamicBuffer<StatObserver> StatObserversBuffer;

        private NativeList<StatHandle> _tmpUpdatedStatsList;
        private NativeList<StatHandle> _tmpModifierObservedStatsList;
        private NativeList<StatObserver> _tmpStatObserversList;

        public StatsBaker(IBaker baker, Entity entity)
        {
            Baker = baker;
            Entity = entity;
            
            StatsOwner = default;
            StatsBuffer = default;
            StatModifiersBuffer = default;
            StatObserversBuffer = default;

            _tmpUpdatedStatsList = default;
            _tmpModifierObservedStatsList = default;
            _tmpStatObserversList = default;
        }

        public void AddComponents()
        {
            Baker.AddComponent(Entity, StatsOwner);
            StatsBuffer = Baker.AddBuffer<Stat>(Entity);
            StatModifiersBuffer = Baker.AddBuffer<TStatModifier>(Entity);
            StatObserversBuffer = Baker.AddBuffer<StatObserver>(Entity);
        }
        
        public void CreateStat(float baseValue, bool produceChangeEvents, out StatHandle statHandle)
        {
            StatsUtilities.CreateStatCommon(Entity, baseValue, produceChangeEvents, out statHandle, ref StatsBuffer);
        }

        public bool TryAddModifier(StatHandle affectedStatHandle, TStatModifier modifier,
            out StatModifierHandle statModifierHandle)
        {
            // Ensure lists are created and cleared
            StatsUtilities.EnsureClearedValidTempList(ref _tmpModifierObservedStatsList);
            StatsUtilities.EnsureClearedValidTempList(ref _tmpStatObserversList);
            
            StatsUtilities.AddModifierPhase1<TStatModifier, TStatModifierStack>(
                affectedStatHandle,
                ref StatsOwner,
                ref modifier,
                ref _tmpModifierObservedStatsList,
                out statModifierHandle);
            
            Baker.SetComponent(Entity, StatsOwner);
                
            // In baking, don't allow observing stats from other entities
            for (int i = 0; i < _tmpModifierObservedStatsList.Length; i++)
            {
                StatHandle observedStatHandle = _tmpModifierObservedStatsList[i];
        
                if (observedStatHandle.Entity != affectedStatHandle.Entity)
                {
                    throw new Exception(
                        "Adding stat modifiers that observe stats of entities other than the baked entity is not allowed during baking.");
                    return false;
                }
            }
                
            BufferLookup<Stat> mockStatsLookup = default;
            BufferLookup<StatObserver> mockStatObserversLookup = default;
            
            bool modifierAdded = StatsUtilities.AddModifierPhase2<TStatModifier, TStatModifierStack>(
                true,
                affectedStatHandle,
                in modifier,
                ref StatsBuffer,
                ref StatModifiersBuffer,
                ref StatObserversBuffer,
                ref mockStatsLookup,
                ref mockStatObserversLookup,
                ref _tmpModifierObservedStatsList,
                ref _tmpStatObserversList);

            if (modifierAdded)
            {
                // Update stat following modifier add
                UpdateStat(affectedStatHandle);
                
                return true;
            }

            statModifierHandle = default;
            return false;
        }

        private unsafe void UpdateStat(StatHandle statHandle)
        {
            StatsUtilities.EnsureClearedValidTempList(ref _tmpUpdatedStatsList);
            _tmpUpdatedStatsList.Add(statHandle);
            
            NativeList<StatChangeEvent> mockStatChangeEventsList = default;

            for (int i = 0; i < _tmpUpdatedStatsList.Length; i++)
            {
                if (statHandle.Index < StatsBuffer.Length)
                {
                    ref Stat statRef =
                        ref UnsafeUtility.ArrayElementAsRef<Stat>(StatsBuffer.GetUnsafePtr(), statHandle.Index);

                    StatValueReader statValueReader = new StatValueReader(StatsBuffer);
                    StatsUtilities.UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
                        statHandle,
                        ref statValueReader,
                        ref statRef,
                        ref StatModifiersBuffer,
                        ref StatObserversBuffer,
                        ref mockStatChangeEventsList,
                        ref _tmpUpdatedStatsList);
                }
            }
        }
    }
}