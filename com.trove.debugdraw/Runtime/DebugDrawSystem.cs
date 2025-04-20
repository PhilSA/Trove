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

namespace Trove.DebugDraw
{
    internal static class DebugDrawSystemManagedDataStore
    {
        internal static Dictionary<World, DebugDrawSystemManagedData> DataMap;

        internal static int ColorPropertyId;
        internal static int ObjectToWorldPropertyId;
        internal static int WorldToObjectPropertyId;
        internal static int PositionsPropertyId;
        internal static int NormalsPropertyId;
        internal static int TangentsPropertyId;
        internal static int BaseIndexPropertyId;

        internal static Material DebugDrawUnlitLineMaterial;
        internal static Material DebugDrawUnlitTriMaterial;
        internal static Material DebugDrawUnlitMaterial;

        internal static Mesh SimpleBoxMesh;
        internal static Mesh SimpleSphereMesh;
        internal static Mesh SimpleCylinderMesh;

        internal const string DebugDrawUnlitLineMaterialName = "DebugDrawUnlitLineURP";
        internal const string DebugDrawUnlitTriMaterialName = "DebugDrawUnlitTriURP";
        internal const string DebugDrawUnlitMaterialName = "DebugDrawUnlitURP";

        internal const string SimpleBoxName = "DebugBox";

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        internal static void Initialize()
        {
            DataMap = new Dictionary<World, DebugDrawSystemManagedData>();
            
            // Shader properties
            ColorPropertyId = Shader.PropertyToID("_Color");
            ObjectToWorldPropertyId = Shader.PropertyToID("unity_ObjectToWorld");
            WorldToObjectPropertyId = Shader.PropertyToID("unity_WorldToObject");
            PositionsPropertyId = Shader.PropertyToID("_Positions");
            NormalsPropertyId = Shader.PropertyToID("_Normals");
            TangentsPropertyId = Shader.PropertyToID("_Tangents"); 
            BaseIndexPropertyId = Shader.PropertyToID("_BaseIndex");

            // Materials
            DebugDrawUnlitLineMaterial = Resources.Load<Material>(DebugDrawUnlitLineMaterialName);
            DebugDrawUnlitTriMaterial = Resources.Load<Material>(DebugDrawUnlitTriMaterialName);
            DebugDrawUnlitMaterial = Resources.Load<Material>(DebugDrawUnlitMaterialName);

            // Meshes
            SimpleBoxMesh = Resources.Load<Mesh>(SimpleBoxName);
        }
    }

