using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;

namespace Trove.SpatialQueries
{
    public struct BVHQueryable : IComponentData
    {
        public float3 Center;
        public float3 Extents;
    }
    
    public static class BVH
    {

    }
}