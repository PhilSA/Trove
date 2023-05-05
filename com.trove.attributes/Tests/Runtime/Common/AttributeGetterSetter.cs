using Trove.Attributes;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Trove.Attributes.Tests
{
    public struct AttributeGetterSetter : IAttributeGetterSetter
    {
        public ComponentLookup<AttributeA> AttributeALookup;
        public ComponentLookup<AttributeB> AttributeBLookup;
        public ComponentLookup<AttributeC> AttributeCLookup;

        public void OnSystemCreate(ref SystemState state)
        {
            AttributeALookup = state.GetComponentLookup<AttributeA>();
            AttributeBLookup = state.GetComponentLookup<AttributeB>();
            AttributeCLookup = state.GetComponentLookup<AttributeC>();
        }

        public void OnSystemUpdate(ref SystemState state)
        {
            AttributeALookup.Update(ref state);
            AttributeBLookup.Update(ref state);
            AttributeCLookup.Update(ref state);
        }

        public bool GetAttributeValues(AttributeReference attributeReference, out AttributeValues value)
        {
            AttributeType type = (AttributeType)attributeReference.AttributeType;
            switch (type)
            {
                case AttributeType.A:
                    {
                        if (AttributeALookup.TryGetComponent(attributeReference.Entity, out AttributeA comp))
                        {
                            value = comp.Values;
                            return true;
                        }
                    }
                    break;
                case AttributeType.B:
                    {
                        if (AttributeBLookup.TryGetComponent(attributeReference.Entity, out AttributeB comp))
                        {
                            value = comp.Values;
                            return true;
                        }
                    }
                    break;
                case AttributeType.C:
                    {
                        if (AttributeCLookup.TryGetComponent(attributeReference.Entity, out AttributeC comp))
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

        public bool SetAttributeValues(AttributeReference attributeReference, AttributeValues value)
        {
            AttributeType type = (AttributeType)attributeReference.AttributeType;
            switch (type)
            {
                case AttributeType.A:
                    {
                        if (AttributeALookup.TryGetComponent(attributeReference.Entity, out AttributeA comp))
                        {
                            comp.Values = value;
                            AttributeALookup[attributeReference.Entity] = comp;
                            return true;
                        }
                    }
                    break;
                case AttributeType.B:
                    {
                        if (AttributeBLookup.TryGetComponent(attributeReference.Entity, out AttributeB comp))
                        {
                            comp.Values = value;
                            AttributeBLookup[attributeReference.Entity] = comp;
                            return true;
                        }
                    }
                    break;
                case AttributeType.C:
                    {
                        if (AttributeCLookup.TryGetComponent(attributeReference.Entity, out AttributeC comp))
                        {
                            comp.Values = value;
                            AttributeCLookup[attributeReference.Entity] = comp;
                            return true;
                        }
                    }
                    break;
            }

            return false;
        }
    }
}