    internal class DebugDrawSystemManagedData
    {
        internal BatchRendererGroup BRG;
        internal GraphicsBuffer PositionsGraphicsBuffer;
        internal GraphicsBuffer LinesGraphicsBuffer;
        internal GraphicsBuffer TrisGraphicsBuffer;
        internal GraphicsBuffer BoxesGraphicsBuffer;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public unsafe partial struct DebugDrawSystem : ISystem, ISystemStartStop
    {
        internal struct Singleton : IComponentData
        {
            internal DebugDrawProceduralLinesBatch UnlitLinesBatch;
            internal DebugDrawProceduralLinesBatch UnlitTrisBatch;
            internal DebugDrawMeshBatch UnlitBoxMeshBatch;
        }

        // TODO; 
        private const int kNumInstances = 3;

        public void OnStartRunning(ref SystemState state)
        {
            Singleton singleton = new Singleton();
            
            if (DebugDrawSystemManagedDataStore.DataMap.TryGetValue(state.World, out DebugDrawSystemManagedData data))
            {
            }
            else
            {
                data = new DebugDrawSystemManagedData();
                DebugDrawSystemManagedDataStore.DataMap.Add(state.World, data);
            } 

            data.BRG = new BatchRendererGroup(this.OnPerformCulling, IntPtr.Zero);
            
            // Register batch resources ids
            {
                singleton.UnlitLinesBatch = new DebugDrawProceduralLinesBatch(
                    default,
                    data.BRG.RegisterMaterial(DebugDrawSystemManagedDataStore.DebugDrawUnlitLineMaterial), 
                    default);
                singleton.UnlitTrisBatch = new DebugDrawProceduralLinesBatch(
                    default, 
                    data.BRG.RegisterMaterial(DebugDrawSystemManagedDataStore.DebugDrawUnlitTriMaterial), 
                    default);
                singleton.UnlitBoxMeshBatch = new DebugDrawMeshBatch(
                    default,
                    data.BRG.RegisterMaterial(DebugDrawSystemManagedDataStore.DebugDrawUnlitMaterial),
                    data.BRG.RegisterMesh(DebugDrawSystemManagedDataStore.SimpleBoxMesh));
            }

            // Init graphics buffers
            int instanceBufferFloat4sLength = 4 + (kNumInstances * (3 + 3 + 1));
            int instanceBufferBytesLength = instanceBufferFloat4sLength * 16;
            data.LinesGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                instanceBufferBytesLength, 
                4);
            data.TrisGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                instanceBufferBytesLength,
                4);
            data.BoxesGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                instanceBufferBytesLength,
                4);
            data.PositionsGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                kNumInstances * 2, // TODO: lines + tris count
                4 * 4);
             
            // Create batches
            DebugDrawUtilities.CreateDrawLinesBatch(
                data.BRG, 
                data.LinesGraphicsBuffer, 
                data.PositionsGraphicsBuffer,
                ref singleton.UnlitLinesBatch.BatchId,
                kNumInstances);
            // DebugDrawUtilities.CreateDrawMeshBatch(
            //     data.BRG, 
            //     data.BoxesGraphicsBuffer, 
            //     ref singleton.UnlitBoxMeshBatch.BatchId, 
            //     kNumInstances);

            singleton.UnlitLinesBatch.PositionsBufferHandle = data.PositionsGraphicsBuffer.bufferHandle;
            singleton.UnlitTrisBatch.PositionsBufferHandle = data.PositionsGraphicsBuffer.bufferHandle;

            DebugDrawSystemManagedDataStore.DataMap[state.World] = data;
            
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
                    Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.PositionsPropertyId, data.PositionsGraphicsBuffer);
                    // Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.NormalsPropertyId, _gpuNormals);
                    // Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.TangentsPropertyId, _gpuTangents);
                }
                DebugDrawSystemManagedDataStore.DebugDrawUnlitLineMaterial.SetInt(DebugDrawSystemManagedDataStore.BaseIndexPropertyId, 0);
            }
        }

        public void OnStopRunning(ref SystemState state)
        { 
            if (DebugDrawSystemManagedDataStore.DataMap.TryGetValue(state.World, out DebugDrawSystemManagedData data))
            {
                if (SystemAPI.TryGetSingleton(out Singleton singleton))
                {
                    data.BRG.UnregisterMaterial(singleton.UnlitLinesBatch.MaterialID);
                    data.BRG.UnregisterMaterial(singleton.UnlitTrisBatch.MaterialID);
                    data.BRG.UnregisterMaterial(singleton.UnlitBoxMeshBatch.MaterialID);
                    data.BRG.UnregisterMesh(singleton.UnlitBoxMeshBatch.MeshID);
                }

                data.BRG.Dispose();
                data.LinesGraphicsBuffer.Dispose();
                data.PositionsGraphicsBuffer.Dispose();
                data.TrisGraphicsBuffer.Dispose();
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
            if (!SystemAPI.HasSingleton<Singleton>())
                return default;
            
            Singleton singleton = SystemAPI.GetSingleton<Singleton>();
            
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
            
            return new DebugDrawCullingJob
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
    internal unsafe struct DebugDrawCullingJob : IJob
    {
        public int NumInstances;
        
        public DebugDrawProceduralLinesBatch LinesBatchData;
        public DebugDrawMeshBatch MeshBatchData;

        [NativeDisableUnsafePtrRestriction] 
        public BatchCullingOutputDrawCommands* DrawCommands;
        
        [NativeDisableUnsafePtrRestriction]
        public IntPtr UserContext;

        public void Execute()
        { 
            DrawCommands->drawCommandPickingInstanceIDs = null;

            DrawCommands->drawCommandCount = 1;
            DrawCommands->proceduralDrawCommandCount = 0;
            DrawCommands->drawRangeCount = 1;
            DrawCommands->visibleInstanceCount = NumInstances;
            
            // This example doens't use depth sorting, so it leaves instanceSortingPositions as null.
            DrawCommands->instanceSortingPositions = null;
            DrawCommands->instanceSortingPositionFloatCount = 0;

            DebugDrawUtilities.DrawLinesCommand(
                DrawCommands, 
                UserContext, 
                LinesBatchData, 
                NumInstances);
            // DebugDrawUtilities.DrawMeshCommand(
            //     DrawCommands, 
            //     UserContext, 
            //     MeshBatchData, 
            //     NumInstances);

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