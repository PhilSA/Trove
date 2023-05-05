using Trove.Attributes;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum ModifierType
{
    Set,
    SetFromAttribute,

    Add,
    AddFromAttribute,

    AddMultiplier,
    AddMultiplierFromAttribute,

    Clamp,
    ClampFromAttribute,
}

[System.Serializable]
public struct AttributeModifierStack : IAttributeModifierStack
{
    public bool HasSet;
    public float SetValue;

    public float AddValue;

    public float MultiplierValue;

    public bool HasClamp;
    public float ClampMinValue;
    public float ClampMaxValue;

    public void Initialize()
    {
        HasSet = false;
        SetValue = 0f;

        AddValue = 0f;

        MultiplierValue = 1.0f;

        HasClamp = false;
        ClampMinValue = 0f;
        ClampMaxValue = 0f;
    }

    public float CalculateFinalValue(float baseValue)
    {
        float value = baseValue;

        if (HasSet)
        {
            value = SetValue;
        }

        value += AddValue;

        value *= MultiplierValue;

        if (HasClamp)
        {
            value = math.clamp(value, ClampMinValue, ClampMaxValue);
        }

        return value;
    }
}

[System.Serializable]
public struct AttributeModifier : IBufferElementData, IAttributeModifier<AttributeModifierStack, AttributeGetterSetter>
{
    public ModifierType ModifierType;

    // Depending on the modifier type, each of these fields could represent different things.
    // It's up to you to determine the minimum amount of data required across all of your modifier types.
    public float ValueA;
    public float ValueB;
    public AttributeReference AttributeA;
    public AttributeReference AttributeB;

    public uint __internal__modifierID;
    public uint ModifierID
    {
        get { return this.__internal__modifierID; }
        set { this.__internal__modifierID = value; }
    }

    public int __internal__affectedAttributeType;
    public int AffectedAttributeType
    {
        get { return this.__internal__affectedAttributeType; }
        set { this.__internal__affectedAttributeType = value; }
    }

    #region Modifier Creators
    public static AttributeModifier Create_Set(float value)
    {
        return new AttributeModifier
        {
            ModifierType = ModifierType.Set,
            ValueA = value,
        };
    }

    public static AttributeModifier Create_SetFromAttribute(AttributeReference attribute)
    {
        return new AttributeModifier
        {
            ModifierType = ModifierType.SetFromAttribute,
            AttributeA = attribute,
        };
    }

    public static AttributeModifier Create_Add(float value)
    {
        return new AttributeModifier
        {
            ModifierType = ModifierType.Add,
            ValueA = value,
        };
    }

    public static AttributeModifier Create_AddFromAttribute(AttributeReference attribute)
    {
        return new AttributeModifier
        {
            ModifierType = ModifierType.AddFromAttribute,
            AttributeA = attribute,
        };
    }

    public static AttributeModifier Create_AddMultiplier(float value)
    {
        return new AttributeModifier
        {
            ModifierType = ModifierType.AddMultiplier,
            ValueA = value,
        };
    }

    public static AttributeModifier Create_AddMultiplierFromAttribute(AttributeReference attribute)
    {
        return new AttributeModifier
        {
            ModifierType = ModifierType.AddMultiplierFromAttribute,
            AttributeA = attribute,
        };
    }

    public static AttributeModifier Create_Clamp(float min, float max)
    {
        return new AttributeModifier
        {
            ModifierType = ModifierType.Clamp,
            ValueA = min,
            ValueB = max,
        };
    }

    public static AttributeModifier Create_ClampFromAttribute(AttributeReference min, AttributeReference max)
    {
        return new AttributeModifier
        {
            ModifierType = ModifierType.ClampFromAttribute,
            AttributeA = min,
            AttributeB = max,
        };
    }
    #endregion

    public void AddObservedAttributesToList(ref FixedList512Bytes<AttributeReference> observedAttributes)
    {
        switch (ModifierType)
        {
            case ModifierType.Set:
            case ModifierType.Add:
            case ModifierType.AddMultiplier:
            case ModifierType.Clamp:
                break;
            case ModifierType.SetFromAttribute:
            case ModifierType.AddFromAttribute:
            case ModifierType.AddMultiplierFromAttribute:
                observedAttributes.Add(AttributeA);
                break;
            case ModifierType.ClampFromAttribute:
                observedAttributes.Add(AttributeA);
                observedAttributes.Add(AttributeB);
                break;
        }
    }

