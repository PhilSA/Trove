using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;

namespace Trove.Stats
{
    public struct StatsBaker<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        internal IBaker Baker;
        internal Entity Entity;
        internal StatsOwner StatsOwner;
        internal DynamicBuffer<Stat> StatsBuffer;
        internal DynamicBuffer<TStatModifier> StatModifiersBuffer;
        internal DynamicBuffer<StatObserver> StatObserversBuffer;
        internal NativeList<StatHandle> TmpObserversStatHandles;

        public StatsBaker(IBaker baker, Entity entity)
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
            // TODO
            // StatsUtilities.AddModifierCommonPhase1<TStatModifier, TStatModifierStack>(
            //     statHandle,
            //     ref modifier,
            //     ref StatsOwner,
            //     out statModifierHandle,
            //     ref TmpObserversStatHandles);
            //
            // bool modifierCanBeAdded = true;
            // for (int i = 0; i < TmpObserversStatHandles.Length; i++)
            // {
            //     StatHandle observedStatHandle = TmpObserversStatHandles[i];
            //     
            //     if (observedStatHandle.Entity != Entity)
            //     {
            //         throw new Exception(
            //             "Modifiers added during baking cannot observe stats on entities other than the baked entity");
            //     }
            //     
            //     if (IsStatHandlePresentDownObserversChain(observedStatHandle, statHandle))
            //     {
            //         modifierCanBeAdded = false;
            //         break;
            //     }
            // }
            //
            // // Only add modifier if no infinite loop would be created
            // if (modifierCanBeAdded)
            // {
            //     Stat stat = StatsBuffer[statHandle.Index];
            //     
            //     StatsUtilities.AddModifierCommonPhase2<TStatModifier, TStatModifierStack>(
            //         statHandle,
            //         ref stat,
            //         modifier,
            //         in TmpObserversStatHandles,
            //         ref StatsBuffer,
            //         in StatModifiersBuffer,
            //         in StatObserversBuffer);
            //
            //     // Write back stat data
            //     StatsBuffer[statHandle.Index] = stat;
            //
            //     // TODO:
            //     // Stat update
            //     // ref Stat statRef = ref TryGetStatRef(statHandle, out bool statSuccess, ref _nullStat);
            //     // UpdateStat(statHandle,
            //     //     ref statRef,
            //     //     ref statModifiersBuffer,
            //     //     ref statObserversBuffer);
            //
            //     return true;
            // }
            // else
            // {
            //     Debug.Log(
            //         "Warning: stat modifier couldn't be added because it would've created an infinite stats update loop. The stat it affects would either directly or indirectly react to its own changes down the reactive stats chain.");
            // }

            statModifierHandle = default;
            return false;
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

}