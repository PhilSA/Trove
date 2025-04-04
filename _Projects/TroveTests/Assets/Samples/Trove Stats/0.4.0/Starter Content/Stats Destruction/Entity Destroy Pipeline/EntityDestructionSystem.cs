using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(EntityDestructionSystemGroup), OrderLast = true)]
partial struct EntityDestructionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityQuery destroyEntitiesQuery = SystemAPI.QueryBuilder().WithAll<DestroyEntity>().Build();
        state.EntityManager.DestroyEntity(destroyEntitiesQuery);
    }
}
