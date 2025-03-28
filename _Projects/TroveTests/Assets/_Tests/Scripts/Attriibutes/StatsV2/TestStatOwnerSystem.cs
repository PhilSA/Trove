using System;
using Trove.Stats;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

public struct StatsSingleton : IComponentData
{
    public StatsWorld<TestStatModifier, TestStatModifier.Stack, TestStatCustomData> StatsWorld;
}

/// <summary>
/// Creates stats singleton and processes stat change events
/// </summary>
partial struct TestStatSystem : ISystem
{
    private StatsWorld<TestStatModifier, TestStatModifier.Stack, TestStatCustomData> _statsWorld;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsTester>();
        state.RequireForUpdate<StatsSingleton>();
        
        // Create stats world
        _statsWorld = new StatsWorld<TestStatModifier, TestStatModifier.Stack, TestStatCustomData>(
            10000,
            10,
            10,
            1.5f);
        
        // Create singleton
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new StatsSingleton
        {
            StatsWorld = _statsWorld,
        });
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _statsWorld.Dispose(state.Dependency);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StatsWorld<TestStatModifier, TestStatModifier.Stack, TestStatCustomData> statsWorld = 
            SystemAPI.GetSingletonRW<StatsSingleton>().ValueRW.StatsWorld;
        StatsTester statsTester = SystemAPI.GetSingletonRW<StatsTester>().ValueRO;

        state.Dependency = new StatChangeEventsJob
        {
            SupportWriteback = statsTester.SupportStatsWriteback,
            StatsWorld = statsWorld,
            TestStatOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false),
        }.Schedule(state.Dependency);
    }
    
    [BurstCompile]
    public struct StatChangeEventsJob : IJob
    {
        public bool SupportWriteback;
        public StatsWorld<TestStatModifier, TestStatModifier.Stack, TestStatCustomData> StatsWorld;
        public ComponentLookup<TestStatOwner> TestStatOwnerLookup;
        
        public void Execute()
        {
            // Stat change events
            for (int i = 0; i < StatsWorld.StatChangeEvents.Length; i++)
            {
                StatChangeEvent changeEvent = StatsWorld.StatChangeEvents[i];
                TestStatCustomData testStatCustomData = StatsWorld.GetStatCustomData(changeEvent.StatIndex);

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
            StatsWorld.StatChangeEvents.Clear();
        }
    }
}