using System;

namespace Trove.PolymorphicElements
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class PolymorphicElementsGroup : System.Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Struct)]
    public class PolymorphicElement : System.Attribute
    {
    }
}