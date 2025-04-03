using System.Runtime.CompilerServices;
using System;
using System.Runtime.InteropServices;
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
        public void Apply(in float statBaseValue, ref float statValue);
    }

    public interface IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public void AddObservedStatsToList(ref NativeList<StatHandle> observedStatHandles);
        public void Apply(
            ref StatsReader statsReader,
            ref TStatModifierStack stack,
            out bool shouldProduceModifierTriggerEvent);
    }

    public struct StatsOwner : IComponentData
    {
        public uint ModifierIDCounter;
    }

    public partial struct Stat : IBufferElementData
    {
        public float BaseValue;
        public float Value;

        public int LastModifierIndex;
        public int LastObserverIndex;
        
        public byte ProduceChangeEvents;
    }

    public partial struct StatModifier<TStatModifier, TStatModifierStack> : IBufferElementData, ICompactMultiLinkedListElement
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public uint ID;
        public TStatModifier Modifier;
        public int PrevElementIndexData;
        public int PrevElementIndex
        {
            get => PrevElementIndexData;
            set => PrevElementIndexData = value;
        }
    }

    public partial struct StatObserver : IBufferElementData, ICompactMultiLinkedListElement
    {
        public StatHandle ObserverHandle;
        public int PrevElementIndexData;
        public int PrevElementIndex
        {
            get => PrevElementIndexData;
            set => PrevElementIndexData = value;
        }
    }

    public struct StatChangeEvent
    {
        public StatHandle StatHandle;
        public Stat PrevValue;
        public Stat NewValue;
    }

    public struct StatHandle : IEquatable<StatHandle>
    {
        public int Index;
        public Entity Entity;

        public StatHandle(Entity entity, int index)
        {
            Index = index;
            Entity = entity;
        }
        
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

    public struct StatsReader
    {
        private byte _isCreated;
        private BufferLookup<Stat> _statsBufferLookup;
        
        public bool IsCreated => _isCreated != 0;
        
        internal StatsReader(in BufferLookup<Stat> statsBufferLookup)
        {
            _isCreated = 1;
            _statsBufferLookup = statsBufferLookup;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStat(StatHandle statHandle, out float value, out float baseValue)
        {
            return StatsUtilities.TryGetStat(statHandle, in _statsBufferLookup, out value, out baseValue);
        }
    }

    public struct StatModifierAndHandle<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public StatModifierHandle ModifierHandle;
        public TStatModifier Modifier;
    }
}