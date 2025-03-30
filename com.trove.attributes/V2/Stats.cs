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

    public interface IStatsModifier<TStatModifierStack> : ICompactMultiLinkedListElement
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public uint ID { get; set; } // TODO: is it ok for set to be public
        public void AddObservedStatsToList(ref NativeList<StatHandle> observedStatHandles);
        public void Apply(
            ref StatsHandler statsHandler,
            ref TStatModifierStack stack);
    }

    [InternalBufferCapacity(0)]
    public struct StatsOwner : IComponentData
    {
        public uint ModifierIDCounter;
        public FastStatsStorage FastStatsStorage;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct FastStatsStorage
    {
        public const int Capacity = 8;
        
        // TODO: netcode?
        // TODO: review stats size and FieldOffsets

        [FieldOffset(0)]
        public int Length;
        [FieldOffset(4)]
        public Stat Stat0;
        [FieldOffset(36)]
        public Stat Stat1;
        [FieldOffset(68)]
        public Stat Stat2;
        [FieldOffset(100)]
        public Stat Stat3;
        [FieldOffset(132)]
        public Stat Stat4;
        [FieldOffset(164)]
        public Stat Stat5;
        [FieldOffset(196)]
        public Stat Stat6;
        [FieldOffset(228)]
        public Stat Stat7;
        
        // Note: To expand fast stats storage:
        // - Add new Stat fields with the incremented [FieldOffset()]
        // - Adjust "FastStatsStorage.Capacity" const
        // Note: Reducing fast stats storage is also allowed

        internal readonly unsafe byte* buffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                unsafe
                {
                    fixed (void* ptr = &Stat0)
                        return (byte*)ptr;
                }
            }
        }  
        
        internal unsafe Stat this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                return UnsafeUtility.ReadArrayElement<Stat>(buffer, index);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                UnsafeUtility.WriteArrayElement<Stat>(buffer, index, value);
            }
        }
        
        internal unsafe ref Stat GetElementAsRefUnsafe(int index)
        {
            return ref UnsafeUtility.ArrayElementAsRef<Stat>(buffer, index);
        }

        internal bool HasRoom()
        {
            return Length < Capacity;
        }

        internal unsafe bool Add(Stat stat)
        {
            if(!HasRoom())
                return false;

            ref Stat writtenStat = ref GetElementAsRefUnsafe(Length);
            writtenStat = stat;
            
            Length++;
            return true;
        }
    }

    [InternalBufferCapacity(0)]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Stat : IBufferElementData
    {
        [FieldOffset(0)]
        public float BaseValue;
        [FieldOffset(4)]
        public float Value;

        // TODO: how to prevent users from touching any of these fields
        [FieldOffset(8)]
        public int LastModifierIndex;
        [FieldOffset(12)]
        public int LastObserverIndex;
        
        [FieldOffset(16)]
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