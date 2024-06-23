using Trove.ObjectHandles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Logging;
using Unity.Assertions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.UniversalDelegates;
using Trove;

partial struct MemoryVisualizerSystem : ISystem
{
    private Entity _testEntity;
    private NativeList<Entity> _spawnedCubes;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MemoryVisualizer>();

        _spawnedCubes = new NativeList<Entity>(Allocator.Persistent);

        _testEntity = state.EntityManager.CreateEntity();
        DynamicBuffer<byte> bytesBuffer = state.EntityManager.AddBuffer<TestVirtualObjectElement>(_testEntity).Reinterpret<byte>();

        VirtualObjectManager.Initialize(ref bytesBuffer, 16, 64);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if(_spawnedCubes.IsCreated)
        {
            _spawnedCubes.Dispose();
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref MemoryVisualizer memViz = ref SystemAPI.GetSingletonRW<MemoryVisualizer>().ValueRW;
        if(memViz.Update)
        {
            UpdateMemoryVisualizer(ref state, ref memViz);
            memViz.Update = false;
        }
    }

    [BurstCompile]
    public unsafe void UpdateMemoryVisualizer(ref SystemState state, ref MemoryVisualizer memViz)
    {
        DynamicBuffer<byte> bytesBuffer = state.EntityManager.GetBuffer<TestVirtualObjectElement>(_testEntity).Reinterpret<byte>();

        // Check for spawning/despawning cubes
        if(bytesBuffer.Length != _spawnedCubes.Length)
        {
            // Clear
            for (int i = 0; i < _spawnedCubes.Length; i++)
            {
                state.EntityManager.DestroyEntity(_spawnedCubes[i]);
            }
            _spawnedCubes.Clear();

            quaternion spawnRot = quaternion.identity;
            float3 spawnScale = new float3
            {
                x = ((memViz.XMinMax.y - memViz.XMinMax.x) / (float)bytesBuffer.Length),
                y = 1f,
                z = 1f,
            };

            // Spawn new
            for (int i = 0; i < bytesBuffer.Length; i++)
            {
                float3 spawnPos = new float3
                {
                    x = math.lerp(memViz.XMinMax.x, memViz.XMinMax.y, (float)i / (float)bytesBuffer.Length),
                    y = 0f,
                    z = 0f,
                };

                Entity newCube = state.EntityManager.Instantiate(memViz.ColorCubePrefab);
                state.EntityManager.SetComponentData(newCube, LocalTransform.FromPositionRotationScale(spawnPos, spawnRot, 1f));
                state.EntityManager.SetComponentData(newCube, new PostTransformMatrix { Value = float4x4.Scale(spawnScale) });
                state.EntityManager.SetComponentData(newCube, new URPMaterialPropertyBaseColor { Value = memViz.DefaultColor });

                _spawnedCubes.Add(newCube);
            }
        }

        VirtualObjectManager.MemoryInfo memoryInfo = VirtualObjectManager.GetMemoryInfo(ref bytesBuffer);
        memoryInfo.MetadataFreeRangesHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> metadataFreeRangesArray);
        memoryInfo.DataFreeRangesHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> dataFreeRangesArray);

        byte* bufferPtr = (byte*)bytesBuffer.GetUnsafePtr();

        // Set cube colors
        {
            Random random = Random.CreateFromIndex(0);

            // Default
            for (int i = 0; i < _spawnedCubes.Length; i++)
            {
                state.EntityManager.SetComponentData(_spawnedCubes[i], new URPMaterialPropertyBaseColor { Value = memViz.DefaultColor });
            }

            // Static data
            for (int i = 0; i < memoryInfo.MetadatasStartIndex; i++)
            {
                state.EntityManager.SetComponentData(_spawnedCubes[i], new URPMaterialPropertyBaseColor { Value = memViz.StaticDataColor });
            }

            // Metadata
            {
                int objectSize = UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                int iteratedIndex = memoryInfo.MetadatasStartIndex;
                for (int i = 0; i < metadataFreeRangesArray.Length; i++)
                {
                    IndexRangeElement range = metadataFreeRangesArray[i];

                    // Used
                    if (iteratedIndex < range.StartInclusive)
                    {
                        while(iteratedIndex < range.StartInclusive)
                        {
                            float4 randomCol = random.NextFloat4(memViz.UsedMetadataColorMin, memViz.UsedMetadataColorMax);
                            for (int s = 0; s < objectSize; s++)
                            {
                                state.EntityManager.SetComponentData(_spawnedCubes[iteratedIndex], new URPMaterialPropertyBaseColor { Value = randomCol });
                                iteratedIndex++;
                            }
                        }
                        Assert.AreEqual(range.StartInclusive, iteratedIndex);
                    }

                    // Unused
                    while (iteratedIndex < range.EndExclusive)
                    {
                        state.EntityManager.SetComponentData(_spawnedCubes[iteratedIndex], new URPMaterialPropertyBaseColor { Value = memViz.UnusedMetadataColor });
                        iteratedIndex++;
                    }
                    Assert.AreEqual(range.EndExclusive, iteratedIndex);
                }

                // Rest of Unused
                for (int r = iteratedIndex; r < memoryInfo.DatasStartIndex; r++)
                {
                    state.EntityManager.SetComponentData(_spawnedCubes[r], new URPMaterialPropertyBaseColor { Value = memViz.UnusedMetadataColor });
                }
            }

            // Data
            {
                // Unused
                for (int i = memoryInfo.DatasStartIndex; i < bytesBuffer.Length; i++)
                {
                    state.EntityManager.SetComponentData(_spawnedCubes[i], new URPMaterialPropertyBaseColor { Value = memViz.UnusedDataColor });
                }

                // Used (by iterating metadatas)
                int metadataSize = UnsafeUtility.SizeOf<VirtualObjectMetadata>();
                int iteratedMetadataIndex = memoryInfo.MetadatasStartIndex;
                while(iteratedMetadataIndex < memoryInfo.DatasStartIndex)
                {
                    ByteArrayUtilities.ReadValue(bufferPtr, iteratedMetadataIndex, out VirtualObjectMetadata metadata);

                    float4 randomCol = random.NextFloat4(memViz.UsedDataColorMin, memViz.UsedDataColorMax);
                    for (int s = metadata.ByteIndex; s < metadata.ByteIndex + metadata.Size; s++)
                    {
                        state.EntityManager.SetComponentData(_spawnedCubes[s], new URPMaterialPropertyBaseColor { Value = randomCol });
                    }
                    iteratedMetadataIndex += metadataSize;
                }

                // Free ranges
                {
                    for (int i = memoryInfo.MetadataFreeRangesStartIndex; i < memoryInfo.MetadataFreeRangesStartIndex + memoryInfo.MetadataFreeRangesSize; i++)
                    {
                        state.EntityManager.SetComponentData(_spawnedCubes[i], new URPMaterialPropertyBaseColor { Value = memViz.MetadataFreeRangeColor });
                    }
                    for (int i = memoryInfo.DataFreeRangesStartIndex; i < memoryInfo.DataFreeRangesStartIndex + memoryInfo.DataFreeRangesSize; i++)
                    {
                        state.EntityManager.SetComponentData(_spawnedCubes[i], new URPMaterialPropertyBaseColor { Value = memViz.DataFreeRangeColor });
                    }
                }
            }
        }
    }
}
