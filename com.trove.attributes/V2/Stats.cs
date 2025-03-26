using System.Runtime.CompilerServices;
using System.Threading;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Trove.EventSystems;
using Unity.Collections;

namespace Trove.Stats
{
    public interface IStatsModifierStack
    {
        public void Reset();
        public void Apply(ref Stat stat);
    }

    public interface IStatsModifier<TStack>
        where TStack : unmanaged, IStatsModifierStack
    {
        public uint Id { get; set; }
        public StatHandle AffectedStatHandle { get; set; }
        public void AddObservedStatsToList(ref UnsafeList<StatHandle> observedStats);
        public void Apply(
            ref TStack stack,
            Entity cachedEntity,
            ref DynamicBuffer<Stat> cachedStatsBuffer,
            ref BufferLookup<Stat> statsBufferLookup);
    }

    public struct BakingModifier<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public int AffectedStatIndex;
        public TStatModifier Modifier;
    }

    public struct StatsSettings : IComponentData
    {
        public int BatchRecomputeUpdatesCount;
        public bool EndWithRecomputeImmediate;

        public static StatsSettings Default()
        {
            return new StatsSettings
            {
                BatchRecomputeUpdatesCount = 1,
                EndWithRecomputeImmediate = true,
            };
        }
    }

    [System.Serializable]
    public struct StatDefinition
    {
        [UnityEngine.HideInInspector]
        public int StatIndex;
        public float BaseValue;

        public StatDefinition(int index, float baseValue)
        {
            StatIndex = index;
            this.BaseValue = baseValue;
        }
    }

    public struct StatOwner : IComponentData
    {
        public uint ModifierIdCounter;
    }

    [InternalBufferCapacity(3)]
    public partial struct Stat : IBufferElementData
    {
        public float BaseValue;
        public float Value;
    }

    [InternalBufferCapacity(0)]
    public unsafe partial struct StatObserver : IBufferElementData
    {
        public StatHandle ObserverStat;
        public StatHandle ObservedStat;
        public int Count;

        public StatObserver(StatHandle observerStat, StatHandle observedStat, int count = 0)
        {
            ObservedStat = observedStat;
            ObserverStat = observerStat;
            Count = count;
        }
    }

    public struct DirtyStatsMask : IComponentData, IEnableableComponent
    {
        public struct Iterator
        {
            internal long BitMask_0;
            internal long BitMask_1;
            internal int BitCount;

            internal int BitIterator;
            internal int SubMaskBitIterator;

            internal Iterator(DirtyStatsMask d)
            {
                BitMask_0 = d.BitMask_0;
                BitMask_1 = d.BitMask_1;
                BitCount = d.StatsCount;

                BitIterator = 0;
                SubMaskBitIterator = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool GetNextDirtyStat(out int nextStatIndex)
            {
                // First mask
                while (BitIterator < BitCount)
                {
                    // If submask has its first bit enabled, return this index and shift mask
                    if ((BitMask_0 & 1) != 0L)
                    {
                        nextStatIndex = BitIterator;
                        BitIterator++;
                        SubMaskBitIterator++;
                        BitMask_0 >>= 1;
                        return true;
                    }

                    BitIterator++;
                    SubMaskBitIterator++;
                    BitMask_0 >>= 1;
                }

                // Moving on to second mask
                SubMaskBitIterator = 0;
                while (BitIterator < BitCount)
                {
                    // If submask has its first bit enabled, return this index and shift mask
                    if ((BitMask_1 & 1) != 0L)
                    {
                        nextStatIndex = BitIterator;
                        BitIterator++;
                        SubMaskBitIterator++;
                        BitMask_1 >>= 1;
                        return true;
                    }

                    BitIterator++;
                    SubMaskBitIterator++;
                    BitMask_1 >>= 1;
                }

                // Additional masks would go here

                nextStatIndex = -1;
                return false;
            }
        }

        internal long BitMask_0;
        internal long BitMask_1;
        internal int StatsCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Iterator GetIterator()
        {
            return new Iterator(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBit(int index)
        {
            int subMaskIndex = index / 64;
            int indexInSubMask = index % 64;
            long newMask;
            switch (subMaskIndex)
            {
                case 0:
                    newMask = BitMask_0 | (uint)(1 << indexInSubMask);
                    Interlocked.Exchange(ref BitMask_0, newMask);
                    break;
                case 1:
                    newMask = BitMask_1 | (uint)(1 << indexInSubMask);
                    Interlocked.Exchange(ref BitMask_1, newMask);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearBit(int index)
        {
            int subMaskIndex = index / 64;
            int indexInSubMask = index % 64;
            long newMask;
            switch (subMaskIndex)
            {
                case 0:
                    newMask = (BitMask_0 & (uint)(~indexInSubMask));
                    Interlocked.Exchange(ref BitMask_0, newMask);
                    break;
                case 1:
                    newMask = (BitMask_1 & (uint)(~indexInSubMask));
                    Interlocked.Exchange(ref BitMask_1, newMask);
                    break;
            }
        }
    }

    public struct StatHandle : IEquatable<StatHandle>
    {
        public Entity Entity;
        public int Index;

        public StatHandle(Entity entity, int index)
        {
            Entity = entity;
            Index = index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is StatHandle h)
            {
                return Equals(h);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(StatHandle other)
        {
            return this == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 55339;
            hash = hash * 104579 + Entity.GetHashCode();
            hash = hash * 104579 + Index.GetHashCode();
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(StatHandle x, StatHandle y)
        {
            return x.Index == y.Index && x.Entity == y.Entity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(StatHandle x, StatHandle y)
        {
            return x.Index != y.Index || x.Entity != y.Entity;
        }
    }

    public struct ModifierHandle : IEquatable<ModifierHandle>
    {
        public Entity Entity;
        public uint Id;

        public ModifierHandle(Entity entity, uint id)
        {
            Entity = entity;
            Id = id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is ModifierHandle h)
            {
                return Equals(h);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ModifierHandle other)
        {
            return this == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = 55339;
            hash = hash * 104579 + Entity.GetHashCode();
            hash = hash * 104579 + Id.GetHashCode();
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ModifierHandle x, ModifierHandle y)
        {
            return x.Entity == y.Entity && x.Id == y.Id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ModifierHandle x, ModifierHandle y)
        {
            return x.Entity != y.Entity || x.Id != y.Id;
        }
    }
}