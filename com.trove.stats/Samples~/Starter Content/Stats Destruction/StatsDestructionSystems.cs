using Trove.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

internal struct StatsDestructionSingleton : IComponentData
{
    public NativeList<StatHandle> StatsToUpdate;
}

[UpdateInGroup(typeof(EntityDestructionSystemGroup))]
partial struct StatsPreDestructionSystem : ISystem
{
    private NativeList<StatHandle> _statsToUpdate;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsDestructionSingleton>();
        
        _statsToUpdate = new NativeList<StatHandle>(Allocator.Persistent);
        
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new StatsDestructionSingleton { StatsToUpdate = _statsToUpdate });
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_statsToUpdate.IsCreated)
        {
            _statsToUpdate.Dispose();
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StatsDestructionSingleton statsDestructionSingleton = SystemAPI.GetSingletonRW<StatsDestructionSingleton>().ValueRW;

        state.Dependency = new StatsPreDestructionJob
        {
            StatsToUpdate = statsDestructionSingleton.StatsToUpdate,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(DestroyEntity))]
    [WithAll(typeof(SampleStatModifier))] // needed so we don't try to update stat handles that cannot use the same StatAccessor<T,S> type
    public partial struct StatsPreDestructionJob : IJobEntity
    {
        public NativeList<StatHandle> StatsToUpdate;
        
        public void Execute(Entity entity, ref DynamicBuffer<StatObserver> statObserversBuffer)
        {
            StatsUtilities.GetOtherDependantStatsOfEntity(entity, ref statObserversBuffer, ref StatsToUpdate);
        }
    }
}

[UpdateInGroup(typeof(EntityDestructionSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(EntityDestructionSystem))]
partial struct StatsPostDestructionSystem : ISystem
{
    private StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> _statsAccessor;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsWorldSingleton>();
        state.RequireForUpdate<StatsDestructionSingleton>();
        
        _statsAccessor = new StatsAccessor<SampleStatModifier, SampleStatModifier.Stack>(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StatsWorldSingleton statsWorldSingleton = SystemAPI.GetSingletonRW<StatsWorldSingleton>().ValueRW;
        StatsDestructionSingleton statsDestructionSingleton = SystemAPI.GetSingletonRW<StatsDestructionSingleton>().ValueRW;
        _statsAccessor.Update(ref state);

        state.Dependency = new StatsPostDestructionJob
        {
            StatsToUpdate = statsDestructionSingleton.StatsToUpdate,
            StatsAccessor = _statsAccessor,
            StatsWorldData = statsWorldSingleton.StatsWorldData,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct StatsPostDestructionJob : IJob
    {
        public NativeList<StatHandle> StatsToUpdate;
        public StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> StatsAccessor;
        public StatsWorldData<SampleStatModifier, SampleStatModifier.Stack> StatsWorldData;
        
        public void Execute()
        {
            for (int i = 0; i < StatsToUpdate.Length; i++)
            {
                StatsAccessor.TryUpdateStat(StatsToUpdate[i], ref StatsWorldData);
            }
            StatsToUpdate.Clear();
        }
    }
}
