using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using AABB = Trove.SpatialQueries.AABB;

namespace Trove.SpatialQueries
{
    [System.Serializable]
    public struct AABB
    {
        public float3 Min;
        public float3 Max;

        public AABB(float3 min, float3 max)
        {
            Min = min;
            Max = max;
        }

        public static AABB FromCenterExtents(float3 center, float3 extents)
        {
            return new AABB
            {
                Min = center - extents,
                Max = center + extents,
            };
        }
    }
    
    [System.Serializable]
    public struct CenteredAABB
    {
        public float3 Center;
        public float3 Extents;

        public AABB ToAABB()
        {
            return new AABB
            {
                Min = Center - Extents,
                Max = Center + Extents,
            };
        }
    }
    
    public struct Ray
    {
        public float3 Origin;
        public float3 Direction;
        public float Length;
    }
    
    public static class SpatialQueryUtils
    {

    }
}