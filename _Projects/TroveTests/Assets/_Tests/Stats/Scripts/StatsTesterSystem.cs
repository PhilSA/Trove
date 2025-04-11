
using Trove;
using Trove.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

public struct UpdatingStat : IComponentData
{ }

partial struct StatsTesterSystem : ISystem
{
    private StatsAccessor<TestStatModifier, TestStatModifier.Stack> _statsAccessor;
    private StatsWorldData<TestStatModifier, TestStatModifier.Stack> _statsWorldData;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsTester>();
        
        _statsAccessor = new StatsAccessor<TestStatModifier, TestStatModifier.Stack>(ref state);
        _statsWorldData = new StatsWorldData<TestStatModifier, TestStatModifier.Stack>(Allocator.Persistent);
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _statsWorldData.Dispose();
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
 
                _statsAccessor.Update(ref state);
                
                DynamicBuffer<TestStatModifier> modifiersBuffer = 
                    state.EntityManager.GetBuffer<TestStatModifier>(observedEntity);
                AddSimpleModifiers(ref tester, observedStatOwner.StatA, ref modifiersBuffer);

                if(tester.MakeLocalStatsDependOnEachOther)
                {
                    _statsAccessor.Update(ref state);
                    
                    _statsAccessor.TryAddStatModifier(
                        observedStatOwner.StatB,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatHandleA = observedStatOwner.StatA,
                            ValueA = 0f,
                        },
                        out StatModifierHandle modifierHandle, 
                        ref _statsWorldData);
                    
                    _statsAccessor.TryAddStatModifier(
                        observedStatOwner.StatC,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatHandleA = observedStatOwner.StatB,
                            ValueA = 0f,
                        },
                        out modifierHandle, 
                        ref _statsWorldData);
                }

                for (int j = 0; j < tester.ChangingAttributesChildDepth; j++)
                {
                    Entity newObserverEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);
                    statsOwnerLookup = SystemAPI.GetComponentLookup<TestStatOwner>(false);
                    TestStatOwner newObserverStatOwner = statsOwnerLookup[newObserverEntity];
                    
                    _statsAccessor.Update(ref state);
                    
                    modifiersBuffer = state.EntityManager.GetBuffer<TestStatModifier>(newObserverEntity);
                    AddSimpleModifiers(ref tester, newObserverStatOwner.StatA, ref modifiersBuffer);
                    
                    _statsAccessor.TryAddStatModifier(
                        newObserverStatOwner.StatA,
                        new TestStatModifier
                        {
                            ModifierType = TestStatModifier.Type.AddFromStat,
                            StatHandleA = observedStatOwner.StatA,
                            ValueA = 0f,
                        },
                        out _, 
                        ref _statsWorldData);

                    observedStatOwner = newObserverStatOwner;
                }
            }

            tester.HasInitialized = true;
        }
        
        _statsAccessor.Update(ref state);

        state.Dependency = new UpdatingStatsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            StatsAccessor = _statsAccessor,
            StatsWorldData = _statsWorldData,
        }.Schedule(state.Dependency);

        state.Dependency = new StatGetValueJob()
        {
        }.ScheduleParallel(state.Dependency);
    }

    private void AddSimpleModifiers(ref StatsTester tester, StatHandle onStat, ref DynamicBuffer<TestStatModifier> modifiers)
    {
        UnsafeList<TestStatModifier> addedSimpleModifiers = new UnsafeList<TestStatModifier>(tester.SimpleAddModifiersAdded, Allocator.Temp);
        for (int j = 0; j < tester.SimpleAddModifiersAdded; j++)
        {
            addedSimpleModifiers.Add(new TestStatModifier
            {
                ModifierType = TestStatModifier.Type.Add,
                ValueA = 1f,
            });
        }

        if (addedSimpleModifiers.Length > 0)
        {
            UnsafeList<StatModifierHandle> tmpHandles = new UnsafeList<StatModifierHandle>(tester.SimpleAddModifiersAdded, Allocator.Temp);
            _statsAccessor.TryAddStatModifiersBatch(
                onStat,
                in addedSimpleModifiers,
                ref tmpHandles,
                ref _statsWorldData);
        }
    }

    [BurstCompile]
    [WithAll(typeof(UpdatingStat))]
    public partial struct UpdatingStatsJob : IJobEntity
    {
        public float DeltaTime;
        public StatsAccessor<TestStatModifier, TestStatModifier.Stack> StatsAccessor;
        public StatsWorldData<TestStatModifier, TestStatModifier.Stack> StatsWorldData;

        void Execute(ref TestStatOwner statsOwner)
        {
            StatsAccessor.TryAddStatBaseValue(statsOwner.StatA, DeltaTime, ref StatsWorldData);
        }
    }

    [BurstCompile]
    public partial struct StatGetValueJob : IJobEntity
    {
        void Execute(ref TestStatOwner test, in DynamicBuffer<Stat> statsBuffer)
        {
            StatsUtilities.GetStat(test.StatA, in statsBuffer, out test.tmp, out _);
        }
    }
}