using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using AttributeCommand = Trove.Attributes.AttributeCommand<Trove.Attributes.Tests.AttributeModifier, Trove.Attributes.Tests.AttributeModifierStack, Trove.Attributes.Tests.AttributeGetterSetter>;

namespace Trove.Attributes.Tests
{
    public struct AttributeCommandElement : IBufferElementData
    {
        public AttributeCommand Command;

        public static implicit operator AttributeCommandElement(AttributeCommand c)
        {
            return new AttributeCommandElement { Command = c };
        }
    }
}