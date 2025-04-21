using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
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
        internal GraphicsBuffer Colors1GraphicsBuffer;
        internal GraphicsBuffer Colors2GraphicsBuffer;
        internal GraphicsBuffer InstancesGraphicsBuffer;

        internal void Dispose()
        {
            BRG.Dispose();
            Positions1GraphicsBuffer.Dispose();
            Positions2GraphicsBuffer.Dispose();
            Colors1GraphicsBuffer.Dispose();
            Colors2GraphicsBuffer.Dispose();
            InstancesGraphicsBuffer.Dispose();
        }
    }
    
    public unsafe struct DebugDrawSingleton : IComponentData
    {
        internal int UsedBuffers;
        internal DebugDrawProceduralLinesBatch UnlitLinesBatch;

        internal UnsafeList<DebugDrawGroup> DebugDrawGroups;
        internal int TotalLineElementsCount;
        
        internal int* PreventInJobsPtr;

        public DebugDrawGroup AllocateDebugDrawGroup(int initialCapacity = 16)
        {
            DebugDrawGroup newGroup = new DebugDrawGroup();
            newGroup.IsDirty = new NativeReference<bool>(Allocator.Persistent);
            newGroup.LinePositions = new NativeList<float4>(initialCapacity, Allocator.Persistent);
            newGroup.LineColors = new NativeList<float4>(initialCapacity, Allocator.Persistent);
                
            DebugDrawGroups.Add(newGroup);
            return newGroup;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public unsafe partial struct DebugDrawSystem : ISystem, ISystemStartStop
    {

        private ulong WorldSequenceNumber;
        
        
        public void OnStartRunning(ref SystemState state)
        {
            WorldSequenceNumber = state.WorldUnmanaged.SequenceNumber;
            DebugDrawSystemManagedDataStore.Initialize();
            
            DebugDrawSingleton debugDrawSingleton = new DebugDrawSingleton();
            
            debugDrawSingleton.DebugDrawGroups = new UnsafeList<DebugDrawGroup>(16, Allocator.Persistent);
            
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
                debugDrawSingleton.UnlitLinesBatch = new DebugDrawProceduralLinesBatch(
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
                1024, // TODO: lines + tris count
                4 * 4);
            data.Positions2GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1024, // TODO: lines + tris count
                4 * 4);
            data.Colors1GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1024, // TODO: lines + tris count
                4 * 4);
            data.Colors2GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1024, // TODO: lines + tris count
                4 * 4);
            
            // Create batches
            DebugDrawUtilities.CreateDebugDrawBatch(
                data.BRG, 
                data.InstancesGraphicsBuffer, 
                ref debugDrawSingleton.UnlitLinesBatch.BatchId);

            DebugDrawSystemManagedDataStore.DataMap[WorldSequenceNumber] = data;
            
            // Create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, debugDrawSingleton);
        }

        public void OnStopRunning(ref SystemState state)
        { 
            if (DebugDrawSystemManagedDataStore.DataMap.TryGetValue(WorldSequenceNumber, out DebugDrawSystemManagedData data))
            {
                if (SystemAPI.TryGetSingleton(out DebugDrawSingleton singleton))
                {
                    for (int i = 0; i < singleton.DebugDrawGroups.Length; i++)
                    {
                        DebugDrawGroup group = singleton.DebugDrawGroups[i];
                        group.Dispose();
                    }

                    singleton.DebugDrawGroups.Dispose();
                    
                    data.BRG.UnregisterMaterial(singleton.UnlitLinesBatch.MaterialID);
                }

                data.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<DebugDrawSingleton>() &&
                DebugDrawSystemManagedDataStore.DataMap.TryGetValue(WorldSequenceNumber,
                    out DebugDrawSystemManagedData data))
            {
                ref DebugDrawSingleton debugDrawSingleton = ref SystemAPI.GetSingletonRW<DebugDrawSingleton>().ValueRW;
                
                // Check groups
                bool mustUpdateGPUData = false;
                debugDrawSingleton.TotalLineElementsCount = 0;
                for (int i = debugDrawSingleton.DebugDrawGroups.Length - 1; i >= 0; i--)
                {
                    DebugDrawGroup group = debugDrawSingleton.DebugDrawGroups[i];

                    if (group.IsDirty.Value)
                    {
                        mustUpdateGPUData = true;
                    }
                        
                    if (!group.LinePositions.IsCreated || !group.LineColors.IsCreated)
                    {
                        mustUpdateGPUData = true;
                        debugDrawSingleton.DebugDrawGroups.RemoveAtSwapBack(i);
                    }
                    else
                    {
                        debugDrawSingleton.TotalLineElementsCount += group.LinePositions.Length;
                    }
                }

                // Reallocate graphics buffers to accomodate new length if greater than existing
                if (debugDrawSingleton.TotalLineElementsCount > data.Positions1GraphicsBuffer.count)
                {
                    data.Positions1GraphicsBuffer.Dispose();
                    data.Positions2GraphicsBuffer.Dispose();
                    data.Colors1GraphicsBuffer.Dispose();
                    data.Colors2GraphicsBuffer.Dispose();
                    
                    data.Positions1GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalLineElementsCount, 
                        4 * 4);
                    data.Positions2GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalLineElementsCount, 
                        4 * 4);
                    data.Colors1GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalLineElementsCount, 
                        4 * 4);
                    data.Colors2GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalLineElementsCount, 
                        4 * 4);
                }

                // GPU buffer update job
                if(mustUpdateGPUData)
                {
                    // Double buffering
                    GraphicsBuffer readPositionsBuffer = data.Positions1GraphicsBuffer;
                    GraphicsBuffer writePositionsBuffer = data.Positions2GraphicsBuffer;
                    GraphicsBuffer readColorsBuffer = data.Colors1GraphicsBuffer;
                    GraphicsBuffer writeColorsBuffer = data.Colors2GraphicsBuffer;
                    switch (debugDrawSingleton.UsedBuffers)
                    {
                        case 0:
                            readPositionsBuffer = data.Positions1GraphicsBuffer;
                            writePositionsBuffer = data.Positions2GraphicsBuffer;
                            readColorsBuffer = data.Colors1GraphicsBuffer;
                            writeColorsBuffer = data.Colors2GraphicsBuffer;
                            break;
                        case 1:
                            readPositionsBuffer = data.Positions2GraphicsBuffer;
                            writePositionsBuffer = data.Positions1GraphicsBuffer;
                            readColorsBuffer = data.Colors2GraphicsBuffer;
                            writeColorsBuffer = data.Colors1GraphicsBuffer;
                            break;
                    }
                    
                    JobHandle job = new UpdateBuffersJob
                    {
                        DebugDrawGroups = debugDrawSingleton.DebugDrawGroups,
                        PositionsBuffer = writePositionsBuffer.LockBufferForWrite<float4>(0, writePositionsBuffer.count),
                        ColorsBuffer = writeColorsBuffer.LockBufferForWrite<float4>(0, writeColorsBuffer.count),
                    }.Schedule(default);
                    job.Complete();

                    writePositionsBuffer.UnlockBufferAfterWrite<float4>(writePositionsBuffer.count);
                    writeColorsBuffer.UnlockBufferAfterWrite<float4>(writeColorsBuffer.count);

                    // TODO: gles
                    // if (UseConstantBuffer) 
                    // {
                    //     Shader.SetGlobalConstantBuffer(positionsID, _gpuPositions, 0, positions.Length * 4 * 4);
                    //     Shader.SetGlobalConstantBuffer(normalsID, _gpuNormals, 0, positions.Length * 4 * 4);
                    //     Shader.SetGlobalConstantBuffer(tangentsID, _gpuTangents, 0, positions.Length * 4 * 4);
                    // }
                    // else
                    {
                        Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.PositionsPropertyId,
                            writePositionsBuffer);
                        Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.ColorsPropertyId,
                            writeColorsBuffer);
                    }
                    DebugDrawSystemManagedDataStore.DebugDrawUnlitLineMaterial.SetInt(
                        DebugDrawSystemManagedDataStore.BaseIndexPropertyId, 0);
                
                    debugDrawSingleton.UsedBuffers++;
                    debugDrawSingleton.UsedBuffers = debugDrawSingleton.UsedBuffers % 2;
                }
            }
        }

        public JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            if (SystemAPI.HasSingleton<DebugDrawSingleton>())
            {
                DebugDrawSingleton debugDrawSingleton = SystemAPI.GetSingleton<DebugDrawSingleton>();

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

                // Configure the single draw command to draw kNumInstances instances
                // starting from offset 0 in the array, using the batch, material and mesh
                // IDs registered in the Start() method. It doesn't set any special flags.
                drawCommands->proceduralDrawCommands[0] = new BatchDrawCommandProcedural
                {
                    flags = BatchDrawCommandFlags.None,
                    batchID = debugDrawSingleton.UnlitLinesBatch.BatchId,
                    materialID = debugDrawSingleton.UnlitLinesBatch.MaterialID,
                
                    sortingPosition = 0,
                    visibleCount = 1,
                    visibleOffset = 0,
                    splitVisibilityMask = 0xff,
                    lightmapIndex = 0,
                
                    topology = MeshTopology.Lines,
                    baseVertex = 0,
                    elementCount = (uint)(debugDrawSingleton.TotalLineElementsCount), 
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
                
                    // Render everything, no shadows or motion vectors
                    filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, },
                };
            }

            return default;
        }
    }

    [BurstCompile]
    internal unsafe struct UpdateBuffersJob : IJob
    {
        [ReadOnly]
        public UnsafeList<DebugDrawGroup> DebugDrawGroups;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> PositionsBuffer;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> ColorsBuffer;

        public void Execute()
        {
            int writeIndex = 0;
            for (int i = 0; i < DebugDrawGroups.Length; i++)
            {
                DebugDrawGroup group = DebugDrawGroups[i];
                
                void* src = group.LinePositions.GetUnsafePtr();
                void* dst = (byte*)PositionsBuffer.GetUnsafePtr() + (long)(writeIndex * DebugDrawUtilities.kSizeOfFloat4);
                UnsafeUtility.MemCpy(dst, src, DebugDrawUtilities.kSizeOfFloat4 * group.LinePositions.Length);
                
                src = group.LineColors.GetUnsafePtr();
                dst = (byte*)ColorsBuffer.GetUnsafePtr() + (long)(writeIndex * DebugDrawUtilities.kSizeOfFloat4);
                UnsafeUtility.MemCpy(dst, src, DebugDrawUtilities.kSizeOfFloat4 * group.LinePositions.Length);
                
                writeIndex += group.LinePositions.Length;
            }
        }
    }
}