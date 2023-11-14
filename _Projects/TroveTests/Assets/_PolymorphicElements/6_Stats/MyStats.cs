using Unity.Entities;
using Unity.Mathematics;
using System;
using Trove.PolymorphicElements;
using Trove.Stats;
using Unity.Collections;

[PolymorphicElementsGroup]
public interface IStatModifier : IBaseStatModifier<StatModifiersStack>
{ }

public static class StatsHandler
{
    private static BaseStatsHandler<IStatModifierUnionElement, StatModifiersStack, ModifiersApplier> _baseHandler = default;

    public static void InitializeStatsData(ref DynamicBuffer<StatsData> statsDataBuffer, in NativeList<StatDefinition> statDefinitions) => 
        _baseHandler.InitializeStatsData(ref statsDataBuffer, in statDefinitions);
    public static int GetDataLayoutVersion(ref DynamicBuffer<StatsData> statsDataBuffer) =>
        _baseHandler.GetDataLayoutVersion(ref statsDataBuffer);
    public static bool IncrementDataLayoutVersion(ref DynamicBuffer<StatsData> statsDataBuffer) =>
        _baseHandler.IncrementDataLayoutVersion(ref statsDataBuffer);
    public static bool GetStatValues(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference, out StatValues statValues) =>
        _baseHandler.GetStatValues(ref statsDataBufferLookup, ref statReference, out statValues);
    public static bool GetStatValues(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, out StatValues statValues) =>
        _baseHandler.GetStatValues(ref statsDataBuffer, ref statReference, out statValues);
    public static bool SetStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference, float baseValue) =>
        _baseHandler.SetStatBaseValue(ref statsDataBufferLookup, ref statReference, baseValue);
    public static bool SetStatBaseValue(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, float baseValue) =>
        _baseHandler.SetStatBaseValue(ref statsDataBuffer, ref statReference, baseValue);
    public static bool AddStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference, float value) =>
        _baseHandler.AddStatBaseValue(ref statsDataBufferLookup, ref statReference, value);
    public static bool AddStatBaseValue(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, float value) =>
        _baseHandler.AddStatBaseValue(ref statsDataBuffer, ref statReference, value);
    public static bool RecalculateStat(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference) =>
        _baseHandler.RecalculateStat(ref statsDataBufferLookup, ref statReference);
    public static bool RecalculateStat(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference) =>
        _baseHandler.RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference);
}

public struct ModifiersApplier : IModifiersApplier<StatModifiersStack>
{
    public void ApplyModifiers(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatModifiersStack modifiersStack, int startByteIndex, int count)
    {
        bool success = true;
        while (success)
        {
            IStatModifierManager.Apply(ref statsDataBuffer, startByteIndex, out startByteIndex, ref modifiersStack, out success);
        }
    }
}

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
