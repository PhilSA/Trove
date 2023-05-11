using Trove.Attributes;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

[BurstCompile]
public struct AttributeGetterSetter : IAttributeGetterSetter
{
    public ComponentLookup<Strength> StrengthLookup;
    public ComponentLookup<Dexterity> DexterityLookup;
    public ComponentLookup<Intelligence> IntelligenceLookup;

    [BurstCompile]
    public void OnSystemCreate(ref SystemState state)
    {
        StrengthLookup = state.GetComponentLookup<Strength>();
        DexterityLookup = state.GetComponentLookup<Dexterity>();
        IntelligenceLookup = state.GetComponentLookup<Intelligence>();
    }

    [BurstCompile]
    public void OnSystemUpdate(ref SystemState state)
    {
        StrengthLookup.Update(ref state);
        DexterityLookup.Update(ref state);
        IntelligenceLookup.Update(ref state);
    }

    [BurstCompile]
    public bool GetAttributeValues(AttributeReference attributeReference, out AttributeValues value)
    {
        AttributeType type = (AttributeType)attributeReference.AttributeType;
        switch (type)
        {
            case AttributeType.Strength:
                {
                    if (StrengthLookup.TryGetComponent(attributeReference.Entity, out Strength comp))
                    {
                        value = comp.Values;
                        return true;
                    }
                }
                break;
            case AttributeType.Dexterity:
                {
                    if (DexterityLookup.TryGetComponent(attributeReference.Entity, out Dexterity comp))
                    {
                        value = comp.Values;
                        return true;
                    }
                }
                break;
            case AttributeType.Intelligence:
                {
                    if (IntelligenceLookup.TryGetComponent(attributeReference.Entity, out Intelligence comp))
                    {
                        value = comp.Values;
                        return true;
                    }
                }
                break;
        }

        value = default;
        return false;
    }

    [BurstCompile]
    public bool SetAttributeValues(AttributeReference attributeReference, AttributeValues value)
    {
        AttributeType type = (AttributeType)attributeReference.AttributeType;
        switch (type)
        {
            case AttributeType.Strength:
                {
                    var compRW = StrengthLookup.GetRefRW(attributeReference.Entity);
                    if (compRW.IsValid)
                    {
                        compRW.ValueRW.Values = value;
                        return true;
                    }
                }
                break;
            case AttributeType.Dexterity:
                {
                    var compRW = DexterityLookup.GetRefRW(attributeReference.Entity);
                    if (compRW.IsValid)
                    {
                        compRW.ValueRW.Values = value;
                        return true;
                    }
                }
                break;
            case AttributeType.Intelligence:
                {
                    var compRW = IntelligenceLookup.GetRefRW(attributeReference.Entity);
                    if (compRW.IsValid)
                    {
                        compRW.ValueRW.Values = value;
                        return true;
                    }
                }
                break;
        }

        return false;
    }
}