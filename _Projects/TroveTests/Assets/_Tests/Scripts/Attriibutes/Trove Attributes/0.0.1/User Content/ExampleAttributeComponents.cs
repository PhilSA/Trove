using Trove.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct Strength : IComponentData
{
    public AttributeValues Values;
}

[Serializable]
public struct Dexterity : IComponentData
{
    public AttributeValues Values;
}

[Serializable]
public struct Intelligence : IComponentData
{
    public AttributeValues Values;
}