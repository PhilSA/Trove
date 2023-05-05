using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Trove
{
    [TestFixture]
    public class TransformUtilitiesTests : MonoBehaviour
    {
        public struct TestEntity : IComponentData
        {
            public int ID;
        }

        private World World => World.DefaultGameObjectInjectionWorld;
        private EntityManager EntityManager => World.EntityManager;
        private List<GameObject> _testGOs = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {            
            foreach (var t in _testGOs)
            {
                GameObject.Destroy(t);
            }

            EntityQuery testEntitiesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<TestEntity>().Build(EntityManager);
            EntityManager.DestroyEntity(testEntitiesQuery);
        }

        public Entity CreateTestTransformEntity(int id = 0)
        {
            Entity entity = EntityManager.CreateEntity(typeof(TestEntity));
            EntityManager.AddComponentData(entity, new LocalTransform { Position = default, Rotation = quaternion.identity, Scale = 1f });
            EntityManager.AddComponentData(entity, new LocalToWorld());
            return entity;
        }

        public GameObject CreateTestTransformGO()
        {
            GameObject go = new GameObject();
            _testGOs.Add(go);
            return go;
        }

        [Test]
        public void GetWorldTransform()
        {
            Entity e1 = CreateTestTransformEntity();
            Entity e2 = CreateTestTransformEntity();
            Entity e3 = CreateTestTransformEntity();

            // Transform 1
            {
                float3 pos1 = new float3(5f, 12f, 2f);
                quaternion rot1 = quaternion.Euler(1f, 0.4f, 11f);
                float scale1 = 2f;

                EntityManager.SetComponentData(e1, new LocalTransform
                {
                    Position = pos1,
                    Rotation = rot1,
                    Scale = scale1,
                });
            }

            // Transform 2
            {
                float3 pos2 = new float3(2f, 6f, 4f);
                quaternion rot2 = quaternion.Euler(00.6f, 0.44f, 6f);
                float scale2 = 0.5f;

                EntityManager.AddComponentData(e2, new Parent { Value = e1 });
                EntityManager.SetComponentData(e2, new LocalTransform
                {
                    Position = pos2,
                    Rotation = rot2,
                    Scale = scale2,
                });
            }

            // Transform 3
            {
                float3 pos3 = new float3(11f, 0.6f, 7f);
                quaternion rot3 = quaternion.Euler(0.88f, 2f, 3.33f);
                float scale3 = 0.9f;

                EntityManager.AddComponentData(e3, new Parent { Value = e2 });
                EntityManager.SetComponentData(e3, new LocalTransform
                {
                    Position = pos3,
                    Rotation = rot3,
                    Scale = scale3,
                });
            }

            World.Update();

            ComponentLookup<Parent> parentLookup = World.GetOrCreateSystemManaged<SimulationSystemGroup>().GetComponentLookup<Parent>(false);
            ComponentLookup<LocalTransform> localTransformLookup = World.GetOrCreateSystemManaged<SimulationSystemGroup>().GetComponentLookup<LocalTransform>(false);
            TransformUtilities.GetWorldTransform(e1, in parentLookup, in localTransformLookup, out float4x4 worldTransformE1);
            TransformUtilities.GetWorldTransform(e2, in parentLookup, in localTransformLookup, out float4x4 worldTransformE2);
            TransformUtilities.GetWorldTransform(e3, in parentLookup, in localTransformLookup, out float4x4 worldTransformE3);
            LocalToWorld ltw1 = EntityManager.GetComponentData<LocalToWorld>(e1);
            LocalToWorld ltw2 = EntityManager.GetComponentData<LocalToWorld>(e2);
            LocalToWorld ltw3 = EntityManager.GetComponentData<LocalToWorld>(e3);

            Assert.IsTrue(worldTransformE1.Position().IsRoughlyEqual(ltw1.Position));
            Assert.IsTrue(worldTransformE1.Rotation().IsRoughlyEqual(ltw1.Rotation));
            Assert.IsTrue(worldTransformE1.Scale().IsRoughlyEqual(ltw1.Value.Scale()));

            Assert.IsTrue(worldTransformE2.Position().IsRoughlyEqual(ltw2.Position));
            Assert.IsTrue(worldTransformE2.Rotation().IsRoughlyEqual(ltw2.Rotation));
            Assert.IsTrue(worldTransformE2.Scale().IsRoughlyEqual(ltw2.Value.Scale()));

            Assert.IsTrue(worldTransformE3.Position().IsRoughlyEqual(ltw3.Position));
            Assert.IsTrue(worldTransformE3.Rotation().IsRoughlyEqual(ltw3.Rotation));
            Assert.IsTrue(worldTransformE3.Scale().IsRoughlyEqual(ltw3.Value.Scale()));
        }
    }
}
