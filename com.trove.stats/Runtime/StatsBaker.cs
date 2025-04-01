using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Trove.Stats
{
    public struct StatsBaker<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        internal IBaker Baker;
        internal Entity Entity;
        
        internal StatsOwner StatsOwner;
        internal DynamicBuffer<Stat> StatsBuffer;
        internal DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> StatModifiersBuffer;
        internal DynamicBuffer<StatObserver> StatObserversBuffer;

        public void CreateStat(float baseValue, bool produceChangeEvents, out StatHandle statHandle)
        {
            StatsUtilities.CreateStat(Entity, baseValue, produceChangeEvents, ref StatsBuffer, out statHandle);
        }
    }
}