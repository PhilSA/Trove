using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class PolymorphicElementsTestsAuthoring : MonoBehaviour
{
    public PolymorphicElementsTests Params;

    class Baker : Baker<PolymorphicElementsTestsAuthoring>
    {
        public override void Bake(PolymorphicElementsTestsAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.Params);
        }
    }
}
