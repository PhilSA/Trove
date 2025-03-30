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
    public bool MakeLocalStatsDependOnEachOther;

    public bool HasInitialized;
}

public struct TestStatOwner : IComponentData
{
    public StatHandle StatA;
    public StatHandle StatB;
    public StatHandle StatC;

    public float tmp;
}

public struct TestStatModifier : IBufferElementData, IStatsModifier<TestStatModifier.Stack>
{
    public enum Type
    {
        Add,
        AddFromStat,
        AddToMultiplier,
        AddToMultiplierFromStat,
    }

    public struct Stack : IStatsModifierStack
    {
        public float Add;
        public float Multiplier;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Add = 0f;
            Multiplier = 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(in float statBaseValue, ref float statValue)
        {
            statValue = statBaseValue;
            statValue += Add;
            statValue *= Multiplier;
        }
    }

    public int PrevElementIndex { get; set; }
    public uint ID { get; set; }

    public Type ModifierType;
    public float ValueA;
    public StatHandle StatHandleA;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddObservedStatsToList(ref NativeList<StatHandle> observedStatHandles)
    {
        switch (ModifierType)
        {
            case (Type.AddFromStat):
            case (Type.AddToMultiplierFromStat):
                observedStatHandles.Add(StatHandleA);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply(ref StatValueReader statValueReader, ref Stack stack)
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
                if (statValueReader.TryGetStat(StatHandleA, out Stat statA))
                {
                    stack.Add += statA.Value;
                }
                break;
            }
            case (Type.AddToMultiplier):
            {
                stack.Multiplier += ValueA;
                break;
            }
            case (Type.AddToMultiplierFromStat):
            {
                if (statValueReader.TryGetStat(StatHandleA, out Stat statA))
                {
                    stack.Multiplier += statA.Value;
                }
                break;
            }
        }
    }
}
