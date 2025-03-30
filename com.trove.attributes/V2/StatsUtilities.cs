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
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            baker.AddComponent(entity,new StatsOwner());
            statsBaker =  new StatsBaker<TStatModifier, TStatModifierStack>
            {
                Baker = baker,
                Entity = entity,

                StatsOwner = default,
                StatsBuffer = baker.AddBuffer<Stat>(entity),
                StatModifiersBuffer = baker.AddBuffer<TStatModifier>(entity),
                StatObserversBuffer = baker.AddBuffer<StatObserver>(entity),
            };
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityManager entityManager)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            entityManager.AddComponentData(entity, new StatsOwner());
            entityManager.AddBuffer<Stat>(entity);
            entityManager.AddBuffer<TStatModifier>(entity);
            entityManager.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer ecb)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(entity, new StatsOwner());
            ecb.AddBuffer<Stat>(entity);
            ecb.AddBuffer<TStatModifier>(entity);
            ecb.AddBuffer<StatObserver>(entity);
        }
        
        public static void AddStatsComponents<TStatModifier, TStatModifierStack>(Entity entity, EntityCommandBuffer.ParallelWriter ecb, int sortKey)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            ecb.AddComponent(sortKey, entity, new StatsOwner());
            ecb.AddBuffer<Stat>(sortKey, entity);
            ecb.AddBuffer<TStatModifier>(sortKey, entity);
            ecb.AddBuffer<StatObserver>(sortKey, entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool CreateStat(
            Entity entity, 
            float baseValue, 
            bool produceChangeEvents, 
            ref StatsOwner statsOwner,
            ref DynamicBuffer<Stat> statsBuffer,
            out StatHandle statHandle)
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
                
                LastModifierIndex = -1,
                LastObserverIndex = -1,
                
                ProduceChangeEvents = produceChangeEvents ? (byte)1 : (byte)0,
            };
            
            if (statsOwner.FastStatsStorage.HasRoom())
            {
                statHandle.Index = statsOwner.FastStatsStorage.Length;
                statsOwner.FastStatsStorage.Add(newStat);
                return true;
            }
            else
            {
                statHandle.Index = FastStatsStorage.Capacity + statsBuffer.Length;
                statsBuffer.Add(newStat);
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateSingleStatCommon<TStatModifier, TStatModifierStack>(
            StatHandle statHandle,
            ref StatsHandler statsHandler,
            ref Stat statRef,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref NativeList<StatChangeEvent> statChangeEventsList,
            ref NativeList<StatHandle> tmpUpdatedStatsList)
            where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData, ICompactMultiLinkedListElement
            where TStatModifierStack : unmanaged, IStatsModifierStack
        {
            Stat initialStat = statRef;
            
            // Apply Modifiers
            TStatModifierStack modifierStack = new TStatModifierStack();
            modifierStack.Reset();
            if (statRef.LastModifierIndex >= 0)
            {
                CompactMultiLinkedListIterator<TStatModifier> modifiersIterator =
                    new CompactMultiLinkedListIterator<TStatModifier>(statRef.LastModifierIndex);
                while (modifiersIterator.GetNext(in statModifiersBuffer, out TStatModifier modifier,
                           out int modifierIndex))
                {
                    modifier.Apply(
                        ref statsHandler,
                        ref modifierStack);
                    // TODO: give a way to say "the modifier depends on a now invalid stat and must be removed"
                }
            }
            modifierStack.Apply(in statRef.BaseValue, ref statRef.Value);

            // If the stat value really changed
            if (initialStat.Value != statRef.Value)
            {
                // Stat change events
                if (statRef.ProduceChangeEvents == 1 && statChangeEventsList.IsCreated)
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
                        tmpUpdatedStatsList.Add(observer.ObserverHandle);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void EnsureClearedValidTempList<T>(ref NativeList<T> list) where T : unmanaged
        {
            if (!list.IsCreated)
            {
                list = new NativeList<T>(Allocator.Temp);
            }
            list.Clear();
        }

        internal static void AddObserversOfStatToList(
            in Stat stat,
            in DynamicBuffer<StatObserver> statObserversBufferOnStatEntity,
            ref NativeList<StatObserver> statObserversList)
        {
            if (stat.LastObserverIndex >= 0)
            {
                CompactMultiLinkedListIterator<StatObserver> observersIterator =
                    new CompactMultiLinkedListIterator<StatObserver>(stat.LastObserverIndex);
                while (observersIterator.GetNext(in statObserversBufferOnStatEntity,
                           out StatObserver observerOfStat, out int observerIndex))
                {
                    statObserversList.Add(observerOfStat);
                }
            }
        }

        internal static void AddStatAsObserverOfOtherStat(
            StatHandle observerStatHandle, 
            StatHandle observedStatHandle,
            ref StatsOwner statsOwnerOfObservedStat,
            ref SingleEntityStatsHandler statsHandlerForObservedStat,
            ref DynamicBuffer<StatObserver> statObserversBufferOnObservedStatEntity)
        {
            Assert.IsTrue(observerStatHandle.Entity != Entity.Null);

            if (statsHandlerForObservedStat.TryGetStat(observedStatHandle, in statsOwnerOfObservedStat, out Stat observedStat))
            {
                CollectionUtilities.AddToCompactMultiLinkedList(
                    ref statObserversBufferOnObservedStatEntity,
                    ref observedStat.LastObserverIndex, 
                    new StatObserver { ObserverHandle = observerStatHandle });

                statsHandlerForObservedStat.TrySetStat(observedStatHandle, observedStat, ref statsOwnerOfObservedStat);
            }
            // TODO: else?
        }
    }

    /// <summary>
    /// Iterates a specified linked list in a dynamic buffer containing multiple linked lists.
    /// Also allows removing elements during iteration.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct CompactMultiLinkedListIterator<T> where T : unmanaged, ICompactMultiLinkedListElement
    {
        private int _iteratedElementIndex;
        private int _prevIteratedElementIndex;
        private T _iteratedElement;
        
        /// <summary>
        /// Create the iterator
        /// </summary>
        public CompactMultiLinkedListIterator(int linkedListLastIndex)
        {
            _iteratedElementIndex = linkedListLastIndex;
            _prevIteratedElementIndex = -1;
            _iteratedElement = default;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetNext(in DynamicBuffer<T> multiLinkedListsBuffer, out T element, out int elementIndex)
        {
            if (_iteratedElementIndex >= 0)
            {
                _iteratedElement = multiLinkedListsBuffer[_iteratedElementIndex];

                element = _iteratedElement;
                elementIndex = _iteratedElementIndex;
                
                // Move to next index but remember previous (used for removing)
                _prevIteratedElementIndex = _iteratedElementIndex;
                _iteratedElementIndex = _iteratedElement.PrevElementIndex;
                
                return true;
            }

            element = default;
            elementIndex = -1;
            return false;
        }

        /// <summary>
        /// Note: will update the last indexes in the linkedListLastIndexes following removal.
        /// Note: GetNext() must be called before this can be used.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveCurrentIteratedElementAndUpdateIndexes(
            ref DynamicBuffer<T> multiLinkedListsBuffer, 
            ref NativeArray<int> linkedListLastIndexes,
            out int firstUpdatedLastIndexIndex)
        {
            firstUpdatedLastIndexIndex = -1;
            int removedElementIndex = _prevIteratedElementIndex;

            if (removedElementIndex < 0)
            {
                return;
            }

            T removedElement = _iteratedElement;
            
            // Remove element
            multiLinkedListsBuffer.RemoveAt(removedElementIndex);

            // Iterate all last indexes and update them 
            for (int i = 0; i < linkedListLastIndexes.Length; i++)
            {
                int tmpLastIndex = linkedListLastIndexes[i];
                
                // If the iterated last index is greater than the removed index, decrement it
                if (tmpLastIndex > removedElementIndex)
                {
                    tmpLastIndex -= 1;
                    linkedListLastIndexes[i] = tmpLastIndex;
                    if (firstUpdatedLastIndexIndex < 0)
                    {
                        firstUpdatedLastIndexIndex = i;
                    }
                }
                // If the iterated last index is the one we removed, update it with the prev index of the removed element
                else if (tmpLastIndex == removedElementIndex)
                {
                    linkedListLastIndexes[i] = removedElement.PrevElementIndex;
                    if (firstUpdatedLastIndexIndex < 0)
                    {
                        firstUpdatedLastIndexIndex = i;
                    }
                }
            }

            // Iterate all buffer elements starting from the removed index to update their prev indexes
            for (int i = _iteratedElementIndex; i < multiLinkedListsBuffer.Length; i++)
            {
                T iteratedElement = multiLinkedListsBuffer[i];
                
                // If the prev index of this element is greater than the removed one, decrement it
                if (iteratedElement.PrevElementIndex > removedElementIndex)
                {
                    iteratedElement.PrevElementIndex -= 1;
                    multiLinkedListsBuffer[i] = iteratedElement;
                }
                // If the prev index of this element was the removed one, change its prev index to the removed one's
                // prev index.
                else if (iteratedElement.PrevElementIndex == removedElementIndex)
                {
                    iteratedElement.PrevElementIndex = removedElement.PrevElementIndex;
                    multiLinkedListsBuffer[i] = iteratedElement;
                }
            }
        }
    }
}