    /// <summary>
    /// Note: You must never use the "AttributeGetterSetter" to SET the value of an attribute in this function. 
    /// Its purpose is only to GET values.
    /// </summary>
    public void ApplyModifier(ref AttributeModifierStack modifierStack, in AttributeGetterSetter attributeGetter, out bool shouldRemoveModifier)
    {
        shouldRemoveModifier = false;

        switch (ModifierType)
        {
            // For the sake of determinism, the "Set" modifier takes the min value between the existing one and the new one (if a "Set" value was already present)
            case ModifierType.Set:
                {
                    bool hadSet = modifierStack.HasSet;
                    modifierStack.HasSet = true;
                    if (hadSet)
                    {
                        modifierStack.SetValue = math.min(modifierStack.SetValue, ValueA);
                    }
                    else
                    {
                        modifierStack.SetValue = ValueA;
                    }
                }
                break;
            // For the sake of determinism, the "Set" modifier takes the min value between the existing one and the new one (if a "Set" value was already present)
            case ModifierType.SetFromAttribute:
                {
                    if (attributeGetter.GetAttributeValues(AttributeA, out AttributeValues attributeValue))
                    {
                        bool hadSet = modifierStack.HasSet;
                        modifierStack.HasSet = true;
                        if (hadSet)
                        {
                            modifierStack.SetValue = math.min(modifierStack.SetValue, attributeValue.Value);
                        }
                        else
                        {
                            modifierStack.SetValue = attributeValue.Value;
                        }
                    }
                    else
                    {
                        shouldRemoveModifier = true;
                    }
                }
                break;
            case ModifierType.Add:
                {
                    modifierStack.AddValue += ValueA;
                }
                break;
            case ModifierType.AddFromAttribute:
                {
                    if (attributeGetter.GetAttributeValues(AttributeA, out AttributeValues attributeValue))
                    {
                        modifierStack.AddValue += attributeValue.Value;
                    }
                    else
                    {
                        shouldRemoveModifier = true;
                    }
                }
                break;
            case ModifierType.AddMultiplier:
                {
                    modifierStack.MultiplierValue += ValueA;
                }
                break;
            case ModifierType.AddMultiplierFromAttribute:
                {
                    if (attributeGetter.GetAttributeValues(AttributeA, out AttributeValues attributeValue))
                    {
                        modifierStack.MultiplierValue += attributeValue.Value;
                    }
                    else
                    {
                        shouldRemoveModifier = true;
                    }
                }
                break;
            // For the sake of determinism, the "Clamp" modifier takes the min/max values between the existing ones and the new ones (if a "Clamp" value was already present)
            case ModifierType.Clamp:
                {
                    bool hadClamp = modifierStack.HasClamp;
                    modifierStack.HasClamp = true;
                    if (hadClamp)
                    {
                        modifierStack.ClampMinValue = math.min(modifierStack.ClampMinValue, ValueA);
                        modifierStack.ClampMaxValue = math.max(modifierStack.ClampMinValue, ValueB);
                    }
                    else
                    {
                        modifierStack.ClampMinValue = ValueA;
                        modifierStack.ClampMaxValue = ValueB;
                    }
                }
                break;
            // For the sake of determinism, the "Clamp" modifier takes the min/max values between the existing ones and the new ones (if a "Clamp" value was already present)
            case ModifierType.ClampFromAttribute:
                {
                    if (attributeGetter.GetAttributeValues(AttributeA, out AttributeValues attributeValueA) &&
                        attributeGetter.GetAttributeValues(AttributeB, out AttributeValues attributeValueB))
                    {
                        modifierStack.HasClamp = true;
                        modifierStack.ClampMinValue = attributeValueA.Value;
                        modifierStack.ClampMaxValue = attributeValueB.Value;
                    }
                    else
                    {
                        shouldRemoveModifier = true;
                    }
                }
                break;
        }
    }
}