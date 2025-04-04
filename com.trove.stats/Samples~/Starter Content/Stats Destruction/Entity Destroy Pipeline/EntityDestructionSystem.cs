using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(EntityDestructionSystemGroup), OrderLast = true)]
partial struct EntityDestructionSystem : ISystem
{
    private EntityQuery _destroyEntitiesQuery;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _destroyEntitiesQuery = SystemAPI.QueryBuilder().WithAll<DestroyEntity>().Build();
        state.RequireForUpdate(_destroyEntitiesQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.DestroyEntity(_destroyEntitiesQuery);
    }
}
