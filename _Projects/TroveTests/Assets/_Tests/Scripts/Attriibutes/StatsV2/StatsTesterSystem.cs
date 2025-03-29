
using Trove.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

public struct UpdatingStat : IComponentData
{ }

partial struct StatsTesterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsTester>();
        state.RequireForUpdate<StatsSingleton>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref StatsTester tester = ref SystemAPI.GetSingletonRW<StatsTester>().ValueRW;
        StatsWorld<,> statsWorld = 
            SystemAPI.GetSingletonRW<StatsSingleton>().ValueRO.StatsWorld;
        
        ComponentLookup<TestStatOwner> statsOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false);
        
        if (!tester.HasInitialized)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            for (int i = 0; i < tester.UnchangingAttributesCount; i++)
            {
                Entity newStatOwnerEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);
                statsOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false);
                InitStatOwner(newStatOwnerEntity, ref statsOwnerLookup, ref statsWorld, tester.SupportStatsWriteback);
            }

            for (int i = 0; i < tester.ChangingAttributesCount; i++)
            {
                Entity observedEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);
                state.EntityManager.AddComponentData(observedEntity, new UpdatingStat());
                statsOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false);
                InitStatOwner(observedEntity, ref statsOwnerLookup, ref statsWorld, tester.SupportStatsWriteback);
                TestStatOwner observedStatOwner = statsOwnerLookup[observedEntity];

                if(tester.MakeOtherStatsDependOnFirstStatOfChangingAttributes)
                {
                    statsWorld.AddStatModifier(
                        observedStatOwner.StatB.Index,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatAIndex = observedStatOwner.StatA.Index,
                            ValueA = 0f,
                        },
                        out StatModifierHandle modifierHandle);
                    
                    statsWorld.AddStatModifier(
                        observedStatOwner.StatC.Index,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatAIndex = observedStatOwner.StatB.Index,
                            ValueA = 0f,
                        },
                        out modifierHandle);
                }

                for (int j = 0; j < tester.ChangingAttributesChildDepth; j++)
                {
                    Entity newObserverEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);
                    statsOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false);
                    InitStatOwner(newObserverEntity, ref statsOwnerLookup, ref statsWorld, tester.SupportStatsWriteback);
                    TestStatOwner newObserverStatOwner = statsOwnerLookup[newObserverEntity];
                        
                    statsWorld.AddStatModifier(
                        newObserverStatOwner.StatA.Index,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatAIndex = observedStatOwner.StatA.Index,
                            ValueA = 0f,
                        },
                        out StatModifierHandle modifierHandle);

                    observedStatOwner = newObserverStatOwner;
                }
            }

            tester.HasInitialized = true;
        }

        state.Dependency = new UpdatingStatsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            StatsWorld = statsWorld,
        }.Schedule(state.Dependency);

        state.Dependency = new StatGetValueJob()
        {
            StatsWorld = statsWorld,
        }.ScheduleParallel(state.Dependency);
    }

    private static void InitStatOwner(Entity entity, ref ComponentLookup<TestStatOwner> statsOwnerLookup,
        ref StatsWorld<,> statsWorld, bool supportStatWriteback)
    {
        TestStatOwner statOwner = statsOwnerLookup[entity];
        statsWorld.CreateStat(statOwner.StatA.Value, supportStatWriteback, new TestStatCustomData(entity, StatType.A), out statOwner.StatA.Index);
        statsWorld.CreateStat(statOwner.StatB.Value, supportStatWriteback, new TestStatCustomData(entity, StatType.B), out statOwner.StatB.Index);
        statsWorld.CreateStat(statOwner.StatC.Value, supportStatWriteback, new TestStatCustomData(entity, StatType.C), out statOwner.StatC.Index);
        statsOwnerLookup[entity] = statOwner;
    }

    [BurstCompile]
    [WithAll(typeof(UpdatingStat))]
    public partial struct UpdatingStatsJob : IJobEntity
    {
        public float DeltaTime;
        public StatsWorld<,> StatsWorld;

        void Execute(ref TestStatOwner statsOwner)
        {
            StatsWorld.AddStatBaseValue(statsOwner.StatA.Index, DeltaTime);
        }
    }

    [BurstCompile]
    public partial struct StatGetValueJob : IJobEntity
    {
        [ReadOnly]
        public StatsWorld<,> StatsWorld;

        void Execute(ref TestStatOwner statsOwner)
        {
            statsOwner.StatA.Value = StatsWorld.TryGetStat(statsOwner.StatA.Index).Value;
        }
    }
}