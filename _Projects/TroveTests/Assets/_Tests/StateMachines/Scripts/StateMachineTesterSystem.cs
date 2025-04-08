using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct StateMachineTester : IComponentData
{
    public Entity Prefab;
    public int SpawnCount;
    public float SpawnSpacing;
    
    public bool IsInitialized;
}

partial struct StateMachineTesterSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        
        foreach (var (tester, entity) in SystemAPI.Query<RefRW<StateMachineTester>>().WithEntityAccess())
        {
            if (!tester.ValueRW.IsInitialized)
            {
                int spawnResolution = (int)math.ceil(math.sqrt(tester.ValueRW.SpawnCount));
                for (int x = 0; x < spawnResolution; x++)
                {
                    for (int y = 0; y < spawnResolution; y++)
                    {
                        float3 spawnPosition = new float3(x, y, 0) * tester.ValueRW.SpawnSpacing;
                        Entity instance = ecb.Instantiate(tester.ValueRW.Prefab);
                        ecb.SetComponent(instance, LocalTransform.FromPosition(spawnPosition));
                    }
                }
                
                tester.ValueRW.IsInitialized = true;
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
