using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Trove.Stats
{
    public struct StatsBaker<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IBufferElementData, IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        internal IBaker Baker;
        internal Entity Entity;
        
        internal StatsOwner StatsOwner;
        internal DynamicBuffer<Stat> StatsBuffer;
        internal DynamicBuffer<TStatModifier> StatModifiersBuffer;
        internal DynamicBuffer<StatObserver> StatObserversBuffer;

        public void CreateStat(float baseValue, bool produceChangeEvents, out StatHandle statHandle)
        {
            StatsUtilities.CreateStat(Entity, baseValue, produceChangeEvents, ref StatsBuffer, out statHandle);
        }
        
        public bool TryAddStatModifier(StatHandle affectedStatHandle, TStatModifier modifier, out StatModifierHandle statModifierHandle)
        {
            // Cancel if the affected stat is not on this entity
            if (affectedStatHandle.Entity != Entity)
            {
                statModifierHandle = default;
                return false;
            }
            
            // Cancel if the modifier involves stats of any other entity
            NativeList<StatHandle> tmpObservedStatHandles = new NativeList<StatHandle>(Allocator.Temp);
            modifier.AddObservedStatsToList(ref tmpObservedStatHandles);
            for (int i = 0; i < tmpObservedStatHandles.Length; ++i)
            {
                if (tmpObservedStatHandles[i].Entity != Entity)
                {
                    statModifierHandle = default;
                    return false;
                }
            }
            tmpObservedStatHandles.Dispose();
            
            StatsWorldData<TStatModifier, TStatModifierStack> statsWorldData =
                new StatsWorldData<TStatModifier, TStatModifierStack>(Allocator.Persistent);
            StatsAccessor<TStatModifier, TStatModifierStack> statsAccessor =
                StatsAccessor<TStatModifier, TStatModifierStack>.CreateForBaking();

            bool success = statsAccessor.TryAddStatModifierSingleEntity(
                affectedStatHandle,
                modifier,
                ref StatsOwner,
                ref StatsBuffer,
                ref StatModifiersBuffer,
                ref StatObserversBuffer,
                out statModifierHandle,
                ref statsWorldData);

            Baker.SetComponent(Entity, StatsOwner);

            statsWorldData.Dispose();

            return success;
        }
    }
}