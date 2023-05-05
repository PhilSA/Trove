using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class AttributesTesterAuthoring : MonoBehaviour
{
    public int ChangingAttributesCount = 10000;
    public int ChangingAttributesChildDepth = 2;
    public int UnchangingAttributesCount = 0;

    class Baker : Baker<AttributesTesterAuthoring>
    {
        public override void Bake(AttributesTesterAuthoring authoring)
        { 
            AddComponent(GetEntity(TransformUsageFlags.None), new AttributesTester
            {
                ChangingAttributesCount = authoring.ChangingAttributesCount,
                ChangingAttributesChildDepth = authoring.ChangingAttributesChildDepth,
                UnchangingAttributesCount = authoring.UnchangingAttributesCount,
            });
        }
    }
}
