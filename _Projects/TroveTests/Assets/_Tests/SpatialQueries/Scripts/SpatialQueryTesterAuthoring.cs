using Unity.Entities;
using UnityEngine;
using Trove.SpatialQueries;
using Unity.Mathematics;

class SpatialQueryTesterAuthoring : MonoBehaviour
{
    public GameObject BVHCubePrefab;

    public int SpawnCount = 100;
    public CenteredAABB SpawnArea = new CenteredAABB { Center = float3.zero, Extents = new float3(50f) };
}

class SpatialQueryTesterAuthoringBaker : Baker<SpatialQueryTesterAuthoring>
{
    public override void Bake(SpatialQueryTesterAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new SpatialQueryTester
        {
            BVHCubePrefab = GetEntity(authoring.BVHCubePrefab, TransformUsageFlags.None),
            
            SpawnCount = authoring.SpawnCount,
            SpawnArea = authoring.SpawnArea.ToAABB(),
        });
    }
}
