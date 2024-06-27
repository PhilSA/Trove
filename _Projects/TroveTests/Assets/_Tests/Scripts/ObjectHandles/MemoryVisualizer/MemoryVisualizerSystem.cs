//using Trove.ObjectHandles;
//using Unity.Burst;
//using Unity.Collections;
//using Unity.Entities;
//using Unity.Mathematics;
//using Unity.Rendering;
//using Unity.Transforms;
//using Unity.Logging;
//using Unity.Assertions;
//using Unity.Collections.LowLevel.Unsafe;
//using Unity.Entities.UniversalDelegates;
//using Trove;

//partial struct MemoryVisualizerSystem : ISystem
//{
//    public struct TestEntity : IBufferElementData
//    {
//        public Entity Value;
//    }

//    [BurstCompile]
//    public void OnCreate(ref SystemState state)
//    {
//        state.RequireForUpdate<MemoryVisualizer>();
//    }

//    [BurstCompile]
//    public void OnUpdate(ref SystemState state)
//    {
//        ref MemoryVisualizer memViz = ref SystemAPI.GetSingletonRW<MemoryVisualizer>().ValueRW;

//        // Handle init
//        if (memViz.TestEntity == Entity.Null)
//        {
//            memViz.TestEntity = state.EntityManager.CreateEntity();
//            state.EntityManager.AddBuffer<TestEntity>(memViz.TestEntity);
//            DynamicBuffer<TestVirtualObjectElement> bytesBuffer = state.EntityManager.AddBuffer<TestVirtualObjectElement>(memViz.TestEntity);
//        }

//        if (memViz.Update && memViz.TestEntity != Entity.Null)
//        {
//            UpdateMemoryVisualizer(ref state, ref memViz);
//            memViz.Update = false;
//        }
//    }

//    [BurstCompile]
//    public unsafe void UpdateMemoryVisualizer(ref SystemState state, ref MemoryVisualizer memViz)
//    {
//        DynamicBuffer<TestVirtualObjectElement> bytesBuffer = state.EntityManager.GetBuffer<TestVirtualObjectElement>(memViz.TestEntity);
//        DynamicBuffer<Entity> testEntitiesBuffer = state.EntityManager.GetBuffer<TestEntity>(memViz.TestEntity).Reinterpret<Entity>();

//        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

//        // Check for spawning/despawning cubes
//        if (bytesBuffer.Length != testEntitiesBuffer.Length)
//        {
//            // Clear
//            for (int i = 0; i < testEntitiesBuffer.Length; i++)
//            {
//                ecb.DestroyEntity(testEntitiesBuffer[i]);
//            }
//            ecb.SetBuffer<TestEntity>(memViz.TestEntity);

//            quaternion spawnRot = quaternion.identity;
//            float3 spawnScale = new float3
//            {
//                x = ((memViz.XMinMax.y - memViz.XMinMax.x) / (float)bytesBuffer.Length),
//                y = 1f,
//                z = 1f,
//            };

//            // Spawn new
//            for (int i = 0; i < bytesBuffer.Length; i++)
//            {
//                float3 spawnPos = new float3
//                {
//                    x = math.lerp(memViz.XMinMax.x, memViz.XMinMax.y, (float)i / (float)bytesBuffer.Length),
//                    y = 0f,
//                    z = 0f,
//                };

//                Entity newCube = ecb.Instantiate(memViz.ColorCubePrefab);
//                ecb.SetComponent(newCube, LocalTransform.FromPositionRotationScale(spawnPos, spawnRot, 1f));
//                ecb.SetComponent(newCube, new PostTransformMatrix { Value = float4x4.Scale(spawnScale) });
//                ecb.SetComponent(newCube, new URPMaterialPropertyBaseColor { Value = memViz.DefaultColor });

//                ecb.AppendToBuffer<TestEntity>(memViz.TestEntity, new TestEntity { Value = newCube });
//            }
//        }

//        ecb.Playback(state.EntityManager);
//        ecb.Dispose();

//        bytesBuffer = state.EntityManager.GetBuffer<TestVirtualObjectElement>(memViz.TestEntity);
//        testEntitiesBuffer = state.EntityManager.GetBuffer<TestEntity>(memViz.TestEntity).Reinterpret<Entity>();
//        byte* bufferPtr = (byte*)bytesBuffer.GetUnsafePtr();

//        VirtualObjectManager.MemoryInfo memoryInfo = VirtualObjectManager.GetMemoryInfo(ref bytesBuffer);
//        memoryInfo.MetadataFreeRangesHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeArrayView<IndexRangeElement> metadataFreeRangesArray);
//        memoryInfo.DataFreeRangesHandle.TryAsUnsafeVirtualArray(ref bytesBuffer, out UnsafeArrayView<IndexRangeElement> dataFreeRangesArray);

