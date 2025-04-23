using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Trove.DebugDraw
{
    public struct DebugDrawGroup
    {
        internal NativeReference<bool> IsDirty;

        internal NativeList<float4x3> LineOtWs;
        internal NativeList<float4x3> LineWtOs;
        internal NativeList<float4> LineColors;

        internal NativeList<float4x3> TriangleOtWs;
        internal NativeList<float4x3> TriangleWtOs;
        internal NativeList<float4> TriangleColors;

        public bool IsCreated => LineOtWs.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddLine(float3 start, float3 end, UnityEngine.Color color)
        {
            IsDirty.Value = true;

            float4x4 mat = float4x4.Translate(start);
            float4x4 invMat = math.inverse(mat);
            LineOtWs.Add(DebugDrawUtilities.ToPackedMatrix(mat));
            LineWtOs.Add(DebugDrawUtilities.ToPackedMatrix(invMat));

            mat = float4x4.Translate(end);
            invMat = math.inverse(mat);
            LineOtWs.Add(DebugDrawUtilities.ToPackedMatrix(mat));
            LineWtOs.Add(DebugDrawUtilities.ToPackedMatrix(invMat));

            float4 colorFloat = color.ToFloat4();
            LineColors.Add(colorFloat);
            LineColors.Add(colorFloat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddLine(float3 start, float3 end, UnityEngine.Color colorStart, UnityEngine.Color colorEnd)
        {
            IsDirty.Value = true;

            float4x4 mat = float4x4.Translate(start);
            float4x4 invMat = math.inverse(mat);
            LineOtWs.Add(DebugDrawUtilities.ToPackedMatrix(mat));
            LineWtOs.Add(DebugDrawUtilities.ToPackedMatrix(invMat));

            mat = float4x4.Translate(end);
            invMat = math.inverse(mat);
            LineOtWs.Add(DebugDrawUtilities.ToPackedMatrix(mat));
            LineWtOs.Add(DebugDrawUtilities.ToPackedMatrix(invMat));

            LineColors.Add(colorStart.ToFloat4());
            LineColors.Add(colorEnd.ToFloat4());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTriangle(float3 v0, float3 v1, float3 v2, UnityEngine.Color color)
        {
            IsDirty.Value = true;

            float4x4 mat = float4x4.Translate(v0);
            float4x4 invMat = math.inverse(mat);
            LineOtWs.Add(DebugDrawUtilities.ToPackedMatrix(mat));
            LineWtOs.Add(DebugDrawUtilities.ToPackedMatrix(invMat));

            mat = float4x4.Translate(v1);
            invMat = math.inverse(mat);
            LineOtWs.Add(DebugDrawUtilities.ToPackedMatrix(mat));
            LineWtOs.Add(DebugDrawUtilities.ToPackedMatrix(invMat));

            mat = float4x4.Translate(v2);
            invMat = math.inverse(mat);
            LineOtWs.Add(DebugDrawUtilities.ToPackedMatrix(mat));
            LineWtOs.Add(DebugDrawUtilities.ToPackedMatrix(invMat));

            float4 colorFloat = color.ToFloat4();
            TriangleColors.Add(colorFloat);
            TriangleColors.Add(colorFloat);
            TriangleColors.Add(colorFloat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            IsDirty.Value = true;

            LineOtWs.Clear();
            LineWtOs.Clear();
            LineColors.Clear();

            TriangleOtWs.Clear();
            TriangleWtOs.Clear();
            TriangleColors.Clear();
        }

        public void Dispose(JobHandle dep = default)
        {
            if (LineOtWs.IsCreated)
            {
                LineOtWs.Dispose(dep);
            }

            if (LineWtOs.IsCreated)
            {
                LineWtOs.Dispose(dep);
            }

            if (LineColors.IsCreated)
            {
                LineColors.Dispose(dep);
            }

            if (TriangleOtWs.IsCreated)
            {
                TriangleOtWs.Dispose(dep);
            }

            if (TriangleWtOs.IsCreated)
            {
                TriangleWtOs.Dispose(dep);
            }

            if (TriangleColors.IsCreated)
            {
                TriangleColors.Dispose(dep);
            }
        }
    }
}
