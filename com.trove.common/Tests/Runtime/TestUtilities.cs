using Unity.Collections;
using Unity.Entities;

namespace Trove.Tests
{
    public struct TestEntity : IComponentData
    { }

    public static class TestUtilities
    {
        public static Entity CreateTestEntity(EntityManager entityManager)
        {
            Entity testEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(testEntity, new TestEntity());
            return testEntity;
        }

        public static void DestroyTestEntities(World world)
        {
            EntityQuery testEntitiesQuery =
                new EntityQueryBuilder(Allocator.Temp).WithAll<TestEntity>().Build(world.EntityManager);
            world.EntityManager.DestroyEntity(testEntitiesQuery);
        }

        public static bool TryGetSingleton<T>(EntityManager entityManager, out T singleton) where T : unmanaged, IComponentData
        {
            EntityQuery singletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(entityManager);
            if (singletonQuery.HasSingleton<T>())
            {
                singleton = singletonQuery.GetSingleton<T>();
                return true;
            }

            singleton = default;
            return false;
        }
    }
}