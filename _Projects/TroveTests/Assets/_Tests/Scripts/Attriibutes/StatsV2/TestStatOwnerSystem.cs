using System;
using Trove.Stats;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

public struct StatsSingleton : IComponentData
{
    public StatsWriter<,> StatsWriter;
}

/// <summary>
/// Creates stats singleton and processes stat change events
/// </summary>
partial struct TestStatSystem : ISystem
{
    private StatsWriter<,> _statsWriter;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsTester>();
        state.RequireForUpdate<StatsSingleton>();
        
        // Create stats world
        _statsWriter = new StatsWriter<,>(
            10000,
            10,
            10,
            1.5f);
        
        // Create singleton
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new StatsSingleton
        {
            StatsWriter = _statsWriter,
        });
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _statsWriter.Dispose(state.Dependency);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StatsWriter<,> statsWriter = 
            SystemAPI.GetSingletonRW<StatsSingleton>().ValueRW.StatsWriter;
        StatsTester statsTester = SystemAPI.GetSingletonRW<StatsTester>().ValueRO;

        state.Dependency = new StatChangeEventsJob
        {
            SupportWriteback = statsTester.SupportStatsWriteback,
            StatsWriter = statsWriter,
            TestStatOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false),
        }.Schedule(state.Dependency);
    }
    
    [BurstCompile]
    public struct StatChangeEventsJob : IJob
    {
        public bool SupportWriteback;
        public StatsWriter<,> StatsWriter;
        public ComponentLookup<TestStatOwner> TestStatOwnerLookup;
        
        public void Execute()
        {
            // Stat change events
            for (int i = 0; i < StatsWriter.StatChangeEvents.Length; i++)
            {
                StatChangeEvent changeEvent = StatsWriter.StatChangeEvents[i];
                TestStatCustomData testStatCustomData = StatsWriter.GetStatCustomData(changeEvent.StatIndex);

                if (TestStatOwnerLookup.TryGetComponent(testStatCustomData.Entity, out TestStatOwner testStatOwner))
                {
                    switch (testStatCustomData.StatType)
                    {
                        case StatType.A:
                            testStatOwner.StatA.Value = changeEvent.NewValue.Value;
                            break;
                        case StatType.B:
                            testStatOwner.StatB.Value = changeEvent.NewValue.Value;
                            break;
                        case StatType.C:
                            testStatOwner.StatC.Value = changeEvent.NewValue.Value;
                            break;
                    }

                    TestStatOwnerLookup[testStatCustomData.Entity] = testStatOwner;
                }
            }

            // Clear events
            StatsWriter.StatChangeEvents.Clear();
        }
    }
}