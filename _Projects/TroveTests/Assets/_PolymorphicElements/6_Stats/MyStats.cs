using Unity.Entities;
using Unity.Mathematics;
using System;
using Trove.PolymorphicElements;
using Trove.Stats;

public enum StatType : ushort // Has to be ushort
{
    Strength,
    Intelligence,
    Dexterity,
}

public struct StatModifiersStack : IStatModifiersStack
{
    public float Multiply;
    public float Add;

    public void Reset()
    {
        Multiply = 1f;
        Add = 0f;
    }

    public void Apply(float statBaseValue, ref float statValue)
    {
        statValue = statBaseValue;
        statValue *= Multiply;
        statValue += Add;
    }
}

[PolymorphicElementsGroup]
public interface IStatModifier : IBaseStatModifier<StatModifiersStack>
{ }

[PolymorphicElement]
public struct StatModifier_Add : IStatModifier
{
    public float Value;

    public void Apply(ref StatModifiersStack stack)
    {
        stack.Add += Value;
    }
}

[PolymorphicElement]
public struct StatModifier_Multiply : IStatModifier 
{
    public float Value;

    public void Apply(ref StatModifiersStack stack)
    {
        stack.Multiply += Value;
    }
}
