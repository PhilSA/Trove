using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Trove.DebugDraw
{
    public struct DebugDrawGroup
    {
        internal NativeReference<bool> IsDirty;

        internal NativeList<float4> LinePositions;
        internal NativeList<float4> LineColors;

        internal NativeList<float4> TrianglePositions;
        internal NativeList<float4> TriangleColors;

        public bool IsCreated => LinePositions.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddLine(float3 start, float3 end, UnityEngine.Color color)
        {
            IsDirty.Value = true;

            LinePositions.Add(new float4(start, 0f));
            LinePositions.Add(new float4(end, 0f));

            float4 colorFloat = color.ToFloat4();
            LineColors.Add(colorFloat);
            LineColors.Add(colorFloat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddLine(float3 start, float3 end, UnityEngine.Color colorStart, UnityEngine.Color colorEnd)
        {
            IsDirty.Value = true;

            LinePositions.Add(new float4(start, 0f));
            LinePositions.Add(new float4(end, 0f));

            LineColors.Add(colorStart.ToFloat4());
            LineColors.Add(colorEnd.ToFloat4());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTriangle(float3 v0, float3 v1, float3 v2, UnityEngine.Color color)
        {
            IsDirty.Value = true;

            TrianglePositions.Add(new float4(v0, 0f));
            TrianglePositions.Add(new float4(v1, 0f));
            TrianglePositions.Add(new float4(v2, 0f));

            float4 colorFloat = color.ToFloat4();
            TriangleColors.Add(colorFloat);
            TriangleColors.Add(colorFloat);
            TriangleColors.Add(colorFloat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            IsDirty.Value = true;

            LinePositions.Clear();
            LineColors.Clear();

            TrianglePositions.Clear();
            TriangleColors.Clear();
        }

        public void Dispose(JobHandle dep = default)
        {
            if (LinePositions.IsCreated)
            {
                LinePositions.Dispose(dep);
            }

            if (LineColors.IsCreated)
            {
                LineColors.Dispose(dep);
            }

            if (TrianglePositions.IsCreated)
            {
                TrianglePositions.Dispose(dep);
            }

            if (TriangleColors.IsCreated)
            {
                TriangleColors.Dispose(dep);
            }
        }
    }
}
