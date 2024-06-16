using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class BasicVOTestsAuthoring : MonoBehaviour
{
    public bool UseVirtualObjects;
    public int EntitiesCount = 1000;
    public int ElementsCount = 10;

    class Baker : Baker<BasicVOTestsAuthoring>
    {

        public override void Bake(BasicVOTestsAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new BasicVOTests
            {
                UseVirtualObjects = authoring.UseVirtualObjects,
                EntitiesCount = authoring.EntitiesCount,
                ElementsCount = authoring.ElementsCount,
            });
        }
    }
}