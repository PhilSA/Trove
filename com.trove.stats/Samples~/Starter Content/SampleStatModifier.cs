using System.Runtime.CompilerServices;
using Trove.Stats;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[assembly: RegisterGenericComponentType(typeof(StatModifier<SampleStatModifier, SampleStatModifier.Stack>))]

// TODO: Tweak internal buffer capacity for your use case
public struct SampleStatModifier : IStatsModifier<SampleStatModifier.Stack>
{
    // TODO: Customize the modifier types
    public enum Type
    {
        Add,
        AddFromStat,
        AddToMultiplier,
        AddToMultiplierFromStat,
        Clamp,
        ClampFromStats,
    }

    // TODO: Customize the modifiers stack by adding new fields, and changing the Reset() and Apply() methods.
    //       When a stat must be recalculated, the stack is reset, then all modifiers apply their changes to the stack,
    //       and finally the stack is applied to the stat value. This allows creating a deterministic way to apply
    //       modifiers regardless of their order, creating effects that vary depending on other modifiers applied, etc... //
    //       If you need to set some extra data in the modifiers stack, such as evaluation curves, blob references, etc...,
    //       you can do so with the "StatsWorldData.SetStatModifiersStack()" method
    public struct Stack : IStatsModifierStack
    {
        public float Add;
        public float Multiplier;
        public float2 ClampRange;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Add = 0f;
            Multiplier = 1f;
            ClampRange = new float2(float.MinValue, float.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(in float statBaseValue, ref float statValue)
        {
            statValue = statBaseValue;
            statValue += Add;
            statValue *= Multiplier;
            statValue = math.max(statValue, ClampRange.x);
            statValue = math.min(statValue, ClampRange.y);
        }
    }

    // TODO: Customize the modifier data. This is the shared data among all modifier types.
    public Type ModifierType;
    public float ValueA;
    public float ValueB;
    public StatHandle StatHandleA;
    public StatHandle StatHandleB;

    // TODO: If the modifier relies on any values gotten from a StatHandle, you must add those handles to the list here.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddObservedStatsToList(ref NativeList<StatHandle> observedStatHandles)
    {
        switch (ModifierType)
        {
            case (Type.AddFromStat):
            case (Type.AddToMultiplierFromStat):
                observedStatHandles.Add(StatHandleA);
                break;
            case (Type.ClampFromStats):
                observedStatHandles.Add(StatHandleA);
                observedStatHandles.Add(StatHandleB);
                break;
        }
    }

    // TODO: Customize how the modifier is applied to the stack
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply(ref StatsReader statsReader, ref Stack stack, out bool shouldProduceModifierTriggerEvent)
    {
        // TODO: set to true if the modifier should produce a modifier trigger event
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
                if (statsReader.TryGetStat(StatHandleA, out float statAValue, out float _))
                {
                    stack.Add += statAValue;
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
                if (statsReader.TryGetStat(StatHandleA, out float statAValue, out float _))
                {
                    stack.Multiplier += statAValue;
                }
                break;
            }
            case (Type.Clamp):
            {
                stack.ClampRange.x = math.max(stack.ClampRange.x, ValueA);
                stack.ClampRange.y = math.min(stack.ClampRange.y, ValueB);
                break;
            }
            case (Type.ClampFromStats):
            {
                if (statsReader.TryGetStat(StatHandleA, out float statAValue, out float _) &&
                    statsReader.TryGetStat(StatHandleB, out float statBValue, out float _))
                {
                    stack.ClampRange.x = math.max(stack.ClampRange.x, statAValue);
                    stack.ClampRange.y = math.min(stack.ClampRange.y, statBValue);
                }
                break;
            }
        }
    }
}
