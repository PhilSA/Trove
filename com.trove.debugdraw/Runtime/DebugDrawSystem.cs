using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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
        internal static int ColorsPropertyId;
        internal static int NormalsPropertyId;
        internal static int TangentsPropertyId;
        internal static int BaseIndexPropertyId;

        internal static Material DebugDrawUnlitLineMaterial;
        internal static Material DebugDrawUnlitTriMaterial;
        internal static Material DebugDrawUnlitMaterial;

        internal const string DebugDrawUnlitLineMaterialName = "DebugDrawUnlitLineURP";
        internal const string DebugDrawUnlitTriMaterialName = "DebugDrawUnlitTriURP";
        internal const string DebugDrawUnlitMaterialName = "DebugDrawUnlitURP";

        internal static void Initialize()
        {
            DataMap = new Dictionary<World, DebugDrawSystemManagedData>();
            
            // Shader properties
            ColorPropertyId = Shader.PropertyToID("_Color");
            ObjectToWorldPropertyId = Shader.PropertyToID("unity_ObjectToWorld");
            WorldToObjectPropertyId = Shader.PropertyToID("unity_WorldToObject");
            PositionsPropertyId = Shader.PropertyToID("_Positions");
            ColorsPropertyId = Shader.PropertyToID("_Colors");
            NormalsPropertyId = Shader.PropertyToID("_Normals");
            TangentsPropertyId = Shader.PropertyToID("_Tangents"); 
            BaseIndexPropertyId = Shader.PropertyToID("_BaseIndex");

            // Materials
            DebugDrawUnlitLineMaterial = Resources.Load<Material>(DebugDrawUnlitLineMaterialName);
            DebugDrawUnlitTriMaterial = Resources.Load<Material>(DebugDrawUnlitTriMaterialName);
            DebugDrawUnlitMaterial = Resources.Load<Material>(DebugDrawUnlitMaterialName);
        }
    }

    internal class DebugDrawSystemManagedData
    {
        internal BatchRendererGroup BRG;
        internal GraphicsBuffer PositionsGraphicsBuffer;
        internal GraphicsBuffer ColorsGraphicsBuffer;
        internal GraphicsBuffer InstancesGraphicsBuffer;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public unsafe partial struct DebugDrawSystem : ISystem, ISystemStartStop
    {
        internal struct Singleton : IComponentData
        {
            internal DebugDrawProceduralLinesBatch UnlitLinesBatch;
        }

        // TODO; 
        private const int kNumLines = 10000000;

        public void OnStartRunning(ref SystemState state)
        {
            DebugDrawSystemManagedDataStore.Initialize();
            
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
                    data.BRG.RegisterMaterial(DebugDrawSystemManagedDataStore.DebugDrawUnlitLineMaterial));
            }

            // TODO: combine all graphicsBuffers into one, and use offsets
            // Init graphics buffers
            int instanceBufferFloat4sLength = 4 + (3 + 3);
            int instanceBufferBytesLength = instanceBufferFloat4sLength * DebugDrawUtilities.kSizeOfFloat4;
            data.InstancesGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw, 
                instanceBufferBytesLength, 
                4);
            data.PositionsGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                kNumLines * 2, // TODO: lines + tris count
                4 * 4);
            data.ColorsGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                kNumLines * 2, // TODO: lines + tris count
                4 * 4);
             
            // Create batches
            DebugDrawUtilities.CreateDrawLinesBatch(
                data.BRG, 
                data.InstancesGraphicsBuffer, 
                data.PositionsGraphicsBuffer,
                data.ColorsGraphicsBuffer,
                ref singleton.UnlitLinesBatch.BatchId,
                kNumLines);
            
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
                    Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.ColorsPropertyId, data.ColorsGraphicsBuffer);
                }
                DebugDrawSystemManagedDataStore.DebugDrawUnlitLineMaterial.SetInt(DebugDrawSystemManagedDataStore.BaseIndexPropertyId, 0);
            }

            DebugDrawSystemManagedDataStore.DataMap[state.World] = data;
            
            // Create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, singleton);
        }

        public void OnStopRunning(ref SystemState state)
        { 
            if (DebugDrawSystemManagedDataStore.DataMap.TryGetValue(state.World, out DebugDrawSystemManagedData data))
            {
                if (SystemAPI.TryGetSingleton(out Singleton singleton))
                {
                    data.BRG.UnregisterMaterial(singleton.UnlitLinesBatch.MaterialID);
                }

                data.BRG.Dispose();
                data.InstancesGraphicsBuffer.Dispose();
                data.PositionsGraphicsBuffer.Dispose();
                data.ColorsGraphicsBuffer.Dispose();
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
            
            // Allocate draw commands
            BatchCullingOutputDrawCommands* drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            {
                int alignment = UnsafeUtility.AlignOf<long>();
                
                drawCommands->drawCommandPickingInstanceIDs = null;
                drawCommands->drawCommandCount = 0;
                drawCommands->proceduralDrawCommandCount = 1;
                drawCommands->drawRangeCount = 1;
                drawCommands->visibleInstanceCount = 1;
                drawCommands->instanceSortingPositions = null;
                drawCommands->instanceSortingPositionFloatCount = 0;

                drawCommands->proceduralDrawCommands =
                    (BatchDrawCommandProcedural*)UnsafeUtility.Malloc(
                        UnsafeUtility.SizeOf<BatchDrawCommandProcedural>() * drawCommands->proceduralDrawCommandCount,
                        alignment, Allocator.TempJob);
                drawCommands->drawRanges =
                    (BatchDrawRange*)UnsafeUtility.Malloc(
                        UnsafeUtility.SizeOf<BatchDrawRange>() * drawCommands->drawRangeCount,
                        alignment, Allocator.TempJob);
                drawCommands->visibleInstances =
                    (int*)UnsafeUtility.Malloc(sizeof(int) * 1,
                        alignment, Allocator.TempJob);
            }

            return new DebugDrawCullingJob
            {
                NumLines = kNumLines,
                
                LinesBatchData = singleton.UnlitLinesBatch,
                
                DrawCommands = drawCommands,
                UserContext = userContext,
            }.Schedule(default);
        }
    }

    [BurstCompile]
    internal unsafe struct DebugDrawCullingJob : IJob
    {
        public int NumLines;
        
        public DebugDrawProceduralLinesBatch LinesBatchData;

        [NativeDisableUnsafePtrRestriction] 
        public BatchCullingOutputDrawCommands* DrawCommands;
        
        [NativeDisableUnsafePtrRestriction]
        public IntPtr UserContext;

        public void Execute()
        { 

            DebugDrawUtilities.DrawLinesCommand(
                DrawCommands, 
                UserContext, 
                LinesBatchData, 
                NumLines);

            DrawCommands->visibleInstances[0] = 0;
        }
    }
}