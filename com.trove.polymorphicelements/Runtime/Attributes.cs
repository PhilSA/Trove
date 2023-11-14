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

    [AttributeUsage(AttributeTargets.Method)]
    public class AllowElementModification : System.Attribute
    {
    }

    /// <summary>
    /// Slightly faster than the alternative [AllowElementModification], but potentially unsafe.
    /// If the method execution adds elements to the byte array that the polymorphic elements are stored in,
    /// and this operation causes the array memory to be moved elsewhere, the polymorphic element struct will be 
    /// modifying invalid memory.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AllowElementModificationByRefUnsafe : System.Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class IgnoreGenerationInManager : System.Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class IgnoreGenerationInUnionElement : System.Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class IgnoreGenerationInPartialElements : System.Attribute
    {
    }
}