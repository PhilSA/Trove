using Unity.Collections;
using Unity.Entities;

namespace Trove.EventSystems.Tests
{
    public struct TestEntity : IComponentData
    { }

    public struct EntityEventTestSingleton : IComponentData
    {
        public Entity ReceiverEntity1;
        public Entity ReceiverEntity2;
    }

    public struct EntityEventReceiver : IComponentData
    {
        public int MainThread_EventCounterVal1;
        public int MainThread_EventCounterVal2;
        public int MainThread_EventCounterVal3;
        public int MainThread_EventCounterVal4;
        public int MainThread_EventCounterVal5;
        public int MainThread_EventCounterVal6;

        public int SingleJob_EventCounterVal1;
        public int SingleJob_EventCounterVal2;
        public int SingleJob_EventCounterVal3;
        public int SingleJob_EventCounterVal4;
        public int SingleJob_EventCounterVal5;
        public int SingleJob_EventCounterVal6;

        public int ParallelJob_EventCounterVal1;
        public int ParallelJob_EventCounterVal2;
        public int ParallelJob_EventCounterVal3;
        public int ParallelJob_EventCounterVal4;
        public int ParallelJob_EventCounterVal5;
        public int ParallelJob_EventCounterVal6;
    }

    public static class EventTestUtilities
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

        public static void AddEventToCounter(ref NativeHashMap<int, int> eventsCounter, int key)
        {
            if (eventsCounter.TryGetValue(key, out int count))
            {
                eventsCounter[key] = count + 1;
            }
            else
            {
                eventsCounter.Add(key, 1);
            }
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