//        // Set cube colors
//        {
//            // Default
//            for (int i = 0; i < testEntitiesBuffer.Length; i++)
//            {
//                state.EntityManager.SetComponentData(testEntitiesBuffer[i], new URPMaterialPropertyBaseColor { Value = memViz.DefaultColor });
//            }

//            // Static data
//            for (int i = 0; i < memoryInfo.MetadatasStartIndex; i++)
//            {
//                state.EntityManager.SetComponentData(testEntitiesBuffer[i], new URPMaterialPropertyBaseColor { Value = memViz.StaticDataColor });
//            }

//            // Metadata
//            {
//                int objectSize = UnsafeUtility.SizeOf<VirtualObjectMetadata>();
//                int iteratedIndex = memoryInfo.MetadatasStartIndex;
//                for (int i = 0; i < metadataFreeRangesArray.Length; i++)
//                {
//                    IndexRangeElement range = metadataFreeRangesArray[i];

//                    // Used
//                    if (iteratedIndex < range.StartInclusive)
//                    {
//                        while (iteratedIndex < range.StartInclusive)
//                        {
//                            float4 randomCol = GetRandomFloat4ForIndex(iteratedIndex, memViz.UsedMetadataColor);
//                            for (int s = 0; s < objectSize; s++)
//                            {
//                                state.EntityManager.SetComponentData(testEntitiesBuffer[iteratedIndex], new URPMaterialPropertyBaseColor { Value = randomCol });
//                                iteratedIndex++;
//                            }
//                        }
//                        Assert.AreEqual(range.StartInclusive, iteratedIndex);
//                    }

//                    // Unused
//                    while (iteratedIndex < range.EndExclusive)
//                    {
//                        state.EntityManager.SetComponentData(testEntitiesBuffer[iteratedIndex], new URPMaterialPropertyBaseColor { Value = memViz.UnusedMetadataColor });
//                        iteratedIndex++;
//                    }
//                    Assert.AreEqual(range.EndExclusive, iteratedIndex);
//                }

//                // Rest of Used (after the last free range)
//                while (iteratedIndex < memoryInfo.DatasStartIndex)
//                {
//                    float4 randomCol = GetRandomFloat4ForIndex(iteratedIndex, memViz.UsedMetadataColor);
//                    for (int s = 0; s < objectSize; s++)
//                    {
//                        state.EntityManager.SetComponentData(testEntitiesBuffer[iteratedIndex], new URPMaterialPropertyBaseColor { Value = randomCol });
//                        iteratedIndex++;
//                    }
//                }
//                Assert.AreEqual(memoryInfo.DatasStartIndex, iteratedIndex);
//            }

//            // Data
//            {
//                // Unused
//                for (int i = memoryInfo.DatasStartIndex; i < bytesBuffer.Length; i++)
//                {
//                    state.EntityManager.SetComponentData(testEntitiesBuffer[i], new URPMaterialPropertyBaseColor { Value = memViz.UnusedDataColor });
//                }

//                // Used (by iterating metadatas)
//                int metadataSize = UnsafeUtility.SizeOf<VirtualObjectMetadata>();
//                int iteratedMetadataIndex = memoryInfo.MetadatasStartIndex;
//                while (iteratedMetadataIndex < memoryInfo.DatasStartIndex)
//                {
//                    ByteArrayUtilities.ReadValue(bufferPtr, iteratedMetadataIndex, out VirtualObjectMetadata metadata);
//                    if (metadata.ByteIndex > 0)
//                    {
//                        float4 randomCol = GetRandomFloat4ForIndex(iteratedMetadataIndex, memViz.UsedDataColor);
//                        for (int s = metadata.ByteIndex; s < metadata.ByteIndex + metadata.Size; s++)
//                        {
//                            state.EntityManager.SetComponentData(testEntitiesBuffer[s], new URPMaterialPropertyBaseColor { Value = randomCol });
//                        }
//                    }
//                    iteratedMetadataIndex += metadataSize;
//                }

//                // Free ranges
//                {
//                    for (int i = memoryInfo.MetadataFreeRangesStartIndex; i < memoryInfo.MetadataFreeRangesStartIndex + memoryInfo.MetadataFreeRangesSize; i++)
//                    {
//                        state.EntityManager.SetComponentData(testEntitiesBuffer[i], new URPMaterialPropertyBaseColor { Value = memViz.MetadataFreeRangeColor });
//                    }
//                    for (int i = memoryInfo.DataFreeRangesStartIndex; i < memoryInfo.DataFreeRangesStartIndex + memoryInfo.DataFreeRangesSize; i++)
//                    {
//                        state.EntityManager.SetComponentData(testEntitiesBuffer[i], new URPMaterialPropertyBaseColor { Value = memViz.DataFreeRangeColor });
//                    }
//                }
//            }
//        }
//    }

//    private float4 GetRandomFloat4ForIndex(int index, float4 col)
//    {
//        Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)index);
//        return random.NextFloat(0.1f, 1f) * col;
//    }
//}
