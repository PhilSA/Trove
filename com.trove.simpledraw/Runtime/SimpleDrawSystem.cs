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
        internal static int PositionsPropertyId;
        internal static int NormalsPropertyId;
        internal static int TangentsPropertyId;
        internal static int BaseIndexPropertyId;

        internal static Material SimpleDrawUnlitLineMaterial;
        internal static Material SimpleDrawUnlitTriMaterial;
        internal static Material SimpleDrawUnlitMaterial;

        internal static Mesh SimpleBoxMesh;
        internal static Mesh SimpleSphereMesh;
        internal static Mesh SimpleCylinderMesh;

        internal const string SimpleDrawUnlitLineMaterialName = "SimpleDrawUnlitLineURP";
        internal const string SimpleDrawUnlitTriMaterialName = "SimpleDrawUnlitTriURP";
        internal const string SimpleDrawUnlitMaterialName = "SimpleDrawUnlitURP";

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
            WorldToObjectPropertyId = Shader.PropertyToID("unity_WorldToObject");int positionsID = Shader.PropertyToID("_Positions");
            PositionsPropertyId = Shader.PropertyToID("_Positions");
            NormalsPropertyId = Shader.PropertyToID("_Normals");
            TangentsPropertyId = Shader.PropertyToID("_Tangents");
            BaseIndexPropertyId = Shader.PropertyToID("_BaseIndex");

            // Materials
            SimpleDrawUnlitLineMaterial = Resources.Load<Material>(SimpleDrawUnlitLineMaterialName);
            SimpleDrawUnlitTriMaterial = Resources.Load<Material>(SimpleDrawUnlitTriMaterialName);
            SimpleDrawUnlitMaterial = Resources.Load<Material>(SimpleDrawUnlitMaterialName);

            // Meshes
            SimpleBoxMesh = Resources.Load<Mesh>(SimpleBoxName);
            SimpleSphereMesh = Resources.Load<Mesh>(SimpleSphereName);
            SimpleCylinderMesh = Resources.Load<Mesh>(SimpleCylinderName);
        }
    }

    internal class SimpleDrawSystemManagedData
    {
        internal BatchRendererGroup BRG;
        internal GraphicsBuffer PositionsGraphicsBuffer;
        internal GraphicsBuffer LinesGraphicsBuffer;
        internal GraphicsBuffer TrisGraphicsBuffer;
        internal GraphicsBuffer BoxesGraphicsBuffer;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public unsafe partial struct SimpleDrawSystem : ISystem, ISystemStartStop
    {
        internal struct Singleton : IComponentData
        {
            internal SimpleDrawProceduralLinesBatch UnlitLinesBatch;
            internal SimpleDrawProceduralLinesBatch UnlitTrisBatch;
            
            internal SimpleDrawMeshBatch UnlitBoxMeshBatch;
            internal SimpleDrawMeshBatch UnlitSphereMeshBatch;
            internal SimpleDrawMeshBatch UnlitCylinderMeshBatch;
        }

        // TODO; 
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
            
            // Register batch resources ids
            {
                BatchMaterialID unlitLineMaterialID = data.BRG.RegisterMaterial(SimpleDrawSystemManagedDataStore.SimpleDrawUnlitLineMaterial);
                BatchMaterialID unlitTriMaterialID = data.BRG.RegisterMaterial(SimpleDrawSystemManagedDataStore.SimpleDrawUnlitTriMaterial);
                BatchMaterialID unlitMaterialID = data.BRG.RegisterMaterial(SimpleDrawSystemManagedDataStore.SimpleDrawUnlitMaterial);
                BatchMeshID boxMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleBoxMesh);
                BatchMeshID sphereMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleSphereMesh);
                BatchMeshID cylinderMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleCylinderMesh);

                singleton.UnlitLinesBatch = new SimpleDrawProceduralLinesBatch(default, unlitLineMaterialID, default);
                singleton.UnlitTrisBatch = new SimpleDrawProceduralLinesBatch(default, unlitTriMaterialID, default);
                
                singleton.UnlitBoxMeshBatch = new SimpleDrawMeshBatch(default, unlitMaterialID, boxMeshID);
                singleton.UnlitSphereMeshBatch = new SimpleDrawMeshBatch(default, unlitMaterialID, sphereMeshID);
                singleton.UnlitCylinderMeshBatch = new SimpleDrawMeshBatch(default, unlitMaterialID, cylinderMeshID);
            }

            // Init graphics buffers
            data.LinesGraphicsBuffer = SimpleDrawUtilities.CreateDrawLinesGraphicsBuffer(kNumInstances);
            data.PositionsGraphicsBuffer = SimpleDrawUtilities.CreateIndexGraphicsBuffer(kNumInstances * 2);
            data.TrisGraphicsBuffer = SimpleDrawUtilities.CreateDrawTrisGraphicsBuffer(kNumInstances);
            data.BoxesGraphicsBuffer = SimpleDrawUtilities.CreateDrawMeshGraphicsBuffer(kNumInstances);
            
            // Create batches
            SimpleDrawUtilities.CreateDrawLinesBatch(
                data.BRG, 
                data.LinesGraphicsBuffer, 
                data.PositionsGraphicsBuffer,
                ref singleton.UnlitLinesBatch.BatchId,
                kNumInstances);
            //SimpleDrawUtilities.CreateDrawMeshBatch(data.BRG, data.BoxesGraphicsBuffer, ref singleton.UnlitBoxMeshBatch.BatchId);

            singleton.UnlitLinesBatch.PositionsBufferHandle = data.PositionsGraphicsBuffer.bufferHandle;
            singleton.UnlitTrisBatch.PositionsBufferHandle = data.PositionsGraphicsBuffer.bufferHandle;

            SimpleDrawSystemManagedDataStore.DataMap[state.World] = data;
            
            // Create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, singleton);
            
             
            
            
            // Set procedural data
            {
                // TODO:
                // if (UseConstantBuffer)
                // {
                //     Shader.SetGlobalConstantBuffer(positionsID, _gpuPositions, 0, positions.Length * 4 * 4);
                //     Shader.SetGlobalConstantBuffer(normalsID, _gpuNormals, 0, positions.Length * 4 * 4);
                //     Shader.SetGlobalConstantBuffer(tangentsID, _gpuTangents, 0, positions.Length * 4 * 4);
                // }
                // else
                {
                    Shader.SetGlobalBuffer(SimpleDrawSystemManagedDataStore.PositionsPropertyId, data.PositionsGraphicsBuffer);
                    // Shader.SetGlobalBuffer(SimpleDrawSystemManagedDataStore.NormalsPropertyId, _gpuNormals);
                    // Shader.SetGlobalBuffer(SimpleDrawSystemManagedDataStore.TangentsPropertyId, _gpuTangents);
                }
                SimpleDrawSystemManagedDataStore.SimpleDrawUnlitLineMaterial.SetInt(SimpleDrawSystemManagedDataStore.BaseIndexPropertyId, 0);
            }
        }

        public void OnStopRunning(ref SystemState state)
        { 
            if (SimpleDrawSystemManagedDataStore.DataMap.TryGetValue(state.World, out SimpleDrawSystemManagedData data))
            {
                data.BRG.Dispose();
                data.BoxesGraphicsBuffer.Dispose();
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
            // SimpleDrawUtilities.DrawMeshCommand(
            //     cullingContext, 
            //     cullingOutput, 
            //     userContext, 
            //     singleton.UnlitBoxBatch, 
            //     kNumInstances);
            // return new JobHandle();
            
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
            drawCommands->proceduralDrawCommands =
                (BatchDrawCommandProcedural*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommandProcedural>(), alignment,
                    Allocator.TempJob);
            drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(),
                alignment, Allocator.TempJob);
            drawCommands->visibleInstances =
                (int*)UnsafeUtility.Malloc(kNumInstances * sizeof(int), alignment, Allocator.TempJob);
            
            return new SimpleDrawCullingJob
            {
                NumInstances = kNumInstances,
                
                LinesBatchData = singleton.UnlitLinesBatch,
                MeshBatchData = singleton.UnlitBoxMeshBatch,
                
                DrawCommands = drawCommands,
                UserContext = userContext,
            }.Schedule(default);
        }
    }

    [BurstCompile]
    internal unsafe struct SimpleDrawCullingJob : IJob
    {
        public int NumInstances;
        
        public SimpleDrawProceduralLinesBatch LinesBatchData;
        public SimpleDrawMeshBatch MeshBatchData;

        [NativeDisableUnsafePtrRestriction] 
        public BatchCullingOutputDrawCommands* DrawCommands;
        
        [NativeDisableUnsafePtrRestriction]
        public IntPtr UserContext;

        public void Execute()
        { 
            DrawCommands->drawCommandPickingInstanceIDs = null;

            DrawCommands->drawCommandCount = 0;
            DrawCommands->proceduralDrawCommandCount = 1;
            DrawCommands->drawRangeCount = 1;
            DrawCommands->visibleInstanceCount = NumInstances;
            
            // This example doens't use depth sorting, so it leaves instanceSortingPositions as null.
            DrawCommands->instanceSortingPositions = null;
            DrawCommands->instanceSortingPositionFloatCount = 0;

            // SimpleDrawUtilities.DrawMeshCommand(
            //     DrawCommands, 
            //     UserContext, 
            //     MeshBatchData, 
            //     NumInstances);
            SimpleDrawUtilities.DrawLinesCommand(
                DrawCommands, 
                UserContext, 
                LinesBatchData, 
                NumInstances);

            // Finally, write the actual visible instance indices to the array. In a more complicated
            // implementation, this output would depend on what is visible, but this example
            // assumes that everything is visible.
            for (int i = 0; i < NumInstances; ++i)
            {
                DrawCommands->visibleInstances[i] = i;
            }
        }
    }
}