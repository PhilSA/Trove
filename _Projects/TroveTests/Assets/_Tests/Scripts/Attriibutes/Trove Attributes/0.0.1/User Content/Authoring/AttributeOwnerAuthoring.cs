using Trove.Attributes;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using AttributeChanger = Trove.Attributes.AttributeChanger<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;
using AttributeUtilities = Trove.Attributes.AttributeUtilities<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;

[DisallowMultipleComponent]
public class AttributeOwnerAuthoring : MonoBehaviour
{
    class Baker : Baker<AttributeOwnerAuthoring>
    {
        public override void Bake(AttributeOwnerAuthoring authoring)
        {
            AttributeUtilities.MakeAttributeOwner(this);
        }
    }
}
