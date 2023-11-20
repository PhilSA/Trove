using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct CoroutineTests : IComponentData
{
    public int RoutinesCount;
    public Entity CubePrefab;
}

public class CoroutineTestsAuthoring : MonoBehaviour
{
    public CoroutineTests Params;
    public GameObject CubePrefab;

    class Baker : Baker<CoroutineTestsAuthoring>
    {
        public override void Bake(CoroutineTestsAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            authoring.Params.CubePrefab = GetEntity(authoring.CubePrefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, authoring.Params);
        }
    }
}
