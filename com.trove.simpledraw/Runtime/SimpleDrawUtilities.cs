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

        internal static MetadataValue CreateMetadataValue(int nameID, int gpuAddress, bool isOverridden)
        {
            const uint kIsOverriddenBit = 0x80000000;
            return new MetadataValue
            {
                NameID = nameID,
                Value = (uint)gpuAddress | (isOverridden ? kIsOverriddenBit : 0),
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
        
        internal static void CreateDrawLinesBatch(
            BatchRendererGroup brg, 
            GraphicsBuffer instancesBuffer, 
            GraphicsBuffer positionsBuffer, 
            ref BatchID batchID,
            int numInstances)
        {
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0); // TODO:
            
            int objectToWorldFloat4sCount = numInstances * 3;
            int worldToObjectFloat4sCount = numInstances * 3;
            int colorFloat4sCount = numInstances;
            int totalFloat4sCount = 4 + (objectToWorldFloat4sCount + worldToObjectFloat4sCount + colorFloat4sCount);
            NativeArray<float4> instances = new NativeArray<float4>(totalFloat4sCount, Allocator.Temp);
            
            // Zero matrix
            instances[0] = float4.zero;
            instances[1] = float4.zero;
            instances[2] = float4.zero;
            instances[3] = float4.zero;
            
            // Instance data
            int objectToWorldsStart = 4;
            int worldToObjectsStart = objectToWorldsStart + objectToWorldFloat4sCount;
            int colorsStart = worldToObjectsStart + worldToObjectFloat4sCount;
            for (int i = 0; i < numInstances; i++)
            {
                float4x4 trs = float4x4.Translate(random.NextFloat3(new float3(5f))); // todo
                float4x3 packedTrs = ToPackedMatrix(trs);
                float4x3 packedTrsInv = ToPackedMatrix(math.inverse(trs));
                
                // ObjectToWorlds
                int writeIndex = objectToWorldsStart + i;
                instances[writeIndex] = packedTrs.c0;
                instances[writeIndex + 1] = packedTrs.c1;
                instances[writeIndex + 2] = packedTrs.c2;
                
                // WorldToObjects
                writeIndex = worldToObjectsStart + i;
                instances[writeIndex] = packedTrsInv.c0;
                instances[writeIndex + 1] = packedTrsInv.c1;
                instances[writeIndex + 2] = packedTrsInv.c2;
                
                // Colors
                writeIndex = colorsStart + i;
                Color color = Color.HSVToRGB(random.NextFloat(1f), 1f, 1f);
                instances[writeIndex] = color.ToFloat4();
            }
            instancesBuffer.SetData(instances);
            
            // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
            // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
            // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
            // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
            // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
            // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
            NativeArray<MetadataValue> metadatas = new NativeArray<MetadataValue>(3, Allocator.Temp);
            metadatas[0] = CreateMetadataValue(SimpleDrawSystemManagedDataStore.ObjectToWorldPropertyId, objectToWorldsStart * 16, true);
            metadatas[1] = CreateMetadataValue(SimpleDrawSystemManagedDataStore.WorldToObjectPropertyId, worldToObjectsStart * 16, true);
            metadatas[2] = CreateMetadataValue(SimpleDrawSystemManagedDataStore.ColorPropertyId, colorsStart * 16, true);

            // Finally, create a batch for the instances and make the batch use the GraphicsBuffer with the
            // instance data as well as the metadata values that specify where the properties are.
            batchID = brg.AddBatch(metadatas, instancesBuffer.bufferHandle);
            
            // Index buffer
            NativeArray<float4> positions = new NativeArray<float4>(numInstances * 2, Allocator.Temp);
            for (int i = 0; i < numInstances; i++)
            {
                positions[(i * 2)] = new float4(i, 0f, 0f, 0f);
                positions[(i * 2) + 1] = new float4(i, 1f, 0f, 0f);
            } 
            positionsBuffer.SetData(positions);

            // TODO: recycle all those arrays instead of realloc?
            instances.Dispose();
            positions.Dispose();
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
            NativeArray<float4> vertices = new NativeArray<float4>(numInstances * 3, Allocator.Temp);
            vertices[0] = new float4(-2f, 1f, 0f, 1f);
            vertices[1] = new float4(0f, 1f, 0f, 1f);
            vertices[2] = new float4(2f, 1f, 0f, 1f);
            vertices[3] = new float4(-2f, 1f, 2f, 1f);
            vertices[4] = new float4(0f, 1f, 2f, 1f);
            vertices[5] = new float4(2f, 1f, 2f, 1f);
            vertices[6] = new float4(-2f, 1f, 4f, 1f);
            vertices[7] = new float4(0f, 1f, 4f, 1f);
            vertices[8] = new float4(2f, 1f, 4f, 1f);
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
            int byteAddressColor = kSizeOfPackedMatrix * 2;

            // Upload the instance data to the GraphicsBuffer so the shader can load them.
            graphicsBuffer.SetData(zeroMatrix, 0, 0, 1);
            graphicsBuffer.SetData(colors, 0, (byteAddressColor / kSizeOfFloat4), colors.Length);

            // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
            // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
            // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
            // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
            // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
            // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
            NativeArray<MetadataValue> metadatas = new NativeArray<MetadataValue>(1, Allocator.TempJob);
            metadatas[0] = CreateMetadataValue(SimpleDrawSystemManagedDataStore.ColorPropertyId, byteAddressColor, true);

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
            GraphicsBuffer instancesBuffer, 
            ref BatchID batchID,
            int numInstances)
        {
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0); // TODO:
            
            int objectToWorldFloat4sCount = numInstances * 3;
            int worldToObjectFloat4sCount = numInstances * 3;
            int colorFloat4sCount = numInstances;
            int totalFloat4sCount = 4 + (objectToWorldFloat4sCount + worldToObjectFloat4sCount + colorFloat4sCount);
            NativeArray<float4> instances = new NativeArray<float4>(totalFloat4sCount, Allocator.Temp);
            
            // Zero matrix
            instances[0] = float4.zero;
            instances[1] = float4.zero;
            instances[2] = float4.zero;
            instances[3] = float4.zero;
            
            // Instance data
            int objectToWorldsStart = 4;
            int worldToObjectsStart = objectToWorldsStart + objectToWorldFloat4sCount;
            int colorsStart = worldToObjectsStart + worldToObjectFloat4sCount;
            for (int i = 0; i < numInstances; i++)
            {
                float4x4 trs = float4x4.Translate(random.NextFloat3(new float3(5f))); // todo
                float4x3 packedTrs = ToPackedMatrix(trs);
                float4x3 packedTrsInv = ToPackedMatrix(math.inverse(trs));
                
                // ObjectToWorlds
                int writeIndex = objectToWorldsStart + i;
                instances[writeIndex] = packedTrs.c0;
                instances[writeIndex + 1] = packedTrs.c1;
                instances[writeIndex + 2] = packedTrs.c2;
                
                // WorldToObjects
                writeIndex = worldToObjectsStart + i;
                instances[writeIndex] = packedTrsInv.c0;
                instances[writeIndex + 1] = packedTrsInv.c1;
                instances[writeIndex + 2] = packedTrsInv.c2;
                
                // Colors
                writeIndex = colorsStart + i;
                Color color = Color.HSVToRGB(random.NextFloat(1f), 1f, 1f);
                instances[writeIndex] = color.ToFloat4();
            }
            instancesBuffer.SetData(instances);
            
            // Set up metadata values to point to the instance data. Set the most significant bit 0x80000000 in each
            // which instructs the shader that the data is an array with one value per instance, indexed by the instance index.
            // Any metadata values that the shader uses that are not set here will be 0. When a value of 0 is used with
            // UNITY_ACCESS_DOTS_INSTANCED_PROP (i.e. without a default), the shader interprets the
            // 0x00000000 metadata value and loads from the start of the buffer. The start of the buffer is
            // a zero matrix so this sort of load is guaranteed to return zero, which is a reasonable default value.
            NativeArray<MetadataValue> metadatas = new NativeArray<MetadataValue>(3, Allocator.Temp);
            metadatas[0] = CreateMetadataValue(SimpleDrawSystemManagedDataStore.ObjectToWorldPropertyId, objectToWorldsStart * 16, true);
            metadatas[1] = CreateMetadataValue(SimpleDrawSystemManagedDataStore.WorldToObjectPropertyId, worldToObjectsStart * 16, true);
            metadatas[2] = CreateMetadataValue(SimpleDrawSystemManagedDataStore.ColorPropertyId, colorsStart * 16, true);

            // Finally, create a batch for the instances and make the batch use the GraphicsBuffer with the
            // instance data as well as the metadata values that specify where the properties are.
            batchID = brg.AddBatch(metadatas, instancesBuffer.bufferHandle);

            // TODO: recycle all those arrays instead of realloc?
            instances.Dispose();
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
                flags = BatchDrawCommandFlags.None,
                batchID = linesBatchData.BatchId,
                materialID = linesBatchData.MaterialID,
                
                sortingPosition = 0,
                visibleCount = (uint)numInstances,
                visibleOffset = 0,
                splitVisibilityMask = 0xff,
                lightmapIndex = 0,
                
                topology = MeshTopology.Lines,
                baseVertex = 0,
                elementCount = (uint)(numInstances), 
                indexBufferHandle = default,
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
                flags = BatchDrawCommandFlags.None,
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
                flags = BatchDrawCommandFlags.None,
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
