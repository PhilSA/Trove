using System.Runtime.CompilerServices;
using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;

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
        public int PrevModifierIndex { get; set; } // TODO: is it ok for set to be public
        public void AddObservedStatsToList(ref NativeList<StatHandle> observedStatHandles);
        public void Apply(
            in StatValueReader statValueReader,
            ref TStatModifierStack stack);
    }

    internal struct StatObserversHandler
    {
        private bool _isForBaking;

        private BufferLookup<Stat> _statsLookup;
        private Entity _latestStatsBufferEntity;
        private DynamicBuffer<Stat> _latestStatsBufferOnObservedStat;

        private BufferLookup<StatObserver> _statObserversLookup;
        private Entity _latestStatObserversBufferEntity;
        private DynamicBuffer<StatObserver> _latestStatObserversBufferOnObservedStat;

        // For runtime
        internal StatObserversHandler(
            BufferLookup<Stat> statsLookup,
            Entity latestStatsBufferEntity,
            DynamicBuffer<Stat> latestStatsBufferOnObservedStat,
            BufferLookup<StatObserver> statObserversLookup,
            Entity latestStatObserversBufferEntity,
            DynamicBuffer<StatObserver> latestStatObserversBufferOnObservedStat)
        {
            _isForBaking = false;

            _statsLookup = statsLookup;
            _latestStatsBufferEntity = latestStatsBufferEntity;
            _latestStatsBufferOnObservedStat = latestStatsBufferOnObservedStat;

            _statObserversLookup = statObserversLookup;
            _latestStatObserversBufferEntity = latestStatObserversBufferEntity;
            _latestStatObserversBufferOnObservedStat = latestStatObserversBufferOnObservedStat;
        }

        // For baking
        internal StatObserversHandler(
            DynamicBuffer<Stat> statsBufferOnObservedStatEntity,
            DynamicBuffer<StatObserver> statObserversBufferOnObservedEntity)
        {
            _isForBaking = true;

            _statsLookup = default;
            _latestStatsBufferEntity = default;
            _latestStatsBufferOnObservedStat = statsBufferOnObservedStatEntity;

            _statObserversLookup = default;
            _latestStatObserversBufferEntity = default;
            _latestStatObserversBufferOnObservedStat = statObserversBufferOnObservedEntity;
        }

        internal void AddObserversOfStatToList(StatHandle statHandle, ref NativeList<StatObserver> statObserversList)
        {
            Assert.IsTrue(statHandle.Entity != Entity.Null);

            if (!_isForBaking)
            {
                UpdateBuffers(statHandle.Entity);
            }

            if (statHandle.Index < _latestStatsBufferOnObservedStat.Length)
            {
                Stat stat = _latestStatsBufferOnObservedStat[statHandle.Index];

                int iteratedPrevObserverIndex = stat.LastObserverIndex;
                while (iteratedPrevObserverIndex >= 0)
                {
                    StatObserver statObserver = _latestStatObserversBufferOnObservedStat[iteratedPrevObserverIndex];
                    statObserversList.Add(statObserver);
                    iteratedPrevObserverIndex = statObserver.PrevObserverIndex;
                }
            }
            // TODO: else? 
        }

        internal bool AddStatAsObserverOfOtherStat(StatHandle observerStatHandle, StatHandle observedStatHandle)
        {
            Assert.IsTrue(observerStatHandle.Entity != Entity.Null);

            // When we're not in baking, we have to update our observers buffer based on the observed stat entity.
            if (!_isForBaking)
            {
                bool updateBuffersSuccess = UpdateBuffers(observedStatHandle.Entity);
                if (!updateBuffersSuccess)
                {
                    return false;
                }
            }
            // In baking, we always assume we're staying on the same observers buffer.
            else
            {
                Assert.IsTrue(observerStatHandle.Entity == observedStatHandle.Entity);
            }

            Stat observedStat = _latestStatsBufferOnObservedStat[observedStatHandle.Index];

            // Add observer at the end of the buffer, and remember the previous observer index
            int observerAddIndex = _latestStatObserversBufferOnObservedStat.Length;
            _latestStatObserversBufferOnObservedStat.Add(new StatObserver
            {
                PrevObserverIndex = observedStat.LastObserverIndex,
                ObserverHandle = observerStatHandle,
            });

            // Update the last observer index for the observed stat
            observedStat.LastObserverIndex = observerAddIndex;
            _latestStatsBufferOnObservedStat[observedStatHandle.Index] = observedStat;

            return true;
        }

        internal bool UpdateBuffers(Entity forStatsEntity)
        {
            bool gotValidBuffers = true;
            
            // Stats
            if (forStatsEntity != _latestStatsBufferEntity)
            {
                gotValidBuffers = _statsLookup.TryGetBuffer(forStatsEntity,
                    out _latestStatsBufferOnObservedStat);
                _latestStatsBufferEntity = forStatsEntity;
            }

            // StatObservers
            if (forStatsEntity != _latestStatObserversBufferEntity)
            {
                gotValidBuffers = gotValidBuffers && _statObserversLookup.TryGetBuffer(forStatsEntity,
                    out _latestStatObserversBufferOnObservedStat);
                _latestStatObserversBufferEntity = forStatsEntity;
            }

            return gotValidBuffers;
        }
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
        public int LastModifierIndex;
        public int LastObserverIndex;
        
        public byte ProduceChangeEvents;
    }

    [InternalBufferCapacity(0)]
    public struct StatObserver : IBufferElementData, ICompactMultiLinkedListElement
    {
        public StatHandle ObserverHandle;
        public int PrevElementIndex { get; set; }
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
}