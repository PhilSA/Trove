using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove.ObjectHandles.Tests
{
    public struct TestEntity : IComponentData
    { }

    public struct TestValueObject1
    {
        public int A;
    }

    public struct TestValueObject2
    {
        public int4 A;
        public float B;
    }

    public struct TestValueObject3
    {
        public float4x4 A;
        public float4x4 B;
        public float4x4 C;
        public float4x4 D;
        public float4x4 E;
        public float4x4 F;
        public float4x4 G;
    }

    public struct ValueObjectElement1 : IBufferElementData
    {
        public ObjectData<TestValueObject1> Data;
    }

    public struct FreeRangeElement1 : IBufferElementData
    {
        public IndexRangeElement Range;
    }

    public struct VirtualObjectsElement1 : IBufferElementData
    {
        public byte Data;
    }

    public static class ObjectHandlesTestUtilities
    {
        public static Entity CreateTestEntity(EntityManager entityManager)
        {
            Entity testEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(testEntity, new TestEntity());
            return testEntity;
        }

        public static Entity CreateTestEntityWithVirtualObjectManager1(EntityManager entityManager)
        {
            Entity testEntity = CreateTestEntity(entityManager);
            entityManager.AddBuffer<VirtualObjectsElement1>(testEntity);
            return testEntity;
        }

        public static Entity CreateTestEntityWithValueObjectManager1(EntityManager entityManager)
        {
            Entity testEntity = CreateTestEntity(entityManager);
            entityManager.AddBuffer<FreeRangeElement1>(testEntity);
            entityManager.AddBuffer<ValueObjectElement1>(testEntity);
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