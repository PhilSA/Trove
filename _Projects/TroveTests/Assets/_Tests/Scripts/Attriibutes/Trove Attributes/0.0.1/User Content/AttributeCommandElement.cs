using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using AttributeCommand = Trove.Attributes.AttributeCommand<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;

public struct AttributeCommandElement : IBufferElementData
{
    public AttributeCommand Command;

    public static implicit operator AttributeCommandElement(AttributeCommand c)
    {
        return new AttributeCommandElement { Command = c };
    }
}
