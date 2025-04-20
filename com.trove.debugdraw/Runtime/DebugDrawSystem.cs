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
        internal static Dictionary<ulong, DebugDrawSystemManagedData> DataMap;

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

        private static bool _hasInitialized;

        internal const string DebugDrawUnlitLineMaterialName = "DebugDrawUnlitLineURP";
        internal const string DebugDrawUnlitTriMaterialName = "DebugDrawUnlitTriURP";
        internal const string DebugDrawUnlitMaterialName = "DebugDrawUnlitURP";

        internal static void Initialize()
        {
            if (!_hasInitialized)
            {
                DataMap = new Dictionary<ulong, DebugDrawSystemManagedData>();

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
                
                _hasInitialized = true;
            }
        }
    }

    internal class DebugDrawSystemManagedData
    {
        internal BatchRendererGroup BRG;
        internal GraphicsBuffer Positions1GraphicsBuffer;
        internal GraphicsBuffer Positions2GraphicsBuffer;
        internal GraphicsBuffer ColorsGraphicsBuffer;
        internal GraphicsBuffer InstancesGraphicsBuffer;

        internal void Dispose()
        {
            BRG.Dispose();
            Positions1GraphicsBuffer.Dispose();
            Positions2GraphicsBuffer.Dispose();
            ColorsGraphicsBuffer.Dispose();
            InstancesGraphicsBuffer.Dispose();
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public unsafe partial struct DebugDrawSystem : ISystem, ISystemStartStop
    {
        internal struct Singleton : IComponentData
        {
            internal int UsedBuffers;
            internal DebugDrawProceduralLinesBatch UnlitLinesBatch;
        }

        private ulong WorldSequenceNumber;
        
        // TODO; 
        private const int kNumLines = 1000000;
        
        public void OnStartRunning(ref SystemState state)
        {
            WorldSequenceNumber = state.WorldUnmanaged.SequenceNumber;
            DebugDrawSystemManagedDataStore.Initialize();
            
            Singleton singleton = new Singleton();
            
            if (DebugDrawSystemManagedDataStore.DataMap.TryGetValue(WorldSequenceNumber, out DebugDrawSystemManagedData data))
            {
                // Dispose old data
                data.Dispose();
            }
            else
            {
                data = new DebugDrawSystemManagedData();
                DebugDrawSystemManagedDataStore.DataMap.Add(WorldSequenceNumber, data);
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
            data.Positions1GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                kNumLines * 2, // TODO: lines + tris count
                4 * 4);
            data.Positions2GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                kNumLines * 2, // TODO: lines + tris count
                4 * 4);
            data.ColorsGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                kNumLines * 2, // TODO: lines + tris count
                4 * 4);
             
            // Create batches
            DebugDrawUtilities.CreateDrawLinesBatch(
                data.BRG, 
                data.InstancesGraphicsBuffer, 
                data.Positions1GraphicsBuffer,
                data.ColorsGraphicsBuffer,
                ref singleton.UnlitLinesBatch.BatchId,
                kNumLines);

            DebugDrawSystemManagedDataStore.DataMap[WorldSequenceNumber] = data;
            
            // Create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, singleton);
        }

        public void OnStopRunning(ref SystemState state)
        { 
            if (DebugDrawSystemManagedDataStore.DataMap.TryGetValue(WorldSequenceNumber, out DebugDrawSystemManagedData data))
            {
                if (SystemAPI.TryGetSingleton(out Singleton singleton))
                {
                    data.BRG.UnregisterMaterial(singleton.UnlitLinesBatch.MaterialID);
                }

                data.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<Singleton>() &&
                DebugDrawSystemManagedDataStore.DataMap.TryGetValue(WorldSequenceNumber,
                    out DebugDrawSystemManagedData data))
            {
                ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
                
                GraphicsBuffer readPositionsBuffer = data.Positions1GraphicsBuffer;
                GraphicsBuffer writePositionsBuffer = data.Positions2GraphicsBuffer;
                switch (singleton.UsedBuffers)
                {
                    case 0:
                        readPositionsBuffer = data.Positions1GraphicsBuffer;
                        writePositionsBuffer = data.Positions2GraphicsBuffer;
                        break;
                    case 1:
                        readPositionsBuffer = data.Positions2GraphicsBuffer;
                        writePositionsBuffer = data.Positions1GraphicsBuffer;
                        break;
                }
                
                // TODO: double buffering
                {
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
                            Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.PositionsPropertyId,
                                readPositionsBuffer);
                            Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.ColorsPropertyId,
                                data.ColorsGraphicsBuffer);
                        }
                        DebugDrawSystemManagedDataStore.DebugDrawUnlitLineMaterial.SetInt(
                            DebugDrawSystemManagedDataStore.BaseIndexPropertyId, 0);
                    }
                }

                 JobHandle job = new UpdateBuffersJob
                 {
                     ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
                     PositionsBuffer = writePositionsBuffer.LockBufferForWrite<float4>(0, kNumLines * 2),
                 }.Schedule(default);
                 job.Complete(); 
                
                writePositionsBuffer.UnlockBufferAfterWrite<float4>(kNumLines * 2);

                singleton.UsedBuffers++;
                singleton.UsedBuffers = singleton.UsedBuffers % 2;
            }
        }

        public JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            if (SystemAPI.HasSingleton<Singleton>())
            {
                Singleton singleton = SystemAPI.GetSingleton<Singleton>();

                // Allocate draw commands
                BatchCullingOutputDrawCommands* drawCommands =
                    (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
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
                            UnsafeUtility.SizeOf<BatchDrawCommandProcedural>() *
                            drawCommands->proceduralDrawCommandCount,
                            alignment, Allocator.TempJob);
                    drawCommands->drawRanges =
                        (BatchDrawRange*)UnsafeUtility.Malloc(
                            UnsafeUtility.SizeOf<BatchDrawRange>() * drawCommands->drawRangeCount,
                            alignment, Allocator.TempJob);
                    drawCommands->visibleInstances =
                        (int*)UnsafeUtility.Malloc(sizeof(int) * 1,
                            alignment, Allocator.TempJob);

                    drawCommands->visibleInstances[0] = 0;
                }

                DebugDrawUtilities.DrawLinesCommand(
                    drawCommands, 
                    userContext, 
                    singleton.UnlitLinesBatch, 
                    kNumLines);
            }

            return default;
        }
    }

    [BurstCompile]
    internal struct UpdateBuffersJob : IJob
    {
        public float ElapsedTime;
        public NativeArray<float4> PositionsBuffer;

        public void Execute()
        {
            int numLines = PositionsBuffer.Length / 2;
            int resolution = (int)math.ceil(math.pow(numLines, 1f/3f));
            float spacing = 2f;
            for (int i = 0; i < numLines; i++)
            {
                float xStart = (i % resolution) * spacing;
                float zStart = ((i / resolution) % resolution) * spacing;
                float yStart = (i / (resolution * resolution)) * spacing;

                xStart += ElapsedTime;
                
                PositionsBuffer[(i*2)] = new float4(xStart, yStart, zStart, 0f);
                PositionsBuffer[(i*2)+1] = new float4(xStart, yStart + 1f, zStart, 0f);
            } 
        }
    }
}