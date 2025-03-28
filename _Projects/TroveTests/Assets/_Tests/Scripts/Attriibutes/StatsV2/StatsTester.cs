using System.Runtime.CompilerServices;
using Trove.Stats;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

public struct StatsTester : IComponentData
{
    public Entity StatOwnerPrefab;

    public int ChangingAttributesCount;
    public int ChangingAttributesChildDepth;
    public int UnchangingAttributesCount;
    public bool MakeOtherStatsDependOnFirstStatOfChangingAttributes;

    public bool SupportStatsWriteback;

    public bool HasInitialized;
}

public enum StatType
{
    A = 0,
    B, 
    C,
}

public struct TestStatCustomData
{
    public Entity Entity;
    public StatType StatType;

    public TestStatCustomData(Entity entity, StatType statType)
    {
        Entity = entity;
        StatType = statType;
    }
}

// TODO: make builtin?
public struct StatHandle
{
    public int Index;
    public float Value;

    public static StatHandle CreateUnititialized(float baseValue)
    {
        return new StatHandle { Index = -1, Value = baseValue };
    }
}

public struct TestStatOwner : IComponentData
{
    public StatHandle StatA;
    public StatHandle StatB;
    public StatHandle StatC;
}

public struct TestStatModifier : IBufferElementData, IStatsModifier<TestStatModifier.Stack>
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
        public void Apply(ref float statBaseValue, ref float statValue)
        {
            statValue = statBaseValue;
            statValue += Add;
            statValue *= AddMultiply;
        }
    }
    
    public uint ID { get; set; }

    public Type ModifierType;
    public float ValueA;
    public int StatAIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddObservedStatsToList(ref NativeList<int> observedStatIndexes)
    {
        switch (ModifierType)
        {
            case (Type.AddFromStat):
            case (Type.AddMultiplierFromStat):
                observedStatIndexes.Add(StatAIndex);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply(in NativeList<Stat> stats, ref Stack stack)
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
                Stat statA = stats[StatAIndex];
                if (statA.IsCreated)
                {
                    stack.Add += statA.Value;
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
                Stat statA = stats[StatAIndex];
                if (statA.IsCreated)
                {
                    stack.AddMultiply += statA.Value;
                }
                break;
            }
        }
    }
}
