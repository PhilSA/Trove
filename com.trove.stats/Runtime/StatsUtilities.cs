using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Trove.Stats
{
    public static class StatsUtilities
    {
        public static void BakeStatsComponents<TStatModifier, TStatModifierStack>(IBaker baker, Entity entity, out StatsBaker<TStatModifier, TStatModifierStack> statsBaker)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            baker.AddComponent(entity,new StatsOwner());
            statsBaker =  new StatsBaker<TStatModifier, TStatModifierStack>
            {
                Baker = baker,
                Entity = entity,

                StatsOwner = default,
                StatsBuffer = baker.AddBuffer<Stat>(entity),
                StatModifiersBuffer = baker.AddBuffer<StatModifier<TStatModifier, TStatModifierStack>>(entity),
                StatObserversBuffer = baker.AddBuffer<StatObserver>(entity),
            };
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityManager entityManager)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            entityManager.AddComponentData(entity, new StatsOwner());
            entityManager.AddBuffer<Stat>(entity);
            entityManager.AddBuffer<StatModifier<TStatModifier, TStatModifierStack>>(entity);
            entityManager.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer ecb)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(entity, new StatsOwner());
            ecb.AddBuffer<Stat>(entity);
            ecb.AddBuffer<StatModifier<TStatModifier, TStatModifierStack>>(entity);
            ecb.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer.ParallelWriter ecb, int sortKey)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(sortKey, entity, new StatsOwner());
            ecb.AddBuffer<Stat>(sortKey, entity);
            ecb.AddBuffer<StatModifier<TStatModifier, TStatModifierStack>>(sortKey, entity);
            ecb.AddBuffer<StatObserver>(sortKey, entity);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AsInt(float floatValue)
        {
            return UnsafeUtility.As<float, int>(ref floatValue);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AsFloat(int intValue)
        {
            return UnsafeUtility.As<int, float>(ref intValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CreateStatCommon(
            Entity entity, 
            float baseValue, 
            bool produceChangeEvents,
            out Stat newStat,
            out StatHandle statHandle)
        {
            statHandle = new StatHandle
            {
                Entity = entity,
                Index = -1,
            };

            newStat = new Stat
            {
                BaseValue = baseValue,
                Value = baseValue,
                
                LastModifierIndex = -1,
                LastObserverIndex = -1,
                
                ProduceChangeEvents = produceChangeEvents ? (byte)1 : (byte)0,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateStat(
            Entity entity, 
            float baseValue, 
            bool produceChangeEvents, 
            ref DynamicBuffer<Stat> statsBuffer,
            out StatHandle statHandle)
        {
            CreateStatCommon(entity, baseValue, produceChangeEvents, out Stat newStat, out statHandle);
            
            statHandle.Index = statsBuffer.Length;
            statsBuffer.Add(newStat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref StatsReader statsReader,
            ref Stat statRef,
            ref DynamicBuffer<StatModifier<TStatModifier, TStatModifierStack>> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref NativeList<StatChangeEvent> statChangeEventsList,
            ref UnsafeList<StatHandle> tmpUpdatedStatsList,
            ref NativeList<StatModifierHandle> modifierTriggerEventsList,
            bool supportStatChangeEvents,
            bool supportModifierTriggerEvents)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Stat initialStat = statRef;
            
            // Apply Modifiers
            TStatModifierStack modifierStack = new TStatModifierStack();
            modifierStack.Reset();
            if (statRef.LastModifierIndex >= 0)
            {
                CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>> modifiersIterator =
                    new CompactMultiLinkedListIterator<StatModifier<TStatModifier, TStatModifierStack>>(statRef.LastModifierIndex);
                while (modifiersIterator.GetNext(in statModifiersBuffer, out StatModifier<TStatModifier, TStatModifierStack> modifier,
                           out int modifierIndex))
                {
                    modifier.Modifier.Apply(
                        ref statsReader,
                        ref modifierStack,
                        out bool addModifierTriggerEvent);
                    
                    // Write back modifier data (TODO: make optional? Or change by ref?)
                    statModifiersBuffer[modifierIndex] = modifier;
                        
                    // Handle modifier trigger events
                    if (addModifierTriggerEvent && supportModifierTriggerEvents && modifierTriggerEventsList.IsCreated)
                    {
                        modifierTriggerEventsList.Add(new StatModifierHandle
                        {
                            ModifierID = modifier.ID,
                            AffectedStatHandle = statHandle,
                        });
                    }
                }
            }
            modifierStack.Apply(in statRef.BaseValue, ref statRef.Value);

            // If the stat value really changed
            if (initialStat.Value != statRef.Value)
            {
                // Stat change events
                if (statRef.ProduceChangeEvents == 1 && supportStatChangeEvents && statChangeEventsList.IsCreated)
                {
                    statChangeEventsList.Add(new StatChangeEvent
                    {
                        StatHandle = statHandle,
                        PrevValue = initialStat,
                        NewValue = statRef,
                    });
                }

                // Notify Observers (add to update list)
                if (statRef.LastObserverIndex >= 0)
                {
                    CompactMultiLinkedListIterator<StatObserver> observersIterator =
                        new CompactMultiLinkedListIterator<StatObserver>(statRef.LastObserverIndex);
                    while (observersIterator.GetNext(in statObserversBuffer, out StatObserver observer,
                               out int observerIndex))
                    {
                        tmpUpdatedStatsList.AddWithGrowFactor(observer.ObserverHandle);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void EnsureClearedValidTempList<T>(ref UnsafeList<T> list) where T : unmanaged
        {
            if (!list.IsCreated)
            {
                list = new UnsafeList<T>(8, Allocator.Temp);
            }
            list.Clear();
        }

        internal static void AddObserversOfStatToList(
            in Stat stat,
            in DynamicBuffer<StatObserver> statObserversBufferOnStatEntity,
            ref UnsafeList<StatObserver> statObserversList)
        {
            if (stat.LastObserverIndex >= 0)
            {
                CompactMultiLinkedListIterator<StatObserver> observersIterator =
                    new CompactMultiLinkedListIterator<StatObserver>(stat.LastObserverIndex);
                while (observersIterator.GetNext(in statObserversBufferOnStatEntity,
                           out StatObserver observerOfStat, out int observerIndex))
                {
                    statObserversList.AddWithGrowFactor(observerOfStat);
                }
            }
        }

        internal static void AddStatAsObserverOfOtherStat(
            StatHandle observerStatHandle, 
            StatHandle observedStatHandle,
            ref DynamicBuffer<Stat> statsBufferOnObservedStat,
            ref DynamicBuffer<StatObserver> statObserversBufferOnObservedStatEntity)
        {
            Assert.IsTrue(observerStatHandle.Entity != Entity.Null);

            if (observedStatHandle.Index < statsBufferOnObservedStat.Length)
            {
                Stat observedStat = statsBufferOnObservedStat[observedStatHandle.Index];
                
                CollectionUtilities.AddToCompactMultiLinkedList(
                    ref statObserversBufferOnObservedStatEntity,
                    ref observedStat.LastObserverIndex, 
                    new StatObserver { ObserverHandle = observerStatHandle });
                
                statsBufferOnObservedStat[observedStatHandle.Index] = observedStat;
            }
            // TODO: else?
        }

        /// <summary>
        /// Note: Assumes the "statsBuffer" is on the entity of the statHandle
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetStat(StatHandle statHandle, ref DynamicBuffer<Stat> statsBuffer, out float value, out float baseValue)
        {
            if (statHandle.Index < statsBuffer.Length)
            {
                Stat stat = statsBuffer[statHandle.Index];
                value = stat.Value;
                baseValue = stat.BaseValue;
                return true;
            }

            value = default;
            baseValue = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetStat(StatHandle statHandle, ref BufferLookup<Stat> statsBufferLookup, out Stat stat)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
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
        public static bool TryGetStat(StatHandle statHandle, ref BufferLookup<Stat> statsBufferLookup, out float value, out float baseValue)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                return TryGetStat(statHandle, ref statsBuffer, out value, out baseValue);
            }

            value = default;
            baseValue = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ref Stat GetStatRefUnsafe(StatHandle statHandle, ref BufferLookup<Stat> statsBufferLookup, out bool success, ref Stat nullResult)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out DynamicBuffer<Stat> statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), statHandle.Index);
                }
            }

            success = false;
            return ref nullResult;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ref Stat GetStatRefUnsafe(StatHandle statHandle, ref DynamicBuffer<Stat> statsBuffer, out bool success, ref Stat nullResult)
        {
            if (statHandle.Index < statsBuffer.Length)
            {
                success = true;
                return ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), statHandle.Index);
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ref Stat GetStatRefWithBufferUnsafe(StatHandle statHandle, ref BufferLookup<Stat> statsBufferLookup, out DynamicBuffer<Stat> statsBuffer, out bool success, ref Stat nullResult)
        {
            if (statsBufferLookup.TryGetBuffer(statHandle.Entity, out statsBuffer))
            {
                if (statHandle.Index < statsBuffer.Length)
                {
                    success = true;
                    return ref UnsafeUtility.ArrayElementAsRef<Stat>(statsBuffer.GetUnsafePtr(), statHandle.Index);
                }
            }

            success = false;
            return ref nullResult;
        }
    }
}