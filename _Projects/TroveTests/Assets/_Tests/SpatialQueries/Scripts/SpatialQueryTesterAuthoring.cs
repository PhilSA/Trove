using Unity.Entities;
using UnityEngine;
using Trove.SpatialQueries;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.Serialization;
using AABB = Trove.AABB;

public struct SpatialQueryTester : IComponentData
{
    public bool UsePhysicsTest;
    
    public Entity BVHCubePrefab;
    public Entity PhysicsCubePrefab;

    public bool UseParallelAdd;
    public bool UseParallelBuild;

    public int SpawnCount;
    public AABB SpawnArea;
    public float SpawnScale;
    
    public float QueryScale;

    public bool IsInitialized;
}

class SpatialQueryTesterAuthoring : MonoBehaviour
{
    public bool UsePhysicsTest;
    
    public GameObject BVHCubePrefab;
    public GameObject PhysicsCubePrefab;

    public bool UseParallelAdd; 
    public bool UseParallelBuild;
    
    public int SpawnCount = 100;
    public float SpawnScale = 1f;
    public float3 SpawnAreaCenter = float3.zero;
    public float3 SpawnAreaExtents = new float3(50f);

    public float QueryScale = 4f;
}

class SpatialQueryTesterAuthoringBaker : Baker<SpatialQueryTesterAuthoring>
{
    public override void Bake(SpatialQueryTesterAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new SpatialQueryTester
        {
            UsePhysicsTest = authoring.UsePhysicsTest,
            
            BVHCubePrefab = GetEntity(authoring.BVHCubePrefab, TransformUsageFlags.None),
            PhysicsCubePrefab = GetEntity(authoring.PhysicsCubePrefab, TransformUsageFlags.None),
            
            UseParallelAdd = authoring.UseParallelAdd,
            UseParallelBuild = authoring.UseParallelBuild,
            
            QueryScale = authoring.QueryScale,
            
            SpawnCount = authoring.SpawnCount,
            SpawnScale = authoring.SpawnScale,
            SpawnArea = AABB.FromCenterExtents(authoring.SpawnAreaCenter, authoring.SpawnAreaExtents),
        });
    }
}
