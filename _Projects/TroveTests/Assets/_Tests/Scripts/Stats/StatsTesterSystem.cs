
using Trove.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

public struct UpdatingStat : IComponentData
{ }

partial struct StatsTesterSystem : ISystem
{
    private StatsWorld<TestStatModifier, TestStatModifier.Stack> _statsWorld;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsTester>();
        
        _statsWorld = new StatsWorld<TestStatModifier, TestStatModifier.Stack>(ref state);
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref StatsTester tester = ref SystemAPI.GetSingletonRW<StatsTester>().ValueRW;
        
        ComponentLookup<TestStatOwner> statsOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false);
        
        if (!tester.HasInitialized)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            for (int i = 0; i < tester.UnchangingAttributesCount; i++)
            {
                Entity newStatOwnerEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);
            }

            for (int i = 0; i < tester.ChangingAttributesCount; i++)
            {
                Entity observedEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);
                state.EntityManager.AddComponentData(observedEntity, new UpdatingStat());
                statsOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false);
                TestStatOwner observedStatOwner = statsOwnerLookup[observedEntity];

                if(tester.MakeLocalStatsDependOnEachOther)
                {
                    _statsWorld.UpdateDataAndLookups(ref state);
                    
                    _statsWorld.TryAddStatModifier(
                        observedStatOwner.StatB,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatHandleA = observedStatOwner.StatA,
                            ValueA = 0f,
                        },
                        out StatModifierHandle modifierHandle);
                    
                    _statsWorld.TryAddStatModifier(
                        observedStatOwner.StatC,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatHandleA = observedStatOwner.StatB,
                            ValueA = 0f,
                        },
                        out modifierHandle);
                }

                for (int j = 0; j < tester.ChangingAttributesChildDepth; j++)
                {
                    Entity newObserverEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);
                    statsOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false);
                    TestStatOwner newObserverStatOwner = statsOwnerLookup[newObserverEntity];
                    
                    _statsWorld.UpdateDataAndLookups(ref state);
                    
                    _statsWorld.TryAddStatModifier(
                        newObserverStatOwner.StatA,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatHandleA = observedStatOwner.StatA,
                            ValueA = 0f,
                        },
                        out StatModifierHandle modifierHandle);

                    observedStatOwner = newObserverStatOwner;
                }
            }

            tester.HasInitialized = true;
        }
        
        _statsWorld.UpdateDataAndLookups(ref state);

        state.Dependency = new UpdatingStatsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            StatsWorld = _statsWorld,
        }.Schedule(state.Dependency);

        state.Dependency = new StatGetValueJob()
        {
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(UpdatingStat))]
    public partial struct UpdatingStatsJob : IJobEntity
    {
        public float DeltaTime;
        public StatsWorld<TestStatModifier, TestStatModifier.Stack> StatsWorld;

        void Execute(ref TestStatOwner statsOwner)
        {
            StatsWorld.TryAddStatBaseValue(statsOwner.StatA, DeltaTime);
        }
    }

    [BurstCompile]
    public partial struct StatGetValueJob : IJobEntity
    {
        void Execute(Entity entity, ref TestStatOwner test, in DynamicBuffer<Stat> statsBuffer)
        {
            if (statsBuffer.Length > 0)
            {
                test.tmp = statsBuffer[0].Value;
            }
        }
    }
}