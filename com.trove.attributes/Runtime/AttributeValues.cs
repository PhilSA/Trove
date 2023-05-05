using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Trove.Attributes
{
    public struct AttributeValues
    {
        public float __internal__baseValue;
        public float __internal__value;
        public float BaseValue => __internal__baseValue;
        public float Value => __internal__value;

        public AttributeValues(float baseValue)
        {
            __internal__baseValue = baseValue;
            __internal__value = baseValue;
        }
    }
}