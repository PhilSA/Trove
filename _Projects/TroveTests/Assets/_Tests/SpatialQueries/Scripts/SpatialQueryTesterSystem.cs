using Trove.SpatialQueries;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using AABB = Trove.AABB;

public struct SpatialQueryTester : IComponentData
{
    public Entity BVHCubePrefab;

    public int SpawnCount;
    public AABB SpawnArea;

    public bool IsInitialized;
}

partial struct SpatialQueryTesterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (tester, entity) in SystemAPI.Query<RefRW<SpatialQueryTester>>().WithEntityAccess())
        {
            if (!tester.ValueRW.IsInitialized)
            {
                Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);
                for (int i = 0; i < tester.ValueRW.SpawnCount; i++)
                {
                    Entity newInstance = ecb.Instantiate(tester.ValueRW.BVHCubePrefab);
                    ecb.SetComponent(newInstance, LocalTransform.FromPositionRotation(
                        random.NextFloat3(tester.ValueRW.SpawnArea.Min, tester.ValueRW.SpawnArea.Max),
                        random.NextQuaternionRotation()));
                }
                
                tester.ValueRW.IsInitialized = true;
            }
        }
                
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
