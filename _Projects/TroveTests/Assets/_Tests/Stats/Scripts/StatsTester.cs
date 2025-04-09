using System.Runtime.CompilerServices;
using Trove.Stats;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

[assembly: RegisterGenericComponentType(typeof(StatModifier<TestStatModifier, TestStatModifier.Stack>))]

public struct StatsTester : IComponentData
{
    public Entity StatOwnerPrefab;

    public int ChangingAttributesCount;
    public int ChangingAttributesChildDepth;
    public int UnchangingAttributesCount;
    public bool MakeLocalStatsDependOnEachOther;

    public int SimpleAddModifiersAdded;

    public bool HasInitialized;
}

public struct TestStatOwner : IComponentData
{
    public StatHandle StatA;
    public StatHandle StatB;
    public StatHandle StatC;

    public float tmp;
}

public struct TestStatModifier : IStatsModifier<TestStatModifier.Stack>
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
    public void Apply(ref StatsReader statsReader, ref Stack stack, out bool shouldProduceModifierTriggerEvent)
    {
        shouldProduceModifierTriggerEvent = false;
        
        switch (ModifierType)
        {
            case (Type.Add):
            {
                stack.Add += ValueA;
                break;
            }
            case (Type.AddFromStat):
            {
                if (statsReader.TryGetStat(StatHandleA, out float statAValue, out _))
                {
                    stack.Add += statAValue;
                    break;
                }
                shouldProduceModifierTriggerEvent = true;
                break;
            }
            case (Type.AddToMultiplier):
            {
                stack.Multiplier += ValueA;
                break;
            }
            case (Type.AddToMultiplierFromStat):
            {
                if (statsReader.TryGetStat(StatHandleA, out float statAValue, out _))
                {
                    stack.Multiplier += statAValue;
                    break;
                }
                shouldProduceModifierTriggerEvent = true;
                break;
            }
        }
    }
}
