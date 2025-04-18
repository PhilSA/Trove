using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Trove
{
    public enum AxisIndex
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    public struct AABB
    {
        public float3 Min;
        public float3 Max;

        public static AABB GetEmpty()
        {
            return new AABB
            {
                Min = new float3(float.MaxValue),
                Max = new float3(float.MinValue),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromMinMax(float3 min, float3 max)
        {
            return new AABB
            {
                Min = min,
                Max = max,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromCenterExtents(float3 center, float3 extents)
        {
            return new AABB
            {
                Min = center - extents,
                Max = center + extents,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromOBB(in RigidTransform boxTransform, float3 boxExtents)
        {
            // TODO: can probably be done better
            AABB aabb = AABB.GetEmpty();
            aabb.Include(boxTransform.pos + math.mul(boxTransform.rot, boxExtents));
            aabb.Include(boxTransform.pos + math.mul(boxTransform.rot, new float3(-boxExtents.x, boxExtents.y, boxExtents.z)));
            aabb.Include(boxTransform.pos + math.mul(boxTransform.rot, new float3(boxExtents.x, -boxExtents.y, boxExtents.z)));
            aabb.Include(boxTransform.pos + math.mul(boxTransform.rot, new float3(-boxExtents.x, -boxExtents.y, boxExtents.z)));
            aabb.Include(boxTransform.pos + math.mul(boxTransform.rot, new float3(boxExtents.x, boxExtents.y, -boxExtents.z)));
            aabb.Include(boxTransform.pos + math.mul(boxTransform.rot, new float3(-boxExtents.x, boxExtents.y, -boxExtents.z)));
            aabb.Include(boxTransform.pos + math.mul(boxTransform.rot, new float3(boxExtents.x, -boxExtents.y, -boxExtents.z)));
            aabb.Include(boxTransform.pos - math.mul(boxTransform.rot, boxExtents));
            return aabb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromSphere(float3 center, float radius)
        {
            return new AABB
            {
                Min = center - new float3(radius),
                Max = center + new float3(radius),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB FromTriangle(float3 v0, float3 v1, float3 v2)
        {
            AABB aabb = AABB.GetEmpty();
            aabb.Include(v0);
            aabb.Include(v1);
            aabb.Include(v2);
            return aabb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Include(float3 point)
        {
            Min = math.min(Min, point);
            Max = math.max(Max, point);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool OverlapAABB(in AABB other)
        {
            return GeometryUtils.OverlapBounds(in Min, in Max, in other.Min, in other.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool OverlapTriangleAABB(float3 v0, float3 v1, float3 v2)
        {
            AABB triangleAABB = AABB.FromTriangle(v0, v1, v2);
            return OverlapAABB(in triangleAABB);
        }
    }

    public struct Bounds
    {
        public float3 Min;
        public float3 Max;
    }

    public struct Triangle
    {
        public float3 V0;
        public float3 V1;
        public float3 V2;

        public Triangle(float3 v0, float3 v1, float3 v2)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
        }
    }

    public static class GeometryUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculateBoundsCenter(in Bounds bounds)
        {
            return bounds.Min + ((bounds.Max - bounds.Min) * 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculateTriangleCenter(in Triangle triangle)
        {
            return (triangle.V0 + triangle.V1 + triangle.V2) / 3f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculateTriangleNormal(in Triangle triangle)
        {
            return math.normalizesafe(math.cross(triangle.V1 - triangle.V2, triangle.V2 - triangle.V0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleRadiansToDotRatio(float angleRadians)
        {
            return math.cos(angleRadians);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OverlapBounds(in float3 aMin, in float3 aMax, in float3 bMin, in float3 bMax)
        {
            return
                aMin.x <= bMax.x && aMax.x >= bMin.x &&
                aMin.y <= bMax.y && aMax.y >= bMin.y &&
                aMin.z <= bMax.z && aMax.z >= bMin.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceSqPointToSegment(float3 point, float3 s1, float3 s2)
        {
            float3 projPointOnSegment = s1 + math.projectsafe(point - s1, s2 - s1);
            return math.distancesq(point, projPointOnSegment);
        }

        // TODO: Source: https://github.com/recastnavigation/recastnavigation/blob/main/Recast/Source/RecastRasterization.cpp
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ClipPolygonOnAxis(
            in UnsafeList<float3> inputPolyVertices,
            AxisIndex clippingAxisIndex,
            float clippingDistanceAlongAxis,
            ref UnsafeList<float> vertexAxisDeltas,
            ref UnsafeList<float3> outputAbovePolyVertices,
            ref UnsafeList<float3> outputBelowPolyVertices)
        {
            int insertIndex = 0;
            outputAbovePolyVertices.m_length = 0;
            outputBelowPolyVertices.m_length = 0;
            int clippingAxisIndexValue = (int)clippingAxisIndex;

            // For each input vertex, get its relative distance to the clipping line on the specified axis
            // Note: positive means above the line
            UnsafeList<float> inputPolyVertexValues =
                new UnsafeList<float>((float*)inputPolyVertices.Ptr, inputPolyVertices.m_length * 3);
            vertexAxisDeltas.m_length = inputPolyVertices.m_length;
            for (int vertexIndex = 0; vertexIndex < inputPolyVertices.m_length; vertexIndex++)
            {
                vertexAxisDeltas[vertexIndex] = inputPolyVertexValues[(vertexIndex * 3) + clippingAxisIndexValue] - clippingDistanceAlongAxis;
            }

            // For each vertexA in the input polygon, and the preceding vertexB,
            // add vertexA to either the output above or below polygon.
            // If vertices are not on same side, we also insert a new vertex.
            for (int inputVertexAIndex = 0, inputVertexBIndex = inputPolyVertices.m_length - 1;
                 inputVertexAIndex < inputPolyVertices.m_length;
                 inputVertexBIndex = inputVertexAIndex, ++inputVertexAIndex)
            {
                bool verticesAreOnSameSideOfClippingLine = (vertexAxisDeltas[inputVertexAIndex] >= 0f) == (vertexAxisDeltas[inputVertexBIndex] >= 0f);

                // If vertices are on same side of clipping line, 
                if (verticesAreOnSameSideOfClippingLine)
                {
                    // If vertexA is above or on the clipping line,
                    if (vertexAxisDeltas[inputVertexAIndex] >= 0)
                    {
                        // Copy vertex A to output above polygon 
                        insertIndex = outputAbovePolyVertices.m_length;
                        outputAbovePolyVertices.m_length++;
                        outputAbovePolyVertices[insertIndex] = inputPolyVertices[inputVertexAIndex];

                        // If vertex A is not directly on clipping line, end here.
                        // We do this because if the vertex is exactly on clipping line, it must be added to both
                        // above and below polygons
                        if (vertexAxisDeltas[inputVertexAIndex] != 0f)
                        {
                            continue;
                        }
                    }

                    // If vertex A is below or exactly on the clipping line, copy vertex A to output below polygon
                    insertIndex = outputBelowPolyVertices.m_length;
                    outputBelowPolyVertices.m_length++;
                    outputBelowPolyVertices[insertIndex] = inputPolyVertices[inputVertexAIndex];
                }
                // If vertices are NOT on same side of clipping line, 
                else
                {
                    // Create a new vertex on the clipping line, and add it to both the output above and below polys
                    float clippingDistanceRatioBToA = vertexAxisDeltas[inputVertexBIndex] / (vertexAxisDeltas[inputVertexBIndex] - vertexAxisDeltas[inputVertexAIndex]);
                    float3 insertedClippingVertex = inputPolyVertices[inputVertexBIndex] +
                                                    ((inputPolyVertices[inputVertexAIndex] -
                                                      inputPolyVertices[inputVertexBIndex]) *
                                                     clippingDistanceRatioBToA);
                    insertIndex = outputAbovePolyVertices.m_length;
                    outputAbovePolyVertices.m_length++;
                    outputAbovePolyVertices[insertIndex] = insertedClippingVertex;
                    insertIndex = outputBelowPolyVertices.m_length;
                    outputBelowPolyVertices.m_length++;
                    outputBelowPolyVertices[insertIndex] = insertedClippingVertex;

                    // Add vertex A to either above or below output poly (skip if exactly on clipping line, because 
                    // we already added the clipping vertex
                    if (vertexAxisDeltas[inputVertexAIndex] > 0f)
                    {
                        insertIndex = outputAbovePolyVertices.m_length;
                        outputAbovePolyVertices.m_length++;
                        outputAbovePolyVertices[insertIndex] = inputPolyVertices[inputVertexAIndex];
                    }
                    else if (vertexAxisDeltas[inputVertexAIndex] < 0f)
                    {
                        insertIndex = outputBelowPolyVertices.m_length;
                        outputBelowPolyVertices.m_length++;
                        outputBelowPolyVertices[insertIndex] = inputPolyVertices[inputVertexAIndex];
                    }
                }
            }
        }
    }
}