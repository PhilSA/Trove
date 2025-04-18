using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using AABB = Trove.AABB;

namespace Trove.SpatialQueries
{
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