using Trove.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

[assembly: RegisterGenericJobType(typeof(StatsUpdateSubSystem<StatModifier, StatModifier.Stack>.BatchRecomputeDirtyStatsJob))]
[assembly: RegisterGenericJobType(typeof(StatsUpdateSubSystem<StatModifier, StatModifier.Stack>.RecomputeDirtyStatsImmediateJob))]

public struct UpdatingStat : IComponentData
{ }

[UpdateBefore(typeof(StatsUpdateSystem))]
partial struct StatsTesterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsTester>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref StatsTester tester = ref SystemAPI.GetSingletonRW<StatsTester>().ValueRW;

        if (!tester.HasInitialized)
        {
            state.EntityManager.CompleteAllTrackedJobs();

            UnsafeList<Trove.Stats.StatHandle> tmpObservedStatsList = new UnsafeList<Trove.Stats.StatHandle>(10, Allocator.Temp);

            for (int i = 0; i < tester.UnchangingAttributesCount; i++)
            {
                state.EntityManager.Instantiate(tester.StatOwnerPrefab);
            }

            for (int i = 0; i < tester.ChangingAttributesCount; i++)
            {
                Entity observedEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);
                state.EntityManager.AddComponentData(observedEntity, new UpdatingStat());

                if(tester.MakeOtherStatsDependOnFirstStatOfChangingAttributes)
                {
                    BufferLookup<Trove.Stats.StatObserver> statObserversBufferLookup = SystemAPI.GetBufferLookup<Trove.Stats.StatObserver>(false);
                    BufferLookup<Trove.Stats.DirtyStat> dirtyStatsBufferLookup = SystemAPI.GetBufferLookup<Trove.Stats.DirtyStat>(false);
                    ComponentLookup<Trove.Stats.HasDirtyStats> hasDirtyStatsLookup = SystemAPI.GetComponentLookup<Trove.Stats.HasDirtyStats>(false);
                    Trove.Stats.StatOwner statOwner = state.EntityManager.GetComponentData<StatOwner>(observedEntity);
                    DynamicBuffer<StatModifier> statModifiersBuffer = state.EntityManager.GetBuffer<StatModifier>(observedEntity);
                    DynamicBuffer<Trove.Stats.StatObserver> statObserversBuffer = state.EntityManager.GetBuffer<Trove.Stats.StatObserver>(observedEntity);
                    DynamicBuffer<Trove.Stats.DirtyStat> dirtyStatsBuffer = state.EntityManager.GetBuffer<Trove.Stats.DirtyStat>(observedEntity);
                    EnabledRefRW<Trove.Stats.HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<Trove.Stats.HasDirtyStats>(observedEntity);

                    ModifierHandle modifierHandle1 = StatUtilities.AddModifier<StatModifier, StatModifier.Stack>(
                        new Trove.Stats.StatHandle(observedEntity, 1),
                        new StatModifier
                        {
                            ModifierType = StatModifier.Type.AddFromStat,
                            StatA = new StatHandle(observedEntity, 0),
                            ValueA = 0f,
                        },
                        ref statOwner,
                        ref statModifiersBuffer,
                        ref statObserversBuffer,
                        ref dirtyStatsBuffer,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsBufferLookup,
                        ref hasDirtyStatsLookup,
                        ref tmpObservedStatsList);

                    ModifierHandle modifierHandle2 = StatUtilities.AddModifier<StatModifier, StatModifier.Stack>(
                        new Trove.Stats.StatHandle(observedEntity, 2),
                        new StatModifier
                        {
                            ModifierType = StatModifier.Type.AddFromStat,
                            StatA = new StatHandle(observedEntity, 0),
                            ValueA = 0f,
                        },
                        ref statOwner,
                        ref statModifiersBuffer,
                        ref statObserversBuffer,
                        ref dirtyStatsBuffer,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsBufferLookup,
                        ref hasDirtyStatsLookup,
                        ref tmpObservedStatsList);
                }

                for (int j = 0; j < tester.ChangingAttributesChildDepth; j++)
                {
                    Entity observerEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);

                    BufferLookup<Trove.Stats.StatObserver> statObserversBufferLookup = SystemAPI.GetBufferLookup<Trove.Stats.StatObserver>(false);
                    BufferLookup<Trove.Stats.DirtyStat> dirtyStatsBufferLookup = SystemAPI.GetBufferLookup<Trove.Stats.DirtyStat>(false);
                    ComponentLookup<Trove.Stats.HasDirtyStats> hasDirtyStatsLookup = SystemAPI.GetComponentLookup<Trove.Stats.HasDirtyStats>(false);
                    Trove.Stats.StatOwner statOwner = state.EntityManager.GetComponentData<StatOwner>(observerEntity);
                    DynamicBuffer<StatModifier> statModifiersBuffer = state.EntityManager.GetBuffer<StatModifier>(observerEntity);
                    DynamicBuffer<Trove.Stats.StatObserver> statObserversBuffer = state.EntityManager.GetBuffer<Trove.Stats.StatObserver>(observerEntity);
                    DynamicBuffer< Trove.Stats.DirtyStat > dirtyStatsBuffer = state.EntityManager.GetBuffer<Trove.Stats.DirtyStat>(observerEntity);
                    EnabledRefRW<Trove.Stats.HasDirtyStats> hasDirtyStatsEnabledRefRW = hasDirtyStatsLookup.GetEnabledRefRW<Trove.Stats.HasDirtyStats>(observerEntity);

                    ModifierHandle modifierHandle = StatUtilities.AddModifier<StatModifier, StatModifier.Stack>(
                        new Trove.Stats.StatHandle(observerEntity, 0),
                        new StatModifier
                        {
                            ModifierType = StatModifier.Type.AddFromStat,
                            StatA = new StatHandle(observedEntity, 0),
                            ValueA = 0f,
                        },
                        ref statOwner,
                        ref statModifiersBuffer,
                        ref statObserversBuffer,
                        ref dirtyStatsBuffer,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsBufferLookup,
                        ref hasDirtyStatsLookup,
                        ref tmpObservedStatsList);

                    observedEntity = observerEntity;
                }
            }

            tmpObservedStatsList.Dispose();
            tester.HasInitialized = true;
        }

        state.Dependency = new UpdatingStatsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(UpdatingStat))]
    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    public partial struct UpdatingStatsJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(
            Entity entity, 
            ref DynamicBuffer<Trove.Stats.Stat> statsBuffer,
            ref DynamicBuffer<Trove.Stats.DirtyStat> dirtyStatsBuffer,
            EnabledRefRW<Trove.Stats.HasDirtyStats> hasDirtyStatsEnabledRefRW)
        {
            Trove.Stats.Stat stat = statsBuffer[0];
            stat.BaseValue += DeltaTime;
            statsBuffer[0] = stat;

            StatUtilities.MarkStatForBatchRecompute(
                0,
                ref dirtyStatsBuffer,
                hasDirtyStatsEnabledRefRW);
        }
    }
}


partial struct StatsUpdateSystem : ISystem
{
    private StatsUpdateSubSystem<StatModifier, StatModifier.Stack> _statsUpdateSubSystem;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsTester>();

        _statsUpdateSubSystem = new StatsUpdateSubSystem<StatModifier, StatModifier.Stack>();
        _statsUpdateSubSystem.OnCreate(ref state);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _statsUpdateSubSystem.OnDestroy(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _statsUpdateSubSystem.OnUpdate(ref state);
    }
}