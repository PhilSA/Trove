using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trove.DebugDraw
{
    internal struct DebugDrawProceduralBatch
    {
        internal BatchID BatchId;
        internal BatchMaterialID MaterialID;

        public DebugDrawProceduralBatch(BatchID batchId, BatchMaterialID materialId)
        {
            BatchId = batchId;
            MaterialID = materialId;
        }
    }

    public static class DebugDrawUtilities
    {
        internal const int kSizeOfMatrix = sizeof(float) * 4 * 4;
        internal const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;
        internal const int kSizeOfFloat3 = sizeof(float) * 3;
        internal const int kSizeOfFloat4 = sizeof(float) * 4;
        internal const int kExtraBytes = kSizeOfMatrix * 2;
        internal const uint kIsOverriddenBit = 0x80000000;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float4x3 ToPackedMatrix(float4x4 mat)
        {
            return new float4x3
            {
                c0 = new float4(mat.c0.x, mat.c0.y, mat.c0.z, mat.c1.x),
                c1 = new float4(mat.c1.y, mat.c1.z, mat.c2.x, mat.c2.y),
                c2 = new float4(mat.c2.z, mat.c3.x, mat.c3.y, mat.c3.z),
            };
        }

        internal static int GetIntsCountForAccomodatingBytesCount(int bytesPerInstance, int numInstances, int extraBytes = 0)
        {
            // Round byte counts to int multiples
            bytesPerInstance = (bytesPerInstance + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            extraBytes = (extraBytes + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            int totalBytes = bytesPerInstance * numInstances + extraBytes;
            return totalBytes / sizeof(int);
        }

        internal static MetadataValue CreateMetadataValue(int propertyID, int byteAddress, bool isOverridden)
        {
            return new MetadataValue
            {
                NameID = propertyID,
                Value = (uint)byteAddress | (isOverridden ? kIsOverriddenBit : 0),
            };
        }
        
        internal static unsafe GraphicsBuffer CreateDrawTrisGraphicsBuffer(int numInstances)
        {
            int bytesPerInstance = kSizeOfFloat4; // color float4
            
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GetIntsCountForAccomodatingBytesCount(bytesPerInstance, numInstances, kExtraBytes),
                sizeof(int));
        }
        
        internal static GraphicsBuffer CreateDrawMeshGraphicsBuffer(int numInstances)
        {
            int bytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;
            
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GetIntsCountForAccomodatingBytesCount(bytesPerInstance, numInstances, kExtraBytes),
                sizeof(int));
        }
        
        internal static void CreateDebugDrawBatch(
            BatchRendererGroup brg, 
            GraphicsBuffer instancesBuffer, 
            ref BatchID batchID)
        {
            int objectToWorldFloat4sCount = 3;
            int worldToObjectFloat4sCount = 3;
            int totalFloat4sCount = 4 + objectToWorldFloat4sCount + worldToObjectFloat4sCount;
            NativeArray<float4> instances = new NativeArray<float4>(totalFloat4sCount, Allocator.Temp);
            
            // Zero matrix
            instances[0] = float4.zero;
            instances[1] = float4.zero;
            instances[2] = float4.zero;
            instances[3] = float4.zero;
            
            // Instance data (just 1 instance)
            int objectToWorldsStart = 4;
            int worldToObjectsStart = objectToWorldsStart + objectToWorldFloat4sCount;
            float4x4 trs = float4x4.identity; 
            float4x3 packedTrs = ToPackedMatrix(trs);
            float4x3 packedTrsInv = ToPackedMatrix(math.inverse(trs));
            
            // ObjectToWorld
            instances[objectToWorldsStart] = packedTrs.c0;
            instances[objectToWorldsStart + 1] = packedTrs.c1;
            instances[objectToWorldsStart + 2] = packedTrs.c2;
            
            // WorldToObject
            instances[worldToObjectsStart] = packedTrsInv.c0;
            instances[worldToObjectsStart + 1] = packedTrsInv.c1;
            instances[worldToObjectsStart + 2] = packedTrsInv.c2;
            
            instancesBuffer.SetData(instances, 0, 0, instances.Length);
            instances.Dispose();
            
            NativeArray<MetadataValue> metadatas = new NativeArray<MetadataValue>(2, Allocator.Temp);
            metadatas[0] = CreateMetadataValue(DebugDrawSystemManagedDataStore.ObjectToWorldPropertyId, objectToWorldsStart * kSizeOfFloat4, true);
            metadatas[1] = CreateMetadataValue(DebugDrawSystemManagedDataStore.WorldToObjectPropertyId, worldToObjectsStart * kSizeOfFloat4, true);

            batchID = brg.AddBatch(metadatas, instancesBuffer.bufferHandle);
        }
    }
}
