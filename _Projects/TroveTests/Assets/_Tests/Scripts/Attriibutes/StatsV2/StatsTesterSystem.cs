using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Trove.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

//[assembly: RegisterGenericComponentType(typeof(DirtyStatsMask<DirtyStatsMaskValue>))]
[assembly: RegisterGenericJobType(typeof(StatsUpdateSubSystem<StatModifier, StatModifier.Stack>.MarkStatObserversDirtyJob))]
[assembly: RegisterGenericJobType(typeof(StatsUpdateSubSystem<StatModifier, StatModifier.Stack>.CompileChangedStatsDataJob))]
[assembly: RegisterGenericJobType(typeof(StatsUpdateSubSystem<StatModifier, StatModifier.Stack>.BatchRecomputeDirtyStatsJob))]
[assembly: RegisterGenericJobType(typeof(StatsUpdateSubSystem<StatModifier, StatModifier.Stack>.RecomputeDirtyStatsImmediateJob))]
[assembly: RegisterGenericJobType(typeof(StatsUpdateSubSystem<StatModifier, StatModifier.Stack>.EnqueueDirtyStatsForRecomputeImmediateJob))]
[assembly: RegisterGenericJobType(typeof(StatsUpdateSubSystem<StatModifier, StatModifier.Stack>.ApplyHasDirtyStatsJob))]

//[StructLayout(LayoutKind.Explicit)]
//public struct DirtyStatsMaskValue : IDirtyStatsBitMask
//{
//    // Each ulong allows up to 64 stats
//    [FieldOffset(0)]
//    public ulong A;

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    public bool GetSubMask(uint index, out ulong submask)
//    {
//        switch (index)
//        {
//            case 0:
//                submask = A;
//                return true;
//        }
//        submask = default;
//        return false;
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    public int GetSubMaskCount()
//    {
//        return 1;
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    public void SetSubMask(uint index, ulong submask)
//    {
//        switch (index)
//        {
//            case 0:
//                A = submask;
//                break;
//        }
//    }
//}

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
                    ComponentLookup<Trove.Stats.DirtyStatsMask> dirtyStatsMaskLookup = SystemAPI.GetComponentLookup<Trove.Stats.DirtyStatsMask>(false);
                    ComponentLookup<Trove.Stats.HasDirtyStats> hasDirtyStatsLookup = SystemAPI.GetComponentLookup<Trove.Stats.HasDirtyStats>(false);
                    Trove.Stats.StatOwner statOwner = state.EntityManager.GetComponentData<StatOwner>(observedEntity);
                    DynamicBuffer<StatModifier> statModifiersBuffer = state.EntityManager.GetBuffer<StatModifier>(observedEntity);
                    DynamicBuffer<Trove.Stats.StatObserver> statObserversBuffer = state.EntityManager.GetBuffer<Trove.Stats.StatObserver>(observedEntity);
                    ref Trove.Stats.DirtyStatsMask dirtyStatsMask = ref dirtyStatsMaskLookup.GetRefRW(observedEntity).ValueRW;
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
                        ref dirtyStatsMask,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsMaskLookup,
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
                        ref dirtyStatsMask,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsMaskLookup,
                        ref hasDirtyStatsLookup,
                        ref tmpObservedStatsList);
                }

                for (int j = 0; j < tester.ChangingAttributesChildDepth; j++)
                {
                    Entity observerEntity = state.EntityManager.Instantiate(tester.StatOwnerPrefab);

                    BufferLookup<Trove.Stats.StatObserver> statObserversBufferLookup = SystemAPI.GetBufferLookup<Trove.Stats.StatObserver>(false);
                    ComponentLookup<Trove.Stats.DirtyStatsMask> dirtyStatsMaskLookup = SystemAPI.GetComponentLookup<Trove.Stats.DirtyStatsMask>(false);
                    ComponentLookup<Trove.Stats.HasDirtyStats> hasDirtyStatsLookup = SystemAPI.GetComponentLookup<Trove.Stats.HasDirtyStats>(false);
                    Trove.Stats.StatOwner statOwner = state.EntityManager.GetComponentData<StatOwner>(observerEntity);
                    DynamicBuffer<StatModifier> statModifiersBuffer = state.EntityManager.GetBuffer<StatModifier>(observerEntity);
                    DynamicBuffer<Trove.Stats.StatObserver> statObserversBuffer = state.EntityManager.GetBuffer<Trove.Stats.StatObserver>(observerEntity);
                    ref Trove.Stats.DirtyStatsMask dirtyStatsMask = ref dirtyStatsMaskLookup.GetRefRW(observerEntity).ValueRW;
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
                        ref dirtyStatsMask,
                        hasDirtyStatsEnabledRefRW,
                        ref statObserversBufferLookup,
                        ref dirtyStatsMaskLookup,
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
            ref Trove.Stats.DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<HasDirtyStats> hasDirtyStatsEnabledRefRW)
        {
            Trove.Stats.Stat stat = statsBuffer[0];
            stat.BaseValue += DeltaTime;
            statsBuffer[0] = stat;

            StatUtilities.MarkStatForBatchRecompute(
                0,
                ref dirtyStatsMask,
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