using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Trove.Tweens.Tests
{
    public struct TestEntity : IComponentData
    {
        public int ID;
    }

    [TestFixture]
    public class TweenTests
    {
        private World World => World.DefaultGameObjectInjectionWorld;
        private EntityManager EntityManager => World.EntityManager;

        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
            EntityQuery testEntitiesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<TestEntity>().Build(EntityManager);
            EntityManager.DestroyEntity(testEntitiesQuery);
        }

        public Entity CreateTestEntity(int id = 0)
        {
            Entity entity = EntityManager.CreateEntity(typeof(TestEntity));
            EntityManager.AddComponentData(entity, new TestEntity { ID = id });
            return entity;
        }

        public Entity CreateECBTestEntity(ref EntityCommandBuffer ecb, int id = 0)
        {
            Entity entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new TestEntity { ID = id });
            return entity;
        }

        [Test]
        public void Test()
        {
        }
    }
}