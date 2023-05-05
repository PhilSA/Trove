using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Trove.Attributes
{
    public interface IAttributeModifier<TAttributeModifierStack, TAttributeGetterSetter>
        where TAttributeModifierStack : unmanaged, IAttributeModifierStack
        where TAttributeGetterSetter : unmanaged, IAttributeGetterSetter
    {
        public uint ModifierID { get; set; }
        public int AffectedAttributeType { get; set; }
        public void AddObservedAttributesToList(ref FixedList512Bytes<AttributeReference> observedAttributes);
        public void ApplyModifier(ref TAttributeModifierStack modifierStack, in TAttributeGetterSetter attributeGetterSetter, out bool shouldRemoveModifier);
    }

    public interface IAttributeModifierStack
    {
        public void Initialize();
        public float CalculateFinalValue(float baseValue);
    }

    public interface IAttributeGetterSetter
    {
        public void OnSystemCreate(ref SystemState state);
        public void OnSystemUpdate(ref SystemState state);
        public bool GetAttributeValues(AttributeReference attributeReference, out AttributeValues value);
        public bool SetAttributeValues(AttributeReference attributeReferenc, AttributeValues value);
    }
}