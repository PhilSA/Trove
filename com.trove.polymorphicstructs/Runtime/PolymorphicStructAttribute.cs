using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Trove.PolymorphicStructs
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class PolymorphicStructInterface : System.Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Struct)]
    public class PolymorphicStruct : System.Attribute
    {
    }
}