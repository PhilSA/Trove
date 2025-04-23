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

        internal static int ObjectToWorldPropertyId;
        internal static int WorldToObjectPropertyId;
        internal static int LinePositionsPropertyId;
        internal static int LineColorsPropertyId;
        internal static int TrianglePositionsPropertyId;
        internal static int TriangleColorsPropertyId;

        internal static Material DebugDrawUnlitLineMaterial;
        internal static Material DebugDrawUnlitTriMaterial;

        private static bool _hasInitialized;

        internal static void Initialize()
        {
            if (!_hasInitialized)
            {
                DataMap = new Dictionary<ulong, DebugDrawSystemManagedData>();

                // Shader properties
                ObjectToWorldPropertyId = Shader.PropertyToID("unity_ObjectToWorld");
                WorldToObjectPropertyId = Shader.PropertyToID("unity_WorldToObject");
                LinePositionsPropertyId = Shader.PropertyToID("_LinePositions");
                LineColorsPropertyId = Shader.PropertyToID("_LineColors");
                TrianglePositionsPropertyId = Shader.PropertyToID("_TrianglePositions");
                TriangleColorsPropertyId = Shader.PropertyToID("_TriangleColors");

                // Materials
                DebugDrawUnlitLineMaterial = Resources.Load<Material>("DebugDrawUnlitLine");
                DebugDrawUnlitTriMaterial = Resources.Load<Material>("DebugDrawUnlitTri");

                _hasInitialized = true;
            }
        }
    }

    internal class DebugDrawSystemManagedData
    {
        internal BatchRendererGroup BRG;

        internal GraphicsBuffer LinePositions1GraphicsBuffer;
        internal GraphicsBuffer LinePositions2GraphicsBuffer;
        internal GraphicsBuffer LineColors1GraphicsBuffer;
        internal GraphicsBuffer LineColors2GraphicsBuffer;

        internal GraphicsBuffer TrianglePositions1GraphicsBuffer;
        internal GraphicsBuffer TrianglePositions2GraphicsBuffer;
        internal GraphicsBuffer TriangleColors1GraphicsBuffer;
        internal GraphicsBuffer TriangleColors2GraphicsBuffer;

        internal GraphicsBuffer LineInstancesGraphicsBuffer;
        internal GraphicsBuffer TriangleInstancesGraphicsBuffer;

        internal void Dispose()
        {
            BRG.Dispose();

            LinePositions1GraphicsBuffer.Dispose();
            LinePositions2GraphicsBuffer.Dispose();
            LineColors1GraphicsBuffer.Dispose();
            LineColors2GraphicsBuffer.Dispose();

            TrianglePositions1GraphicsBuffer.Dispose();
            TrianglePositions2GraphicsBuffer.Dispose();
            TriangleColors1GraphicsBuffer.Dispose();
            TriangleColors2GraphicsBuffer.Dispose();

            LineInstancesGraphicsBuffer.Dispose();
            TriangleInstancesGraphicsBuffer.Dispose();
        }
    }

    public unsafe struct DebugDrawSingleton : IComponentData
    {
        internal int UsedBuffers;
        internal DebugDrawProceduralBatch LinesBatch;
        internal DebugDrawProceduralBatch TrisBatch;

        internal UnsafeList<DebugDrawGroup> DebugDrawGroups;
        internal int TotalLineElementsCount;
        internal int TotalTriangleElementsCount;

        internal int* PreventInJobsPtr;

        public DebugDrawGroup AllocateDebugDrawGroup(int initialCapacity = 16)
        {
            DebugDrawGroup newGroup = new DebugDrawGroup();
            newGroup.IsDirty = new NativeReference<bool>(Allocator.Persistent);
            newGroup.LinePositions = new NativeList<float4>(initialCapacity, Allocator.Persistent);
            newGroup.LineColors = new NativeList<float4>(initialCapacity, Allocator.Persistent);
            newGroup.TrianglePositions = new NativeList<float4>(initialCapacity, Allocator.Persistent);
            newGroup.TriangleColors = new NativeList<float4>(initialCapacity, Allocator.Persistent);

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
                debugDrawSingleton.LinesBatch = new DebugDrawProceduralBatch(
                    default,
                    data.BRG.RegisterMaterial(DebugDrawSystemManagedDataStore.DebugDrawUnlitLineMaterial));
                debugDrawSingleton.TrisBatch = new DebugDrawProceduralBatch(
                    default,
                    data.BRG.RegisterMaterial(DebugDrawSystemManagedDataStore.DebugDrawUnlitTriMaterial));
            }

            // Init graphics buffers
            int instanceBufferFloat4sLength = 4 + (3 + 3);
            int instanceBufferIntsLength = instanceBufferFloat4sLength * 4;
            data.LineInstancesGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                instanceBufferIntsLength,
                4);
            data.TriangleInstancesGraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                instanceBufferIntsLength,
                4);

            data.LinePositions1GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1024,
                4 * 4);
            data.LinePositions2GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1024,
                4 * 4);
            data.LineColors1GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1024,
                4 * 4);
            data.LineColors2GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1024,
                4 * 4);

            data.TrianglePositions1GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1200,
                4 * 4);
            data.TrianglePositions2GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1200,
                4 * 4);
            data.TriangleColors1GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1200,
                4 * 4);
            data.TriangleColors2GraphicsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1200,
                4 * 4);

            // Create batches
            DebugDrawUtilities.CreateDebugDrawBatch(
                data.BRG,
                data.LineInstancesGraphicsBuffer,
                ref debugDrawSingleton.LinesBatch.BatchId);
            DebugDrawUtilities.CreateDebugDrawBatch(
                data.BRG,
                data.TriangleInstancesGraphicsBuffer,
                ref debugDrawSingleton.TrisBatch.BatchId);

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

                    data.BRG.UnregisterMaterial(singleton.LinesBatch.MaterialID);
                    data.BRG.UnregisterMaterial(singleton.TrisBatch.MaterialID);
                    data.BRG.Dispose();
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
                debugDrawSingleton.TotalTriangleElementsCount = 0;
                for (int i = debugDrawSingleton.DebugDrawGroups.Length - 1; i >= 0; i--)
                {
                    DebugDrawGroup group = debugDrawSingleton.DebugDrawGroups[i];

                    if (group.IsDirty.Value)
                    {
                        mustUpdateGPUData = true;
                        group.IsDirty.Value = false;
                    }

                    if (!group.LinePositions.IsCreated || !group.LineColors.IsCreated ||
                        !group.TrianglePositions.IsCreated || !group.TriangleColors.IsCreated)
                    {
                        mustUpdateGPUData = true;
                        debugDrawSingleton.DebugDrawGroups.RemoveAtSwapBack(i);
                    }
                    else
                    {
                        debugDrawSingleton.TotalLineElementsCount += group.LinePositions.Length;
                        debugDrawSingleton.TotalTriangleElementsCount += group.TrianglePositions.Length;
                    }
                }

                // Reallocate graphics buffers to accomodate new length if greater than existing
                if (debugDrawSingleton.TotalLineElementsCount > data.LinePositions1GraphicsBuffer.count)
                {
                    data.LinePositions1GraphicsBuffer.Dispose();
                    data.LinePositions2GraphicsBuffer.Dispose();
                    data.LineColors1GraphicsBuffer.Dispose();
                    data.LineColors2GraphicsBuffer.Dispose();

                    data.LinePositions1GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalLineElementsCount,
                        4 * 4);
                    data.LinePositions2GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalLineElementsCount,
                        4 * 4);
                    data.LineColors1GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalLineElementsCount,
                        4 * 4);
                    data.LineColors2GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalLineElementsCount,
                        4 * 4);
                }
                if (debugDrawSingleton.TotalTriangleElementsCount > data.TrianglePositions1GraphicsBuffer.count)
                {
                    data.TrianglePositions1GraphicsBuffer.Dispose();
                    data.TrianglePositions2GraphicsBuffer.Dispose();
                    data.TriangleColors1GraphicsBuffer.Dispose();
                    data.TriangleColors2GraphicsBuffer.Dispose();

                    data.TrianglePositions1GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalTriangleElementsCount,
                        4 * 4);
                    data.TrianglePositions2GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalTriangleElementsCount,
                        4 * 4);
                    data.TriangleColors1GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalTriangleElementsCount,
                        4 * 4);
                    data.TriangleColors2GraphicsBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        debugDrawSingleton.TotalTriangleElementsCount,
                        4 * 4);
                }

                // GPU buffer update job
                if (mustUpdateGPUData)
                {
                    // Double buffering
                    GraphicsBuffer writeLinePositionsBuffer = data.LinePositions2GraphicsBuffer;
                    GraphicsBuffer writeLineColorsBuffer = data.LineColors2GraphicsBuffer;
                    GraphicsBuffer writeTrianglePositionsBuffer = data.TrianglePositions2GraphicsBuffer;
                    GraphicsBuffer writeTriangleColorsBuffer = data.TriangleColors2GraphicsBuffer;
                    switch (debugDrawSingleton.UsedBuffers)
                    {
                        case 0:
                            writeLinePositionsBuffer = data.LinePositions2GraphicsBuffer;
                            writeLineColorsBuffer = data.LineColors2GraphicsBuffer;
                            writeTrianglePositionsBuffer = data.TrianglePositions2GraphicsBuffer;
                            writeTriangleColorsBuffer = data.TriangleColors2GraphicsBuffer;
                            break;
                        case 1:
                            writeLinePositionsBuffer = data.LinePositions1GraphicsBuffer;
                            writeLineColorsBuffer = data.LineColors1GraphicsBuffer;
                            writeTrianglePositionsBuffer = data.TrianglePositions1GraphicsBuffer;
                            writeTriangleColorsBuffer = data.TriangleColors1GraphicsBuffer;
                            break;
                    }

                    JobHandle job = new UpdateBuffersJob
                    {
                        DebugDrawGroups = debugDrawSingleton.DebugDrawGroups,

                        LinePositionsBuffer = writeLinePositionsBuffer.LockBufferForWrite<float4>(0, writeLinePositionsBuffer.count),
                        LineColorsBuffer = writeLineColorsBuffer.LockBufferForWrite<float4>(0, writeLineColorsBuffer.count),

                        TrianglePositionsBuffer = writeTrianglePositionsBuffer.LockBufferForWrite<float4>(0, writeTrianglePositionsBuffer.count),
                        TriangleColorsBuffer = writeTriangleColorsBuffer.LockBufferForWrite<float4>(0, writeTriangleColorsBuffer.count),
                    }.Schedule(default);
                    job.Complete();

                    writeLinePositionsBuffer.UnlockBufferAfterWrite<float4>(writeLinePositionsBuffer.count);
                    writeLineColorsBuffer.UnlockBufferAfterWrite<float4>(writeLineColorsBuffer.count);
                    writeTrianglePositionsBuffer.UnlockBufferAfterWrite<float4>(writeTrianglePositionsBuffer.count);
                    writeTriangleColorsBuffer.UnlockBufferAfterWrite<float4>(writeTriangleColorsBuffer.count);

                    // TODO: gles
                    // if (UseConstantBuffer) 
                    // {
                    //     Shader.SetGlobalConstantBuffer(positionsID, _gpuPositions, 0, positions.Length * 4 * 4);
                    //     Shader.SetGlobalConstantBuffer(normalsID, _gpuNormals, 0, positions.Length * 4 * 4);
                    //     Shader.SetGlobalConstantBuffer(tangentsID, _gpuTangents, 0, positions.Length * 4 * 4);
                    // }
                    // else
                    {
                        Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.LinePositionsPropertyId,
                            writeLinePositionsBuffer);
                        Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.LineColorsPropertyId,
                            writeLineColorsBuffer);
                        Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.TrianglePositionsPropertyId,
                            writeTrianglePositionsBuffer);
                        Shader.SetGlobalBuffer(DebugDrawSystemManagedDataStore.TriangleColorsPropertyId,
                            writeTriangleColorsBuffer);
                    }

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
                    drawCommands->proceduralDrawCommandCount = 2;
                    drawCommands->drawRangeCount = 1;
                    drawCommands->visibleInstanceCount = 2;
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
                        (int*)UnsafeUtility.Malloc(sizeof(int) * drawCommands->visibleInstanceCount,
                            alignment, Allocator.TempJob);

                    drawCommands->visibleInstances[0] = 0;
                    drawCommands->visibleInstances[1] = 1;
                }

                // Lines
                drawCommands->proceduralDrawCommands[0] = new BatchDrawCommandProcedural
                {
                    flags = BatchDrawCommandFlags.None,
                    batchID = debugDrawSingleton.LinesBatch.BatchId,
                    materialID = debugDrawSingleton.LinesBatch.MaterialID,

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

                // Tris
                drawCommands->proceduralDrawCommands[1] = new BatchDrawCommandProcedural
                {
                    flags = BatchDrawCommandFlags.None,
                    batchID = debugDrawSingleton.TrisBatch.BatchId,
                    materialID = debugDrawSingleton.TrisBatch.MaterialID,

                    sortingPosition = 0,
                    visibleCount = 1,
                    visibleOffset = 0,
                    splitVisibilityMask = 0xff,
                    lightmapIndex = 0,

                    topology = MeshTopology.Triangles,
                    baseVertex = 0,
                    elementCount = (uint)(debugDrawSingleton.TotalTriangleElementsCount),
                    indexBufferHandle = default,
                    indexOffsetBytes = 0,
                };

                drawCommands->drawRanges[0] = new BatchDrawRange
                {
                    drawCommandsType = BatchDrawCommandType.Procedural,
                    drawCommandsBegin = 0,
                    drawCommandsCount = 2,

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
        public NativeArray<float4> LinePositionsBuffer;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> LineColorsBuffer;

        [NativeDisableParallelForRestriction]
        public NativeArray<float4> TrianglePositionsBuffer;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> TriangleColorsBuffer;

        public void Execute()
        {
            int linesWriteIndex = 0;
            int trianglesWriteIndex = 0;
            for (int i = 0; i < DebugDrawGroups.Length; i++)
            {
                DebugDrawGroup group = DebugDrawGroups[i];

                void* src = group.LinePositions.GetUnsafePtr();
                void* dst = (byte*)LinePositionsBuffer.GetUnsafePtr() + (long)(linesWriteIndex * DebugDrawUtilities.kSizeOfFloat4);
                UnsafeUtility.MemCpy(dst, src, DebugDrawUtilities.kSizeOfFloat4 * group.LinePositions.Length);

                src = group.LineColors.GetUnsafePtr();
                dst = (byte*)LineColorsBuffer.GetUnsafePtr() + (long)(linesWriteIndex * DebugDrawUtilities.kSizeOfFloat4);
                UnsafeUtility.MemCpy(dst, src, DebugDrawUtilities.kSizeOfFloat4 * group.LineColors.Length);

                linesWriteIndex += group.LinePositions.Length;

                src = group.TrianglePositions.GetUnsafePtr();
                dst = (byte*)TrianglePositionsBuffer.GetUnsafePtr() + (long)(trianglesWriteIndex * DebugDrawUtilities.kSizeOfFloat4);
                UnsafeUtility.MemCpy(dst, src, DebugDrawUtilities.kSizeOfFloat4 * group.TrianglePositions.Length);

                src = group.TriangleColors.GetUnsafePtr();
                dst = (byte*)TriangleColorsBuffer.GetUnsafePtr() + (long)(trianglesWriteIndex * DebugDrawUtilities.kSizeOfFloat4);
                UnsafeUtility.MemCpy(dst, src, DebugDrawUtilities.kSizeOfFloat4 * group.TriangleColors.Length);

                trianglesWriteIndex += group.TrianglePositions.Length;
            }
        }
    }
}