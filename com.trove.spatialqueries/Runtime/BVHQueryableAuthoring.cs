using System;
using System.Collections.Generic;
using Trove.SpatialQueries;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class BVHQueryableAuthoring : MonoBehaviour
{
    public float3 Center = float3.zero;
    public float3 Extents = new float3(0.5f);
    
    public void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        
        RigidTransform rTransform = new RigidTransform(transform.rotation, transform.position);
        
        float3 v0 = math.transform(rTransform, -Extents); // left-down-back
        float3 v1 = math.transform(rTransform, new float3(Extents.x, -Extents.y, -Extents.z)); // right-down-back
        float3 v2 = math.transform(rTransform, new float3(-Extents.x, Extents.y, -Extents.z)); // left-up-back
        float3 v3 = math.transform(rTransform, new float3(Extents.x, Extents.y, -Extents.z)); // right-up-back
        float3 v4 = math.transform(rTransform, new float3(-Extents.x, -Extents.y, Extents.z)); // left-down-front
        float3 v5 = math.transform(rTransform, new float3(Extents.x, -Extents.y, Extents.z)); // right-down-front
        float3 v6 = math.transform(rTransform, new float3(-Extents.x, Extents.y, Extents.z)); // left-up-front
        float3 v7 = math.transform(rTransform, Extents); // right-up-front
        
        Vector3[] lines = new Vector3[]
        {
            v0, v1,
            v0, v2,
            v3, v1,
            v3, v2,
            
            v7, v6,
            v7, v5,
            v4, v6,
            v4, v5,
            
            v0, v4,
            v1, v5,
            v2, v6,
            v3, v7,
        };
        
        Gizmos.DrawLineList(lines);
    }
}

class BVHQueryableAuthoringBaker : Baker<BVHQueryableAuthoring>
{
    public override void Bake(BVHQueryableAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new BVHQueryable
        {
            Center = authoring.Center,
            Extents = authoring.Extents,
        });
    }
}
