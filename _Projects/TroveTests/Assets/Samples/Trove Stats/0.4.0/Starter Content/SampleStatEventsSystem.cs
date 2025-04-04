using Trove.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

/// <summary>
/// Shows how to process and clear stat events
/// </summary>
partial struct SampleStatEventsSystem : ISystem
{
    private StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> _statsAccessor;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsWorldSingleton>();
        
        _statsAccessor = new StatsAccessor<SampleStatModifier, SampleStatModifier.Stack>(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StatsWorldSingleton statsWorldSingleton = SystemAPI.GetSingletonRW<StatsWorldSingleton>().ValueRW;
        _statsAccessor.Update(ref state);

        state.Dependency = new StatsEventsJob
        {
            StatsWorldData = statsWorldSingleton.StatsWorldData,
            StatsAccessor = _statsAccessor,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct StatsEventsJob : IJob
    {
        public StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> StatsAccessor;
        public StatsWorldData<SampleStatModifier, SampleStatModifier.Stack> StatsWorldData;
        
        public void Execute()
        {
            // Stat change events
            for (int i = 0; i < StatsWorldData.StatChangeEventsList.Length; i++)
            {
                // TODO
            }
            StatsWorldData.StatChangeEventsList.Clear();
            
            // Modifier trigger events
            for (int i = 0; i < StatsWorldData.ModifierTriggerEventsList.Length; i++)
            {
                // TODO
            }
            StatsWorldData.ModifierTriggerEventsList.Clear();
        }
    }
}
