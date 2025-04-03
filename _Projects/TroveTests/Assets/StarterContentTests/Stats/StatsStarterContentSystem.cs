using Trove.Stats;
using Unity.Burst;
using Unity.Entities;

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

        _statsAccessor = new StatsAccessor<SampleStatModifier, SampleStatModifier.Stack>(ref state, true, true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StatsWorldSingleton statsWorldSingleton = SystemAPI.GetSingleton<StatsWorldSingleton>();
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
        public StatsWorldData<SampleStatModifier.Stack> StatsWorldData;
        
        public void Execute(Entity entity, in SampleStats stats, ref SampleStatValues statValues)
        {
            StatsAccessor.TryGetStat(stats.Strength, out statValues.Strength, out _);
            StatsAccessor.TryGetStat(stats.Intelligence, out statValues.Intelligence, out _);
            StatsAccessor.TryGetStat(stats.Dexterity, out statValues.Dexterity, out _);

            if (StatsAccessor.TryCalculateModifiersCount(stats.Intelligence, out int intelligenceModifiersCount))
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
        }
    }
}
