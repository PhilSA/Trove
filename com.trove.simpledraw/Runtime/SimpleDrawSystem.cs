using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trove.SimpleDraw
{
    internal static class SimpleDrawSystemManagedDataStore
    {
        internal static Dictionary<World, SimpleDrawSystemManagedData> DataMap;

        internal static int ColorPropertyId;
        internal static int ObjectToWorldPropertyId;
        internal static int WorldToObjectPropertyId;

        internal static Material SimpleDrawUnlitMaterial;

        internal static Mesh SimpleTriMesh;
        internal static Mesh SimpleQuadMesh;
        internal static Mesh SimpleBoxMesh;
        internal static Mesh SimpleSphereMesh;
        internal static Mesh SimpleCylinderMesh;

        internal const string SimpleDrawUnlitMaterialName = "SimpleDrawUnlitURP";

        internal const string SimpleTriName = "SimpleTri";
        internal const string SimpleQuadName = "SimpleQuad";
        internal const string SimpleBoxName = "SimpleBox";
        internal const string SimpleSphereName = "SimpleSphere";
        internal const string SimpleCylinderName = "SimpleCylinder";

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        internal static void Initialize()
        {
            DataMap = new Dictionary<World, SimpleDrawSystemManagedData>();
            
            // Shader properties
            ColorPropertyId = Shader.PropertyToID("_Color");
            ObjectToWorldPropertyId = Shader.PropertyToID("unity_ObjectToWorld");
            WorldToObjectPropertyId = Shader.PropertyToID("unity_WorldToObject");

            // Materials
            SimpleDrawUnlitMaterial = Resources.Load<Material>(SimpleDrawUnlitMaterialName);

            // Meshes
            SimpleTriMesh = Resources.Load<Mesh>(SimpleTriName);
            SimpleQuadMesh = Resources.Load<Mesh>(SimpleQuadName);
            SimpleBoxMesh = Resources.Load<Mesh>(SimpleBoxName);
            SimpleSphereMesh = Resources.Load<Mesh>(SimpleSphereName);
            SimpleCylinderMesh = Resources.Load<Mesh>(SimpleCylinderName);
        }
    }

    internal class SimpleDrawSystemManagedData
    {
        internal BatchRendererGroup BRG;
        internal GraphicsBuffer GraphicsBuffer;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public unsafe partial struct SimpleDrawSystem : ISystem, ISystemStartStop
    {
        internal struct Singleton : IComponentData
        {
            internal BatchID _batchId;
            
            internal BatchMaterialID _unlitMaterialID;
            
            internal BatchMeshID _triMeshID;
            internal BatchMeshID _quadMeshID;
            internal BatchMeshID _boxMeshID;
            internal BatchMeshID _sphereMeshID;
            internal BatchMeshID _cylinderMeshID;
        }

        // TODO; 
        private const int kSizeOfMatrix = sizeof(float) * 4 * 4;
        private const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;
        private const int kSizeOfFloat4 = sizeof(float) * 4;
        private const int kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;
        private const int kExtraBytes = kSizeOfMatrix * 2;
        private const int kNumInstances = 3;

        public void OnStartRunning(ref SystemState state)
        {
            Singleton singleton = new Singleton();
            
            if (SimpleDrawSystemManagedDataStore.DataMap.TryGetValue(state.World, out SimpleDrawSystemManagedData data))
            {
            }
            else
            {
                data = new SimpleDrawSystemManagedData();
                SimpleDrawSystemManagedDataStore.DataMap.Add(state.World, data);
            } 

            data.BRG = new BatchRendererGroup(this.OnPerformCulling, IntPtr.Zero);
            
            // Shader properties

            // Register default materials
            singleton._unlitMaterialID = data.BRG.RegisterMaterial(SimpleDrawSystemManagedDataStore.SimpleDrawUnlitMaterial);

            // Register default meshes
            singleton._triMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleTriMesh);
            singleton._quadMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleQuadMesh);
            singleton._boxMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleBoxMesh);
            singleton._sphereMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleSphereMesh);
            singleton._cylinderMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleCylinderMesh);

            // Init graphics buffer
            data.GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                SimpleDrawUtilities.CalculateGraphicsBufferCountForBytes(kBytesPerInstance, kNumInstances, kExtraBytes),
                sizeof(int));
            
            // Create batches
            CreateBatch(data, ref singleton);

            SimpleDrawSystemManagedDataStore.DataMap[state.World] = data;
            
            // Create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, singleton);
        }

        public void OnStopRunning(ref SystemState state)
        { 
            if (SimpleDrawSystemManagedDataStore.DataMap.TryGetValue(state.World, out SimpleDrawSystemManagedData data))
            {
                data.BRG.Dispose();
                data.GraphicsBuffer.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        { }

        public JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            Singleton singleton = SystemAPI.GetSingleton<Singleton>();
            DoCulling(rendererGroup, cullingContext, cullingOutput, userContext, in singleton);
            return new JobHandle();
            
            // return new SimpleDrawCullingJob
            // {
            //     NumInstances = kNumInstances,
            //     BatchID = _batchId,
            //     MeshID = _boxMeshID,
            //     MaterialID = _unlitMaterialID,
            //     
            //     CullingContext = cullingContext,
            //     CullingOutput = cullingOutput,
            // }.Schedule(1, 1, default);
        }

        private void DoCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext,
            in Singleton singleton) 
        {

            // UnsafeUtility.Malloc() requires an alignment, so use the largest integer type's alignment
            // which is a reasonable default.
            int alignment = UnsafeUtility.AlignOf<long>();

            // Acquire a pointer to the BatchCullingOutputDrawCommands struct so you can easily
            // modify it directly.
            BatchCullingOutputDrawCommands* drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();

            // Allocate memory for the output arrays. In a more complicated implementation, you would calculate
            // the amount of memory to allocate dynamically based on what is visible.
            // This example assumes that all of the instances are visible and thus allocates
            // memory for each of them. The necessary allocations are as follows:
            // - a single draw command (which draws kNumInstances instances)
            // - a single draw range (which covers our single draw command)
            // - kNumInstances visible instance indices.
            // You must always allocate the arrays using Allocator.TempJob.
            drawCommands->drawCommands =
                (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment,
                    Allocator.TempJob);
            drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(),
                alignment, Allocator.TempJob);
            drawCommands->visibleInstances =
                (int*)UnsafeUtility.Malloc(kNumInstances * sizeof(int), alignment, Allocator.TempJob);
            drawCommands->drawCommandPickingInstanceIDs = null;

            drawCommands->drawCommandCount = 1;
            drawCommands->drawRangeCount = 1;
            drawCommands->visibleInstanceCount = kNumInstances;

            // This example doens't use depth sorting, so it leaves instanceSortingPositions as null.
            drawCommands->instanceSortingPositions = null;
            drawCommands->instanceSortingPositionFloatCount = 0;

            // Configure the single draw command to draw kNumInstances instances
            // starting from offset 0 in the array, using the batch, material and mesh
            // IDs registered in the Start() method. It doesn't set any special flags.
            drawCommands->drawCommands[0].visibleOffset = 0;
            drawCommands->drawCommands[0].visibleCount = kNumInstances;
            drawCommands->drawCommands[0].batchID = singleton._batchId;
            drawCommands->drawCommands[0].materialID = singleton._unlitMaterialID;
            drawCommands->drawCommands[0].meshID = singleton._boxMeshID;
            drawCommands->drawCommands[0].submeshIndex = 0;
            drawCommands->drawCommands[0].splitVisibilityMask = 0xff;
            drawCommands->drawCommands[0].flags = 0;
            drawCommands->drawCommands[0].sortingPosition = 0;

            // Configure the single draw range to cover the single draw command which
            // is at offset 0.
            drawCommands->drawRanges[0].drawCommandsType = BatchDrawCommandType.Direct;
            drawCommands->drawRanges[0].drawCommandsBegin = 0;
            drawCommands->drawRanges[0].drawCommandsCount = 1;

            // This example doesn't care about shadows or motion vectors, so it leaves everything
            // at the default zero values, except the renderingLayerMask which it sets to all ones
            // so Unity renders the instances regardless of mask settings.
            drawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };

            // Finally, write the actual visible instance indices to the array. In a more complicated
            // implementation, this output would depend on what is visible, but this example
            // assumes that everything is visible.
            for (int i = 0; i < kNumInstances; ++i)
                drawCommands->visibleInstances[i] = i;
        }

        private void CreateBatch(SimpleDrawSystemManagedData brgData, ref Singleton singleton)
        {
            // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
            NativeArray<float4x4> zeroMatrix = new NativeArray<float4x4>(1, Allocator.Temp);
            zeroMatrix[0] = float4x4.zero;

            // Create transform matrices for three example instances.
            NativeArray<float4x4> matrices = new NativeArray<float4x4>(kNumInstances, Allocator.Temp);
            matrices[0] = float4x4.Translate(new float3(-2f, 1f, 0f));
            matrices[1] = float4x4.Translate(new float3(0f, 1f, 0f));
            matrices[2] = float4x4.Translate(new float3(2f, 1f, 0f));

            // Convert the transform matrices into the packed format that the shader expects.
            NativeArray<float3x4> objectToWorlds = new NativeArray<float3x4>(kNumInstances, Allocator.Temp);
            objectToWorlds[0] = SimpleDrawUtilities.ToPackedMatrix(matrices[0]);
            objectToWorlds[1] = SimpleDrawUtilities.ToPackedMatrix(matrices[1]);
            objectToWorlds[2] = SimpleDrawUtilities.ToPackedMatrix(matrices[2]);

            // Also create packed inverse matrices.
            NativeArray<float3x4> worldToObjects = new NativeArray<float3x4>(kNumInstances, Allocator.Temp);
            worldToObjects[0] = SimpleDrawUtilities.ToPackedMatrix(math.inverse(matrices[0]));
            worldToObjects[1] = SimpleDrawUtilities.ToPackedMatrix(math.inverse(matrices[1]));
            worldToObjects[2] = SimpleDrawUtilities.ToPackedMatrix(math.inverse(matrices[2]));

            // Make all instances have unique colors.
            NativeArray<float4> colors = new NativeArray<float4>(kNumInstances, Allocator.Temp);
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
            uint byteAddressWorldToObject = byteAddressObjectToWorld + kSizeOfPackedMatrix * kNumInstances;
            uint byteAddressColor = byteAddressWorldToObject + kSizeOfPackedMatrix * kNumInstances;

            // Upload the instance data to the GraphicsBuffer so the shader can load them.
            brgData.GraphicsBuffer.SetData(zeroMatrix, 0, 0, 1);
            brgData.GraphicsBuffer.SetData(objectToWorlds, 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix),
                objectToWorlds.Length);
            brgData.GraphicsBuffer.SetData(worldToObjects, 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix),
                worldToObjects.Length);
            brgData.GraphicsBuffer.SetData(colors, 0, (int)(byteAddressColor / kSizeOfFloat4), colors.Length);

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
            singleton._batchId = brgData.BRG.AddBatch(metadatas, brgData.GraphicsBuffer.bufferHandle);

            zeroMatrix.Dispose();
            matrices.Dispose();
            objectToWorlds.Dispose();
            worldToObjects.Dispose();
            colors.Dispose();
        }
    }

    [BurstCompile]
    internal unsafe struct SimpleDrawCullingJob : IJobParallelFor
    {
        public int NumInstances;
        public BatchID BatchID;
        public BatchMaterialID MaterialID;
        public BatchMeshID MeshID;
        
        [NativeDisableUnsafePtrRestriction]
        public BatchCullingContext CullingContext;
        
        [NativeDisableUnsafePtrRestriction]
        public BatchCullingOutput CullingOutput;

        public void Execute(int index)
        {
            // UnsafeUtility.Malloc() requires an alignment, so use the largest integer type's alignment
            // which is a reasonable default.
            int alignment = UnsafeUtility.AlignOf<long>();

            // Acquire a pointer to the BatchCullingOutputDrawCommands struct so you can easily
            // modify it directly.
            BatchCullingOutputDrawCommands* drawCommands =
                (BatchCullingOutputDrawCommands*)CullingOutput.drawCommands.GetUnsafePtr();

            // Allocate memory for the output arrays. In a more complicated implementation, you would calculate
            // the amount of memory to allocate dynamically based on what is visible.
            // This example assumes that all of the instances are visible and thus allocates
            // memory for each of them. The necessary allocations are as follows:
            // - a single draw command (which draws kNumInstances instances)
            // - a single draw range (which covers our single draw command)
            // - kNumInstances visible instance indices.
            // You must always allocate the arrays using Allocator.TempJob.
            drawCommands->drawCommands =
                (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment,
                    Allocator.TempJob);
            drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(),
                alignment, Allocator.TempJob);
            drawCommands->visibleInstances =
                (int*)UnsafeUtility.Malloc(NumInstances * sizeof(int), alignment, Allocator.TempJob);
            drawCommands->drawCommandPickingInstanceIDs = null;

            drawCommands->drawCommandCount = 1;
            drawCommands->drawRangeCount = 1;
            drawCommands->visibleInstanceCount = NumInstances;

            // This example doens't use depth sorting, so it leaves instanceSortingPositions as null.
            drawCommands->instanceSortingPositions = null;
            drawCommands->instanceSortingPositionFloatCount = 0;

            // Configure the single draw command to draw kNumInstances instances
            // starting from offset 0 in the array, using the batch, material and mesh
            // IDs registered in the Start() method. It doesn't set any special flags.
            drawCommands->drawCommands[0].visibleOffset = 0;
            drawCommands->drawCommands[0].visibleCount = (uint)NumInstances;
            drawCommands->drawCommands[0].batchID = BatchID;
            drawCommands->drawCommands[0].materialID = MaterialID;
            drawCommands->drawCommands[0].meshID = MeshID;
            drawCommands->drawCommands[0].submeshIndex = 0;
            drawCommands->drawCommands[0].splitVisibilityMask = 0xff;
            drawCommands->drawCommands[0].flags = 0;
            drawCommands->drawCommands[0].sortingPosition = 0;

            // Configure the single draw range to cover the single draw command which
            // is at offset 0.
            drawCommands->drawRanges[0].drawCommandsType = BatchDrawCommandType.Direct;
            drawCommands->drawRanges[0].drawCommandsBegin = 0;
            drawCommands->drawRanges[0].drawCommandsCount = 1;

            // This example doesn't care about shadows or motion vectors, so it leaves everything
            // at the default zero values, except the renderingLayerMask which it sets to all ones
            // so Unity renders the instances regardless of mask settings.
            drawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };

            // Finally, write the actual visible instance indices to the array. In a more complicated
            // implementation, this output would depend on what is visible, but this example
            // assumes that everything is visible.
            for (int i = 0; i < NumInstances; ++i)
            {
                drawCommands->visibleInstances[i] = i;
            }
        }
    }
}