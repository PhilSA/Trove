using Trove.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

public struct StatsWorldSingleton : IComponentData
{
    public StatsWorldData<SampleStatModifier.Stack> StatsWorldData;
}

public partial struct SampleStatsWorldSystem : ISystem
{
    private StatsWorldData<SampleStatModifier.Stack> _statsWorldData;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _statsWorldData = new StatsWorldData<SampleStatModifier.Stack>(Allocator.Persistent);
        
        // Create the singleton
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new StatsWorldSingleton
        {
            StatsWorldData = _statsWorldData,
        });
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _statsWorldData.Dispose();
    }
}
