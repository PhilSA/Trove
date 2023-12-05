using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class BasicVOTestsAuthoring : MonoBehaviour
{
    class Baker : Baker<BasicVOTestsAuthoring>
    {
        public override void Bake(BasicVOTestsAuthoring authoring)
        {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new BasicVOTests());
        }
    }
}