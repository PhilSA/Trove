using System.Runtime.CompilerServices;
using Trove.Stats;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

public struct StatsTester : IComponentData
{
    public Entity StatOwnerPrefab;

    public int ChangingAttributesCount;
    public int ChangingAttributesChildDepth;
    public int UnchangingAttributesCount;
    public bool MakeOtherStatsDependOnFirstStatOfChangingAttributes;

    public bool HasInitialized;
}

// TODO: configurable buffer capacities for all stat buffer types
[InternalBufferCapacity(0)]
public struct StatModifier : IBufferElementData, IStatsModifier<StatModifier.Stack>
{
    public enum Type
    {
        Add,
        AddFromStat,
        AddMultiplier,
        AddMultiplierFromStat,
    }

    public struct Stack : IStatsModifierStack
    {
        public float Add;
        public float AddMultiply;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Add = 0f;
            AddMultiply = 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(ref Trove.Stats.Stat stat)
        {
            stat.Value = stat.BaseValue;
            stat.Value += Add;
            stat.Value *= AddMultiply;
        }
    }

    // TODO: how to inform of the fact that it's not the user's job to assign ID and AffectedStat
    public uint Id { get; set; }
    public StatHandle AffectedStat { get; set; }

    public Type ModifierType;
    public float ValueA;
    public StatHandle StatA;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddObservedStatsToList(ref UnsafeList<StatHandle> observedStats)
    {
        switch (ModifierType)
        {
            case (Type.AddFromStat):
            case (Type.AddMultiplierFromStat):
                observedStats.Add(StatA);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply(
        ref Stack stack,
        Entity cachedEntity,
        ref DynamicBuffer<Trove.Stats.Stat> cachedStatsBuffer,
        ref BufferLookup<Trove.Stats.Stat> statsBufferLookup)
    {
        switch (ModifierType)
        {
            case (Type.Add):
                {
                    stack.Add += ValueA;
                    break;
                }
            case (Type.AddFromStat):
                {
                    if (StatUtilities.TryResolveStat(StatA, cachedEntity, ref cachedStatsBuffer, ref statsBufferLookup, out Stat resolvedStat))
                    {
                        stack.Add += resolvedStat.Value;
                    }
                    break;
                }
            case (Type.AddMultiplier):
                {
                    stack.AddMultiply += ValueA;
                    break;
                }
            case (Type.AddMultiplierFromStat):
                {
                    if (StatUtilities.TryResolveStat(StatA, cachedEntity, ref cachedStatsBuffer, ref statsBufferLookup, out Stat resolvedStat))
                    {
                        stack.AddMultiply += resolvedStat.Value;
                    }
                    break;
                }
        }
    }
}
