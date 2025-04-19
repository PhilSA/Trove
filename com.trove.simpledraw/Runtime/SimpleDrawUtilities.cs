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
    internal struct SimpleDrawProceduralLinesBatch
    {
        internal BatchID BatchId;
        internal BatchMaterialID MaterialID;
        internal GraphicsBufferHandle PositionsBufferHandle;

        public SimpleDrawProceduralLinesBatch(BatchID batchId, BatchMaterialID materialId, GraphicsBufferHandle positionsBufferHandle)
        {
            BatchId = batchId;
            MaterialID = materialId;
            PositionsBufferHandle = positionsBufferHandle;
        }
    }
    
    // internal struct SimpleDrawProceduralTrisBatch
    // {
    //     internal BatchID BatchId;
    //     internal BatchMaterialID MaterialID;
    //     internal GraphicsBufferHandle PositionsBufferHandle;
    //     internal GraphicsBufferHandle NormalsBufferHandle;
    //     internal GraphicsBufferHandle PositionsBufferHandle;
    //
    //     public SimpleDrawProceduralLinesBatch(BatchID batchId, BatchMaterialID materialId, GraphicsBufferHandle indexBufferHandle)
    //     {
    //         BatchId = batchId;
    //         MaterialID = materialId;
    //         IndexBufferHandle = indexBufferHandle;
    //     }
    // }
    
    internal struct SimpleDrawMeshBatch
    {
        internal BatchID BatchId;
        internal BatchMaterialID MaterialID;
        internal BatchMeshID MeshID;

        public SimpleDrawMeshBatch(BatchID batchId, BatchMaterialID materialId, BatchMeshID meshId)
        {
            BatchId = batchId;
            MaterialID = materialId;
            MeshID = meshId;
        }
    }

    public static class SimpleDrawUtilities
    {
        internal const int kSizeOfMatrix = sizeof(float) * 4 * 4;
        internal const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;
        internal const int kSizeOfFloat3 = sizeof(float) * 3;
        internal const int kSizeOfFloat4 = sizeof(float) * 4;
        internal const int kExtraBytes = kSizeOfMatrix * 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3x4 ToPackedMatrix(float4x4 mat)
        {
            return new float3x4
            {
                c0 = mat.c0.xyz,
                c1 = mat.c1.xyz,
                c2 = mat.c2.xyz,
                c3 = mat.c3.xyz,
            };
        }

        public static int GetIntsCountForAccomodatingBytesCount(int bytesPerInstance, int numInstances, int extraBytes = 0)
        {
            // Round byte counts to int multiples
            bytesPerInstance = (bytesPerInstance + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            extraBytes = (extraBytes + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            int totalBytes = bytesPerInstance * numInstances + extraBytes;
            return totalBytes / sizeof(int);
        }
        
        public static unsafe GraphicsBuffer CreateIndexGraphicsBuffer(int numIndexes)
        {
            int bytesPerInstance = kSizeOfFloat4; // position float4
            
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GetIntsCountForAccomodatingBytesCount(bytesPerInstance, numIndexes, kExtraBytes),
                sizeof(int));
        }
        
        public static unsafe GraphicsBuffer CreateDrawLinesGraphicsBuffer(int numInstances)
        {
            int bytesPerInstance = kSizeOfFloat4; // color float4
            
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GetIntsCountForAccomodatingBytesCount(bytesPerInstance, numInstances, kExtraBytes),
                sizeof(int));
        }
        
        public static unsafe GraphicsBuffer CreateDrawTrisGraphicsBuffer(int numInstances)
        {
            int bytesPerInstance = kSizeOfFloat4; // color float4
            
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GetIntsCountForAccomodatingBytesCount(bytesPerInstance, numInstances, kExtraBytes),
                sizeof(int));
        }
        
        public static GraphicsBuffer CreateDrawMeshGraphicsBuffer(int numInstances)
        {
            int bytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;
            
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GetIntsCountForAccomodatingBytesCount(bytesPerInstance, numInstances, kExtraBytes),
                sizeof(int));
        }
        
        internal static void CreateDrawLinesBatch(
            BatchRendererGroup brg, 
            GraphicsBuffer graphicsBuffer, 
            GraphicsBuffer indexBuffer, 
            ref BatchID batchID,
            int numInstances)
        {
            // Index buffer
            // -------------------------------------------
            // Create transform matrices for three example instances.
            NativeArray<float3> vertices = new NativeArray<float3>(numInstances * 2, Allocator.Temp);
            vertices[0] = new float3(-2f, 1f, 0f);
            vertices[1] = new float3(0f, 1f, 0f);
            vertices[2] = new float3(2f, 1f, 0f);
            vertices[3] = new float3(-2f, 1f, 2f);
            vertices[4] = new float3(0f, 1f, 2f);
            vertices[5] = new float3(2f, 1f, 2f);
            indexBuffer.SetData(vertices);
            
            
            // Batch buffer
            // -------------------------------------------
            
            // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
            NativeArray<float4x4> zeroMatrix = new NativeArray<float4x4>(1, Allocator.Temp);
            zeroMatrix[0] = float4x4.zero;

            // Make all instances have unique colors.
            NativeArray<float4> colors = new NativeArray<float4>(numInstances, Allocator.Temp);
            colors[0] = new float4(1f, 0f, 0f, 1f);
            colors[1] = new float4(0f, 1f, 0f, 1f);
            colors[2] = new float4(0f, 0f, 1f, 1f);

            // Calculates start addresses for the different instanced properties. unity_ObjectToWorld starts
            // at address 96 instead of 64, because the computeBufferStartIndex parameter of SetData
            // is expressed as source array elements, so it is easier to work in multiples of sizeof(PackedMatrix).
            uint byteAddressColor = kSizeOfPackedMatrix * 2;

            // Upload the instance data to the GraphicsBuffer so the shader can load them.
            graphicsBuffer.SetData(zeroMatrix, 0, 0, 1);
            graphicsBuffer.SetData(colors, 0, (int)(byteAddressColor / kSizeOfFloat4), colors.Length);

            // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
            // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
            // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
            // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
            // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
            // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
            NativeArray<MetadataValue> metadatas = new NativeArray<MetadataValue>(1, Allocator.TempJob);
            metadatas[0] = new MetadataValue
                { NameID = SimpleDrawSystemManagedDataStore.ColorPropertyId, Value = 0x80000000 | byteAddressColor, };

            // Finally, create a batch for the instances and make the batch use the GraphicsBuffer with the
            // instance data as well as the metadata values that specify where the properties are.
            batchID = brg.AddBatch(metadatas, graphicsBuffer.bufferHandle);

            // TODO: recycle all those arrays instead of realloc?
            zeroMatrix.Dispose();
            vertices.Dispose();
            colors.Dispose();
        }
        
        internal static void CreateDrawTrisBatch(
            BatchRendererGroup brg, 
            GraphicsBuffer graphicsBuffer, 
            GraphicsBuffer indexBuffer, 
            ref BatchID batchID,
            int numInstances)
        {
            // Index buffer
            // -------------------------------------------
            // Create transform matrices for three example instances.
            NativeArray<float3> vertices = new NativeArray<float3>(numInstances * 3, Allocator.Temp);
            vertices[0] = new float3(-2f, 1f, 0f);
            vertices[1] = new float3(0f, 1f, 0f);
            vertices[2] = new float3(2f, 1f, 0f);
            vertices[3] = new float3(-2f, 1f, 2f);
            vertices[4] = new float3(0f, 1f, 2f);
            vertices[5] = new float3(2f, 1f, 2f);
            vertices[6] = new float3(-2f, 1f, 4f);
            vertices[7] = new float3(0f, 1f, 4f);
            vertices[8] = new float3(2f, 1f, 4f);
            indexBuffer.SetData(vertices);
            
            
            // Batch buffer
            // -------------------------------------------
            
            // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
            NativeArray<float4x4> zeroMatrix = new NativeArray<float4x4>(1, Allocator.Temp);
            zeroMatrix[0] = float4x4.zero;

            // Make all instances have unique colors.
            NativeArray<float4> colors = new NativeArray<float4>(numInstances, Allocator.Temp);
            colors[0] = new float4(1f, 0f, 0f, 1f);
            colors[1] = new float4(0f, 1f, 0f, 1f);
            colors[2] = new float4(0f, 0f, 1f, 1f);

            // Calculates start addresses for the different instanced properties. unity_ObjectToWorld starts
            // at address 96 instead of 64, because the computeBufferStartIndex parameter of SetData
            // is expressed as source array elements, so it is easier to work in multiples of sizeof(PackedMatrix).
            uint byteAddressColor = kSizeOfPackedMatrix * 2;

            // Upload the instance data to the GraphicsBuffer so the shader can load them.
            graphicsBuffer.SetData(zeroMatrix, 0, 0, 1);
            graphicsBuffer.SetData(colors, 0, (int)(byteAddressColor / kSizeOfFloat4), colors.Length);

            // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
            // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
            // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
            // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
            // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
            // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
            NativeArray<MetadataValue> metadatas = new NativeArray<MetadataValue>(1, Allocator.TempJob);
            metadatas[0] = new MetadataValue
                { NameID = SimpleDrawSystemManagedDataStore.ColorPropertyId, Value = 0x80000000 | byteAddressColor, };

            // Finally, create a batch for the instances and make the batch use the GraphicsBuffer with the
            // instance data as well as the metadata values that specify where the properties are.
            batchID = brg.AddBatch(metadatas, graphicsBuffer.bufferHandle);

            // TODO: recycle all those arrays instead of realloc?
            zeroMatrix.Dispose();
            vertices.Dispose();
            colors.Dispose();
        }
        
        internal static void CreateDrawMeshBatch(
            BatchRendererGroup brg, 
            GraphicsBuffer graphicsBuffer, 
            ref BatchID batchID,
            int numInstances)
        {
            // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
            NativeArray<float4x4> zeroMatrix = new NativeArray<float4x4>(1, Allocator.Temp);
            zeroMatrix[0] = float4x4.zero;

            // Create transform matrices for three example instances.
            NativeArray<float4x4> matrices = new NativeArray<float4x4>(numInstances, Allocator.Temp);
            matrices[0] = float4x4.Translate(new float3(-2f, 1f, 0f));
            matrices[1] = float4x4.Translate(new float3(0f, 1f, 0f));
            matrices[2] = float4x4.Translate(new float3(2f, 1f, 0f));

            // Convert the transform matrices into the packed format that the shader expects.
            NativeArray<float3x4> objectToWorlds = new NativeArray<float3x4>(numInstances, Allocator.Temp);
            objectToWorlds[0] = SimpleDrawUtilities.ToPackedMatrix(matrices[0]);
            objectToWorlds[1] = SimpleDrawUtilities.ToPackedMatrix(matrices[1]);
            objectToWorlds[2] = SimpleDrawUtilities.ToPackedMatrix(matrices[2]);

            // Also create packed inverse matrices.
            NativeArray<float3x4> worldToObjects = new NativeArray<float3x4>(numInstances, Allocator.Temp);
            worldToObjects[0] = SimpleDrawUtilities.ToPackedMatrix(math.inverse(matrices[0]));
            worldToObjects[1] = SimpleDrawUtilities.ToPackedMatrix(math.inverse(matrices[1]));
            worldToObjects[2] = SimpleDrawUtilities.ToPackedMatrix(math.inverse(matrices[2]));

            // Make all instances have unique colors.
            NativeArray<float4> colors = new NativeArray<float4>(numInstances, Allocator.Temp);
            colors[0] = new float4(1f, 0f, 0f, 1f);
            colors[1] = new float4(0f, 1f, 0f, 1f);
            colors[2] = new float4(0f, 0f, 1f, 1f);

            // In this simple example, the instance data is placed into the buffer like this:
            // Offset | Description
            //      0 | 64 bytes of zeroes, so loads from address 0 return zeroes
            //     64 | 32 uninitialized bytes to make working with SetData easier, otherwise unnecessary
            //     96 | unity_ObjectToWorld, three packed float3x4 matrices
            //    240 | unity_WorldToObject, three packed float3x4 matrices
            //    384 | _BaseColor, three float4s

            // Calculates start addresses for the different instanced properties. unity_ObjectToWorld starts
            // at address 96 instead of 64, because the computeBufferStartIndex parameter of SetData
            // is expressed as source array elements, so it is easier to work in multiples of sizeof(PackedMatrix).
            uint byteAddressObjectToWorld = kSizeOfPackedMatrix * 2;
            uint byteAddressWorldToObject = byteAddressObjectToWorld + (uint)(kSizeOfPackedMatrix * numInstances);
            uint byteAddressColor = byteAddressWorldToObject + (uint)(kSizeOfPackedMatrix * numInstances);

            // Upload the instance data to the GraphicsBuffer so the shader can load them.
            graphicsBuffer.SetData(zeroMatrix, 0, 0, 1);
            graphicsBuffer.SetData(objectToWorlds, 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix),
                objectToWorlds.Length);
            graphicsBuffer.SetData(worldToObjects, 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix),
                worldToObjects.Length);
            graphicsBuffer.SetData(colors, 0, (int)(byteAddressColor / kSizeOfFloat4), colors.Length);

            // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
            // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
            // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
            // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
            // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
            // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
            NativeArray<MetadataValue> metadatas = new NativeArray<MetadataValue>(3, Allocator.TempJob);
            metadatas[0] = new MetadataValue
                { NameID = SimpleDrawSystemManagedDataStore.ObjectToWorldPropertyId, Value = 0x80000000 | byteAddressObjectToWorld, };
            metadatas[1] = new MetadataValue
                { NameID = SimpleDrawSystemManagedDataStore.WorldToObjectPropertyId, Value = 0x80000000 | byteAddressWorldToObject, };
            metadatas[2] = new MetadataValue
                { NameID = SimpleDrawSystemManagedDataStore.ColorPropertyId, Value = 0x80000000 | byteAddressColor, };

            // Finally, create a batch for the instances and make the batch use the GraphicsBuffer with the
            // instance data as well as the metadata values that specify where the properties are.
            batchID = brg.AddBatch(metadatas, graphicsBuffer.bufferHandle);

            // TODO: recycle all those arrays instead of realloc?
            zeroMatrix.Dispose();
            matrices.Dispose();
            objectToWorlds.Dispose();
            worldToObjects.Dispose();
            colors.Dispose();
        }

        internal static unsafe void DrawLinesCommand(
            BatchCullingOutputDrawCommands* drawCommands,
            IntPtr userContext,
            SimpleDrawProceduralLinesBatch linesBatchData,
            int numInstances) 
        {
            // Configure the single draw command to draw kNumInstances instances
            // starting from offset 0 in the array, using the batch, material and mesh
            // IDs registered in the Start() method. It doesn't set any special flags.
            drawCommands->proceduralDrawCommands[0] = new BatchDrawCommandProcedural
            {
                flags = 0,
                batchID = linesBatchData.BatchId,
                materialID = linesBatchData.MaterialID,
                
                sortingPosition = 0,
                visibleCount = (uint)numInstances,
                visibleOffset = 0,
                splitVisibilityMask = 0xff,
                lightmapIndex = 0,
                
                topology = MeshTopology.Lines,
                baseVertex = 0,
                elementCount = (uint)(numInstances * 2), 
                indexBufferHandle = linesBatchData.PositionsBufferHandle,
                indexOffsetBytes = 0,
            };

            // Configure the single draw range to cover the single draw command which
            // is at offset 0.
            drawCommands->drawRanges[0] = new BatchDrawRange
            {
                drawCommandsType = BatchDrawCommandType.Procedural,
                drawCommandsBegin = 0,
                drawCommandsCount = 1,
                
                // This example doesn't care about shadows or motion vectors, so it leaves everything
                // at the default zero values, except the renderingLayerMask which it sets to all ones
                // so Unity renders the instances regardless of mask settings.
                filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, },
            };
        }

        internal static unsafe void DrawTrisCommand(
            BatchCullingOutputDrawCommands* drawCommands,
            IntPtr userContext,
            SimpleDrawProceduralLinesBatch linesBatchData,
            GraphicsBufferHandle indexBufferHandle,
            int numInstances) 
        {
            // Configure the single draw command to draw kNumInstances instances
            // starting from offset 0 in the array, using the batch, material and mesh
            // IDs registered in the Start() method. It doesn't set any special flags.
            drawCommands->proceduralDrawCommands[0] = new BatchDrawCommandProcedural
            {
                flags = 0,
                batchID = linesBatchData.BatchId,
                materialID = linesBatchData.MaterialID,
                
                sortingPosition = 0,
                visibleCount = (uint)numInstances,
                visibleOffset = 0,
                splitVisibilityMask = 0xff,
                lightmapIndex = 0,
                
                topology = MeshTopology.Triangles,
                baseVertex = 0,
                elementCount = 3, // TODO
                indexBufferHandle = indexBufferHandle,
                indexOffsetBytes = 0,
            };

            // Configure the single draw range to cover the single draw command which
            // is at offset 0.
            drawCommands->drawRanges[0] = new BatchDrawRange
            {
                drawCommandsType = BatchDrawCommandType.Procedural,
                drawCommandsBegin = 0,
                drawCommandsCount = 1,
                
                // This example doesn't care about shadows or motion vectors, so it leaves everything
                // at the default zero values, except the renderingLayerMask which it sets to all ones
                // so Unity renders the instances regardless of mask settings.
                filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, },
            };
        }

        internal static unsafe void DrawMeshCommand(
            BatchCullingOutputDrawCommands* drawCommands,
            IntPtr userContext,
            SimpleDrawMeshBatch batchData,
            int numInstances) 
        {
            // Configure the single draw command to draw kNumInstances instances
            // starting from offset 0 in the array, using the batch, material and mesh
            // IDs registered in the Start() method. It doesn't set any special flags.
            drawCommands->drawCommands[0] = new BatchDrawCommand
            {
                flags = 0,
                batchID = batchData.BatchId,
                materialID = batchData.MaterialID,
                meshID = batchData.MeshID,
                submeshIndex = 0,
                
                sortingPosition = 0,
                visibleCount = (uint)numInstances,
                visibleOffset = 0,
                splitVisibilityMask = 0xff,
                lightmapIndex = 0,
            };

            // Configure the single draw range to cover the single draw command which
            // is at offset 0.
            drawCommands->drawRanges[0] = new BatchDrawRange
            {
                drawCommandsType = BatchDrawCommandType.Direct,
                drawCommandsBegin = 0,
                drawCommandsCount = 1,
                
                // This example doesn't care about shadows or motion vectors, so it leaves everything
                // at the default zero values, except the renderingLayerMask which it sets to all ones
                // so Unity renders the instances regardless of mask settings.
                filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, },
            };
        }
    }
}
