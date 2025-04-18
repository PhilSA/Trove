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
            internal SimpleDrawBatch UnlitLineBatch;
            internal SimpleDrawBatch UnlitBoxBatch;
            internal SimpleDrawBatch UnlitSphereBatch;
            internal SimpleDrawBatch UnlitCylinderBatch;
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
                BatchMaterialID unlitMaterialID =
                    data.BRG.RegisterMaterial(SimpleDrawSystemManagedDataStore.SimpleDrawUnlitMaterial);
                BatchMeshID triMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleTriMesh);
                BatchMeshID quadMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleQuadMesh);
                BatchMeshID boxMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleBoxMesh);
                BatchMeshID sphereMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleSphereMesh);
                BatchMeshID cylinderMeshID = data.BRG.RegisterMesh(SimpleDrawSystemManagedDataStore.SimpleCylinderMesh);

                singleton.UnlitBoxBatch = new SimpleDrawBatch(default, unlitMaterialID, boxMeshID);
                singleton.UnlitSphereBatch = new SimpleDrawBatch(default, unlitMaterialID, sphereMeshID);
                singleton.UnlitCylinderBatch = new SimpleDrawBatch(default, unlitMaterialID, cylinderMeshID);
            }

            // Init graphics buffer
            data.GraphicsBuffer = SimpleDrawUtilities.CreateDrawMeshGraphicsBuffer(kNumInstances);
            
            // Create batches
            SimpleDrawUtilities.CreateDrawMeshBatch(data.BRG, data.GraphicsBuffer, ref singleton.UnlitBoxBatch.BatchId);

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
            drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(),
                alignment, Allocator.TempJob);
            drawCommands->visibleInstances =
                (int*)UnsafeUtility.Malloc(kNumInstances * sizeof(int), alignment, Allocator.TempJob);
            
            return new SimpleDrawCullingJob
            {
                NumInstances = kNumInstances,
                BatchData = singleton.UnlitBoxBatch,
                
                DrawCommands = drawCommands,
                UserContext = userContext,
            }.Schedule(default);
        }
    }

    [BurstCompile]
    internal unsafe struct SimpleDrawCullingJob : IJob
    {
        public int NumInstances;
        public SimpleDrawBatch BatchData;

        [NativeDisableUnsafePtrRestriction] 
        public BatchCullingOutputDrawCommands* DrawCommands;
        
        [NativeDisableUnsafePtrRestriction]
        public IntPtr UserContext;

        public void Execute()
        {
            SimpleDrawUtilities.DrawMeshCommand(
                DrawCommands, 
                UserContext, 
                BatchData, 
                NumInstances);
        }
    }
}