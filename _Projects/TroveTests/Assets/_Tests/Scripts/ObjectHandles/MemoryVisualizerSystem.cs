using Trove.ObjectHandles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

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
    public void UpdateMemoryVisualizer(ref SystemState state, ref MemoryVisualizer memViz)
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

            quaternion spawnRot = quaternion.identity;
            float spawnScale = ((memViz.XMinMax.y - memViz.XMinMax.y) / (float)bytesBuffer.Length) * 0.9f;

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
                state.EntityManager.SetComponentData(newCube, LocalTransform.FromPositionRotationScale(spawnPos, spawnRot, spawnScale));
                state.EntityManager.SetComponentData(newCube, new URPMaterialPropertyBaseColor { Value = memViz.DefaultColor });
            }
        }

        VirtualObjectManager.MemoryInfo memoryInfo = VirtualObjectManager.GetMemoryInfo(ref bytesBuffer);
        memoryInfo.MetadataFreeRangesHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> metadataFreeRangesArray);
        memoryInfo.DataFreeRangesHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeVirtualArray<IndexRangeElement> dataFreeRangesArray);

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
                int iteratedIndex = memoryInfo.MetadatasStartIndex;
                for (int i = 0; i < metadataFreeRangesArray.Length; i++)
                {
                    IndexRangeElement range = metadataFreeRangesArray[i];

                    // Used
                    if(iteratedIndex < range.StartInclusive)
                    {
                        for (int r = iteratedIndex; r < range.StartInclusive; r++)
                        {
                            float4 randomCol = random.NextFloat4(memViz.UsedMetadataColorMin, memViz.UsedMetadataColorMax);
                            state.EntityManager.SetComponentData(_spawnedCubes[r], new URPMaterialPropertyBaseColor { Value = randomCol });
                        }
                        iteratedIndex = range.StartInclusive;
                    }
                    // Unused
                    else
                    {
                        for (int r = range.StartInclusive; r < range.EndExclusive; r++)
                        {
                            state.EntityManager.SetComponentData(_spawnedCubes[r], new URPMaterialPropertyBaseColor { Value = memViz.UnusedMetadataColor });
                        }
                        iteratedIndex = range.EndExclusive;
                    }
                }
            }

            // Data
            {
                int iteratedIndex = memoryInfo.DatasStartIndex;
                for (int i = 0; i < dataFreeRangesArray.Length; i++)
                {
                    IndexRangeElement range = dataFreeRangesArray[i];

                    // Used
                    if (iteratedIndex < range.StartInclusive)
                    {
                        for (int r = iteratedIndex; r < range.StartInclusive; r++)
                        {
                            float4 randomCol = random.NextFloat4(memViz.UsedDataColorMin, memViz.UsedDataColorMax);
                            state.EntityManager.SetComponentData(_spawnedCubes[r], new URPMaterialPropertyBaseColor { Value = randomCol });
                        }
                        iteratedIndex = range.StartInclusive;
                    }
                    // Unused
                    else
                    {
                        for (int r = range.StartInclusive; r < range.EndExclusive; r++)
                        {
                            state.EntityManager.SetComponentData(_spawnedCubes[r], new URPMaterialPropertyBaseColor { Value = memViz.UnusedDataColor });
                        }
                        iteratedIndex = range.EndExclusive;
                    }
                }

                // Free ranges
                {
                    for (int i = memoryInfo.MetadataFreeRangesStartIndex; i < memoryInfo.MetadataFreeRangesStartIndex + metadataFreeRangesArray.Length; i++)
                    {
                        state.EntityManager.SetComponentData(_spawnedCubes[i], new URPMaterialPropertyBaseColor { Value = memViz.MetadataFreeRangeColor });
                    }
                    for (int i = memoryInfo.DataFreeRangesStartIndex; i < memoryInfo.DataFreeRangesStartIndex + dataFreeRangesArray.Length; i++)
                    {
                        state.EntityManager.SetComponentData(_spawnedCubes[i], new URPMaterialPropertyBaseColor { Value = memViz.DataFreeRangeColor });
                    }
                }
            }
        }
    }
}
