using Unity.Entities;
using UnityEngine;
using Trove.SpatialQueries;
using Unity.Mathematics;
using AABB = Trove.AABB;

class SpatialQueryTesterAuthoring : MonoBehaviour
{
    public GameObject BVHCubePrefab;

    public int SpawnCount = 100;
    public float3 SpawnAreaCenter = float3.zero;
    public float3 SpawnAreaExtents = new float3(50f);
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
            SpawnArea = AABB.FromCenterExtents(authoring.SpawnAreaCenter, authoring.SpawnAreaExtents),
        });
    }
}
