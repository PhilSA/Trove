using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct PolymorphicElementsTests : IComponentData
{
    public int StresTestBatches;
}

public class PolymorphicElementsTestsAuthoring : MonoBehaviour
{
    public PolymorphicElementsTests Params;
    public GameObject StateMachinePrefab;

    class Baker : Baker<PolymorphicElementsTestsAuthoring>
    {
        public override void Bake(PolymorphicElementsTestsAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.Params);
        }
    }
}
