using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trove.SimpleDraw
{
    public static class SimpleDrawUtilities
    {
        /// <summary>
        /// Round byte counts to int multiples
        /// </summary>
        public static int CalculateGraphicsBufferCountForBytes(int bytesPerInstance, int numInstances, int extraBytes)
        {
            bytesPerInstance = (bytesPerInstance + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            extraBytes = (extraBytes + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            int totalBytes = bytesPerInstance * numInstances + extraBytes;
            return totalBytes / sizeof(int);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3x4 ToPackedMatrix(float4x4 mat)
        {
            return new float3x4
            {
                c0 = new float3(mat.c0.x, mat.c0.y, mat.c0.z),
                c1 = new float3(mat.c1.x, mat.c1.y, mat.c1.z),
                c2 = new float3(mat.c2.x, mat.c2.y, mat.c2.z),
                c3 = new float3(mat.c3.x, mat.c3.y, mat.c3.z),
            };
        }
    }
}
