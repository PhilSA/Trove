using Trove.Stats;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

public struct SampleStatValues : IComponentData
{
    public float Strength;
    public float Intelligence;
    public float Dexterity;
}

partial struct StatsStarterContentSystem : ISystem
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
        
        state.Dependency = new StatsStarterContentJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            StatsWorldData = statsWorldSingleton.StatsWorldData,
            StatsAccessor = _statsAccessor,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct StatsStarterContentJob : IJobEntity
    {
        public float DeltaTime;
        public StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> StatsAccessor;
        public StatsWorldData<SampleStatModifier, SampleStatModifier.Stack> StatsWorldData;
        
        public void Execute(Entity entity, in SampleStats stats, ref SampleStatValues statValues)
        {
            StatsAccessor.TryGetStat(stats.Strength, out statValues.Strength, out _);
            StatsAccessor.TryGetStat(stats.Intelligence, out statValues.Intelligence, out _);
            StatsAccessor.TryGetStat(stats.Dexterity, out statValues.Dexterity, out _);

            StatsAccessor.TrySetStatProduceChangeEvents(stats.Intelligence, true);

            if (StatsAccessor.TryCalculateStatModifiersCount(stats.Intelligence, out int intelligenceModifiersCount))
            {
                if (intelligenceModifiersCount <= 0)
                {
                    StatsAccessor.TryAddStatModifier(stats.Intelligence,
                        new SampleStatModifier
                        {
                            ModifierType = SampleStatModifier.Type.AddFromStat,
                            StatHandleA = stats.Dexterity,
                        },
                        out StatModifierHandle modifierHandle,
                        ref StatsWorldData);
                }
            }
            
            StatsAccessor.TryAddStatBaseValue(stats.Dexterity, DeltaTime, ref StatsWorldData);
            
            UnityEngine.Debug.Log($"Detected {StatsWorldData.StatChangeEventsList.Length} stat change events");
            StatsWorldData.StatChangeEventsList.Clear();
        }
    }
}
