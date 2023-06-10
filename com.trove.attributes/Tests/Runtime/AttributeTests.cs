using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using AttributeChanger = Trove.Attributes.AttributeChanger<Trove.Attributes.Tests.AttributeModifier, Trove.Attributes.Tests.AttributeModifierStack, Trove.Attributes.Tests.AttributeGetterSetter>;
using AttributeUtilities = Trove.Attributes.AttributeUtilities<Trove.Attributes.Tests.AttributeModifier, Trove.Attributes.Tests.AttributeModifierStack, Trove.Attributes.Tests.AttributeGetterSetter>;
using AttributeCommand = Trove.Attributes.AttributeCommand<Trove.Attributes.Tests.AttributeModifier, Trove.Attributes.Tests.AttributeModifierStack, Trove.Attributes.Tests.AttributeGetterSetter>;

namespace Trove.Attributes.Tests
{
    public struct TestEntity : IComponentData
    {
        public int ID;
    }

    public struct AttributeA : IComponentData
    {
        public AttributeValues Values;
    }

    public struct AttributeB : IComponentData
    {
        public AttributeValues Values;
    }

    public struct AttributeC : IComponentData
    {
        public AttributeValues Values;
    }

    public static class AttributeTestUtilities
    {
        public static bool IsRoughlyEqual(this float a, float b, float error = 0.001f)
        {
            return math.distance(a, b) <= error;
        }
    }

    [TestFixture]
    public class AttributeTests
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

        public void MakeTestEntity(Entity entity, int id = 0)
        {
            EntityManager.AddComponentData(entity, new TestEntity { ID = id });
        }

        public void MakeTestEntity(ref EntityCommandBuffer ecb, Entity entity, int id = 0)
        {
            ecb.AddComponent(entity, new TestEntity { ID = id });
        }

        public Entity CreateAttributesEntity(bool hasA, bool hasB, bool hasC, int id = 0)
        {
            Entity entity = CreateTestEntity(id);
            AttributeUtilities.MakeAttributeOwner(EntityManager, entity);

            if (hasA)
            {
                EntityManager.AddComponentData(entity, new AttributeA
                {
                    Values = new AttributeValues(10f),
                });
            }

            if (hasB)
            {
                EntityManager.AddComponentData(entity, new AttributeB
                {
                    Values = new AttributeValues(10f),
                });
            }

            if (hasC)
            {
                EntityManager.AddComponentData(entity, new AttributeC
                {
                    Values = new AttributeValues(10f),
                });
            }

            return entity;
        }

        public Entity CreateECBAttributesEntity(bool hasA, bool hasB, bool hasC, ref EntityCommandBuffer ecb, int id = 0)
        {
            Entity entity = CreateECBTestEntity(ref ecb, id);
            AttributeUtilities.MakeAttributeOwner(ecb, entity);

            if (hasA)
            {
                ecb.AddComponent(entity, new AttributeA
                {
                    Values = new AttributeValues(10f),
                });
            }

            if (hasB)
            {
                ecb.AddComponent(entity, new AttributeB
                {
                    Values = new AttributeValues(10f),
                });
            }

            if (hasC)
            {
                ecb.AddComponent(entity, new AttributeC
                {
                    Values = new AttributeValues(10f),
                });
            }

            return entity;
        }

        public bool FindEntityWithID(int id, out Entity entity)
        {
            bool success = false;
            entity = default;

            EntityQuery query = new EntityQueryBuilder(Allocator.Temp).WithAll<TestEntity>().Build(EntityManager);
            var entities = query.ToEntityArray(Allocator.Temp);
            var ids = query.ToComponentDataArray<TestEntity>(Allocator.Temp);
            for (int i = 0; i < ids.Length; i++)
            {
                if(ids[i].ID == id)
                {
                    entity = entities[i];
                    success = true;
                    break;
                }
            }

            entities.Dispose();
            ids.Dispose();

            return success;
        }

        public AttributeChanger CreateAttributeChanger()
        {
            ref SystemState state = ref World.GetOrCreateSystemManaged<SimulationSystemGroup>().CheckedStateRef;
            AttributeChanger attributeChanger = new AttributeChanger(ref state);
            attributeChanger.UpdateData(ref state);
            return attributeChanger;
        }

        public void UpdateProcessCommandsSystem()
        {
            ref SystemState state = ref World.GetOrCreateSystemManaged<SimulationSystemGroup>().CheckedStateRef;
            state.WorldUnmanaged.GetExistingUnmanagedSystem<ProcessAttributeChangerCommandsSystem>().Update(state.WorldUnmanaged);
        }

        [Test]
        public void AttributeChanger()
        {
            Entity entity1 = CreateAttributesEntity(true, true, false);
            Entity entity2 = CreateAttributesEntity(true, true, false);
            AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute2B = new AttributeReference(entity2, (int)AttributeType.B);

            AttributeChanger attributeChanger = CreateAttributeChanger();

            DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            DynamicBuffer<AttributeModifier> modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
            DynamicBuffer<AttributeObserver> observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);

            AttributeValues valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);

            attributeChanger.SetBaseValue(attribute1A, 2f); 
            
            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(2f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(2f));
            Assert.AreEqual(0, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);

            attributeChanger.AddBaseValue(attribute1A, 1f); 
            
            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(3f));
            Assert.AreEqual(0, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);

            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2B), out ModifierReference modifier1);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(13f));
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(0, modifiers2.Length);
            Assert.AreEqual(1, observers2.Length);
            Assert.AreEqual(1, observers2[0].Count);
            Assert.AreEqual(attribute1A, observers2[0].ObserverAttribute);
            Assert.AreEqual(1, modifier1.ID);

            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2B), out ModifierReference modifier2);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(23f));
            Assert.AreEqual(2, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(0, modifiers2.Length);
            Assert.AreEqual(1, observers2.Length);
            Assert.AreEqual(2, observers2[0].Count);
            Assert.AreEqual(attribute1A, observers2[0].ObserverAttribute);
            Assert.AreEqual(2, modifier2.ID);

            // Remove mod 1
            attributeChanger.RemoveModifier(modifier1);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(13f));
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(0, modifiers2.Length);
            Assert.AreEqual(1, observers2.Length);
            Assert.AreEqual(1, observers2[0].Count);
            Assert.AreEqual(attribute1A, observers2[0].ObserverAttribute);

            // Remove mod 1 again
            attributeChanger.RemoveModifier(modifier1);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(13f));
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(0, modifiers2.Length);
            Assert.AreEqual(1, observers2.Length);
            Assert.AreEqual(1, observers2[0].Count);
            Assert.AreEqual(attribute1A, observers2[0].ObserverAttribute);

            // Remove mod 2 again
            attributeChanger.RemoveModifier(modifier2);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(3f));
            Assert.AreEqual(0, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(0, modifiers2.Length);
            Assert.AreEqual(0, observers2.Length);

            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2B), out ModifierReference modifier3);
            attributeChanger.SetBaseValue(attribute1A, 5f, false);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(3, modifier3.ID);

            attributeChanger.RecalculateAttributeAndAllObservers(attribute1A);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(15f));
        }

        [Test]
        public void ECBCommands_Values()
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            Entity entity1 = CreateECBAttributesEntity(true, false, false, ref ecb, 1);
            AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);

            Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out DynamicBuffer<AttributeCommand> attributeCommands);
            MakeTestEntity(ref ecb, commandsEntity);
            attributeCommands.Add(AttributeCommand.Create_SetBaseValue(attribute1A, 2f));
            attributeCommands.Add(AttributeCommand.Create_AddBaseValue(attribute1A, 1f));

            ecb.Playback(EntityManager);
            ecb.Dispose();
            UpdateProcessCommandsSystem();

            FindEntityWithID(1, out entity1);
            AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(3f));
        }

        [Test]
        public void ECBCommands_Modifiers()
        {
            Entity notificationsEntity = CreateTestEntity();
            EntityManager.AddBuffer<ModifierReferenceNotification>(notificationsEntity);

            Entity entity1 = CreateAttributesEntity(true, true, true, 1);
            Entity entity2 = CreateAttributesEntity(true, true, true, 2);
            Entity entity3 = CreateAttributesEntity(true, true, true, 3);
            AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
            AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
            AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
            AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);

            EntityCommandBuffer ecb = default;
            DynamicBuffer<AttributeCommand> attributeCommands = default;

            // ----------------------------------------------------------

            {
                ecb = new EntityCommandBuffer(Allocator.Temp);

                Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out attributeCommands);
                MakeTestEntity(ref ecb, commandsEntity);
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));

                ecb.Playback(EntityManager);
                ecb.Dispose();
                UpdateProcessCommandsSystem();
            }
            
            FindEntityWithID(1, out entity1);
            attribute1A.Entity = entity1;
            attribute1B.Entity = entity1;
            attribute1C.Entity = entity1;
            FindEntityWithID(2, out entity2);
            attribute2A.Entity = entity2;
            FindEntityWithID(3, out entity3);
            attribute3A.Entity = entity3;

            AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            AttributeValues values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
            AttributeValues values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
            AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            AttributeValues values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            DynamicBuffer<AttributeModifier> modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
            DynamicBuffer<AttributeModifier> modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
            DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            DynamicBuffer<AttributeObserver> observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
            DynamicBuffer<AttributeObserver> observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);
            DynamicBuffer<ModifierReferenceNotification> modifierNotifications = EntityManager.GetBuffer<ModifierReferenceNotification>(notificationsEntity);

            Assert.AreEqual(2, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(30f));
            Assert.AreEqual(2, modifierNotifications.Length);
            Assert.IsTrue(new AttributeReference(entity1, (int)AttributeType.A).IsSame(modifierNotifications[0].ModifierReference.AffectedAttribute));
            Assert.AreEqual(1, modifierNotifications[0].ModifierReference.ID);
            Assert.AreEqual(2, modifierNotifications[1].ModifierReference.ID);

            // ----------------------------------------------------------

            {
                ecb = new EntityCommandBuffer(Allocator.Temp);

                Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out attributeCommands);
                MakeTestEntity(ref ecb, commandsEntity);
                attributeCommands.Add(AttributeCommand.Create_RemoveModifier(modifierNotifications[0].ModifierReference));

                ecb.Playback(EntityManager);
                ecb.Dispose();
                UpdateProcessCommandsSystem();
            }

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(20f));
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);

            // ----------------------------------------------------------

            {
                ecb = new EntityCommandBuffer(Allocator.Temp);

                Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out attributeCommands);
                MakeTestEntity(ref ecb, commandsEntity);
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity1));

                ecb.Playback(EntityManager);
                ecb.Dispose();
                UpdateProcessCommandsSystem();
            }

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);

            // ----------------------------------------------------------

            {
                ecb = new EntityCommandBuffer(Allocator.Temp);

                Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out attributeCommands);
                MakeTestEntity(ref ecb, commandsEntity);
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity1));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity2));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity3));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute1B), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1B, AttributeModifier.Create_AddFromAttribute(attribute1C), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiersAffectingAttribute(attribute1A));

                ecb.Playback(EntityManager);
                ecb.Dispose();
                UpdateProcessCommandsSystem();
            }

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
            modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1B.Value.IsRoughlyEqual(20f));
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(1, observers1.Length);

            // ----------------------------------------------------------

            {
                ecb = new EntityCommandBuffer(Allocator.Temp);

                Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out attributeCommands);
                MakeTestEntity(ref ecb, commandsEntity);
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity1));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity2));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity3));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_Add(5f), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_Add(5f), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiersObservingAttribute(attribute2A));

                ecb.Playback(EntityManager);
                ecb.Dispose();
                UpdateProcessCommandsSystem();
            }

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
            observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(20f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(2, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(0, modifiers3.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(0, observers2.Length);

            // ----------------------------------------------------------

            {
                ecb = new EntityCommandBuffer(Allocator.Temp);

                Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out attributeCommands);
                MakeTestEntity(ref ecb, commandsEntity);
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity1));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity2));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiers(entity3));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute2A), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_Add(5f), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_AddModifier(attribute1A, AttributeModifier.Create_Add(5f), notificationsEntity));
                attributeCommands.Add(AttributeCommand.Create_RemoveAllModifiersObservingAttributeOnEntity(entity1, attribute2A));

                ecb.Playback(EntityManager);
                ecb.Dispose();
                UpdateProcessCommandsSystem();
            }

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
            observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(20f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(20f));
            Assert.AreEqual(2, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(1, modifiers3.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(1, observers2.Length);
        }

        [Test]
        public void ComplexTree()
        {
            /*
             *           1
             *         /   \
             *        2 --> 3
             *       /  \ /  \
             *      4    5    6
             *       \ / | \ /
             *        7 <-- 8
             *         \ | /
             *           9
             *           
             * (    A                       )
             * (   /   means B observes A   )
             * (  B  (bottom observes top)  )
             * 
             * ( A --> B means A observes B )
            */

            Entity entity1 = CreateAttributesEntity(true, true, true);
            Entity entity2 = CreateAttributesEntity(true, true, true);
            Entity entity3 = CreateAttributesEntity(true, true, true);
            Entity entity4 = CreateAttributesEntity(true, true, true);
            Entity entity5 = CreateAttributesEntity(true, true, true);
            Entity entity6 = CreateAttributesEntity(true, true, true);
            Entity entity7 = CreateAttributesEntity(true, true, true);
            Entity entity8 = CreateAttributesEntity(true, true, true);
            Entity entity9 = CreateAttributesEntity(true, true, true);
            AttributeReference attribute1 = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute2 = new AttributeReference(entity2, (int)AttributeType.A);
            AttributeReference attribute3 = new AttributeReference(entity3, (int)AttributeType.A);
            AttributeReference attribute4 = new AttributeReference(entity4, (int)AttributeType.A);
            AttributeReference attribute5 = new AttributeReference(entity5, (int)AttributeType.A);
            AttributeReference attribute6 = new AttributeReference(entity6, (int)AttributeType.A);
            AttributeReference attribute7 = new AttributeReference(entity7, (int)AttributeType.A);
            AttributeReference attribute8 = new AttributeReference(entity8, (int)AttributeType.A);
            AttributeReference attribute9 = new AttributeReference(entity9, (int)AttributeType.A);

            ModifierReference modifier = default;
            AttributeChanger attributeChanger = CreateAttributeChanger();
            attributeChanger.AddModifier(attribute2, AttributeModifier.Create_AddFromAttribute(attribute1), out modifier);
            attributeChanger.AddModifier(attribute2, AttributeModifier.Create_AddFromAttribute(attribute3), out modifier);
            attributeChanger.AddModifier(attribute3, AttributeModifier.Create_AddFromAttribute(attribute1), out modifier);
            attributeChanger.AddModifier(attribute4, AttributeModifier.Create_AddFromAttribute(attribute2), out modifier);
            attributeChanger.AddModifier(attribute5, AttributeModifier.Create_AddFromAttribute(attribute2), out modifier);
            attributeChanger.AddModifier(attribute5, AttributeModifier.Create_AddFromAttribute(attribute3), out modifier);
            attributeChanger.AddModifier(attribute6, AttributeModifier.Create_AddFromAttribute(attribute3), out modifier);
            attributeChanger.AddModifier(attribute7, AttributeModifier.Create_AddFromAttribute(attribute4), out modifier);
            attributeChanger.AddModifier(attribute7, AttributeModifier.Create_AddFromAttribute(attribute5), out modifier);
            attributeChanger.AddModifier(attribute8, AttributeModifier.Create_AddFromAttribute(attribute5), out modifier);
            attributeChanger.AddModifier(attribute8, AttributeModifier.Create_AddFromAttribute(attribute6), out modifier);
            attributeChanger.AddModifier(attribute8, AttributeModifier.Create_AddFromAttribute(attribute7), out modifier);
            attributeChanger.AddModifier(attribute9, AttributeModifier.Create_AddFromAttribute(attribute5), out modifier);
            attributeChanger.AddModifier(attribute9, AttributeModifier.Create_AddFromAttribute(attribute7), out modifier);
            attributeChanger.AddModifier(attribute9, AttributeModifier.Create_AddFromAttribute(attribute8), out modifier);

            AttributeValues values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            AttributeValues values2 = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            AttributeValues values3 = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            AttributeValues values4 = EntityManager.GetComponentData<AttributeA>(entity4).Values;
            AttributeValues values5 = EntityManager.GetComponentData<AttributeA>(entity5).Values;
            AttributeValues values6 = EntityManager.GetComponentData<AttributeA>(entity6).Values;
            AttributeValues values7 = EntityManager.GetComponentData<AttributeA>(entity7).Values;
            AttributeValues values8 = EntityManager.GetComponentData<AttributeA>(entity8).Values;
            AttributeValues values9 = EntityManager.GetComponentData<AttributeA>(entity9).Values;

            Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values2.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2.Value.IsRoughlyEqual(40f));
            Assert.IsTrue(values3.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3.Value.IsRoughlyEqual(20f));
            Assert.IsTrue(values4.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values4.Value.IsRoughlyEqual(50f));
            Assert.IsTrue(values5.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values5.Value.IsRoughlyEqual(70f));
            Assert.IsTrue(values6.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values6.Value.IsRoughlyEqual(30f));
            Assert.IsTrue(values7.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values7.Value.IsRoughlyEqual(130f));
            Assert.IsTrue(values8.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values8.Value.IsRoughlyEqual(240f));
            Assert.IsTrue(values9.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values9.Value.IsRoughlyEqual(450f));
        }

        [Test]
        public void SelfEntityObserver()
        {
            // Create an attribute with a modifier that affects another attribute on itself

            Entity entity1 = CreateAttributesEntity(true, true, true);
            AttributeReference attribute1 = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute2 = new AttributeReference(entity1, (int)AttributeType.B);

            ModifierReference modifier = default;
            AttributeChanger attributeChanger = CreateAttributeChanger();
            attributeChanger.AddModifier(attribute1, AttributeModifier.Create_AddFromAttribute(attribute2), out modifier);

            AttributeValues values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);

            Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1.Value.IsRoughlyEqual(20f));
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(1, observers1.Length);

            attributeChanger.AddBaseValue(attribute2, 5f);

            values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;

            Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1.Value.IsRoughlyEqual(25f));
        }

        [Test]
        public void SelfAttributeObserver()
        {
            // Create an attribute with a modifier that affects itself

            Entity entity1 = CreateAttributesEntity(true, true, true);
            AttributeReference attribute1 = new AttributeReference(entity1, (int)AttributeType.A);

            ModifierReference modifier = default;
            AttributeChanger attributeChanger = CreateAttributeChanger();
            attributeChanger.AddModifier(attribute1, AttributeModifier.Create_AddFromAttribute(attribute1), out modifier);

            AttributeValues values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);

            Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);

            attributeChanger.AddBaseValue(attribute1, 5f);

            values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;

            Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(values1.Value.IsRoughlyEqual(15f));
        }

        [Test]
        public void ModifiersFiltering()
        {
            // Create an attribute with modifiers affecting 3 different attributes on it.
            // Validate that only the modifiers on the specific attributes are taken into account.

            Entity entity1 = CreateAttributesEntity(true, true, true);
            Entity entity2 = CreateAttributesEntity(true, true, true);
            Entity entity3 = CreateAttributesEntity(true, true, true);
            Entity entity4 = CreateAttributesEntity(true, true, true);
            AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
            AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
            AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
            AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);
            AttributeReference attribute4A = new AttributeReference(entity4, (int)AttributeType.A);

            AttributeChanger attributeChanger = CreateAttributeChanger();

            attributeChanger.SetBaseValue(attribute1A, 1f);
            attributeChanger.SetBaseValue(attribute1B, 2f);
            attributeChanger.SetBaseValue(attribute1C, 3f);
            attributeChanger.SetBaseValue(attribute2A, 4f);
            attributeChanger.SetBaseValue(attribute3A, 5f);
            attributeChanger.SetBaseValue(attribute4A, 6f);

            ModifierReference modifier = default;
            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifier);
            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute1C), out modifier);
            attributeChanger.AddModifier(attribute1B, AttributeModifier.Create_AddFromAttribute(attribute3A), out modifier);
            attributeChanger.AddModifier(attribute1C, AttributeModifier.Create_AddFromAttribute(attribute4A), out modifier);

            AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            AttributeValues values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
            AttributeValues values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(1f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(14f));
            Assert.IsTrue(values1B.BaseValue.IsRoughlyEqual(2f));
            Assert.IsTrue(values1B.Value.IsRoughlyEqual(7f));
            Assert.IsTrue(values1C.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(values1C.Value.IsRoughlyEqual(9f));
        }

        [Test]
        public void ObserversFiltering()
        {
            // Create an entity observed by 3 different attributes (each observing a different attribute on the entity).
            // Validate that only the specific observers are recalculated.

            Entity entity1 = CreateAttributesEntity(true, true, true);
            Entity entity2 = CreateAttributesEntity(true, true, true);
            Entity entity3 = CreateAttributesEntity(true, true, true);
            Entity entity4 = CreateAttributesEntity(true, true, true);
            AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
            AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
            AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
            AttributeReference attribute2B = new AttributeReference(entity2, (int)AttributeType.B);
            AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);
            AttributeReference attribute4A = new AttributeReference(entity4, (int)AttributeType.A);

            ModifierReference modifier = default;
            AttributeChanger attributeChanger = CreateAttributeChanger();
            attributeChanger.AddModifier(attribute2A, AttributeModifier.Create_AddFromAttribute(attribute1A), out modifier);
            attributeChanger.AddModifier(attribute2B, AttributeModifier.Create_AddFromAttribute(attribute1B), out modifier);
            attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute1B), out modifier);
            attributeChanger.AddModifier(attribute4A, AttributeModifier.Create_AddFromAttribute(attribute1C), out modifier);

            AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            AttributeValues values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
            AttributeValues values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            AttributeValues values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;

            Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2A.Value.IsRoughlyEqual(20f));
            Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2B.Value.IsRoughlyEqual(20f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(20f));
            Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values4A.Value.IsRoughlyEqual(20f));

            // Set 2B to 0 and check that it didn't get recalculated when 1A changes (because it doesn't observe it)
            values2B.__internal__value = 0f;
            EntityManager.SetComponentData(entity2, new AttributeB { Values = values2B });
            attributeChanger.AddBaseValue(attribute1A, 1f);

            values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
            values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;

            Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2A.Value.IsRoughlyEqual(21f));
            Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2B.Value.IsRoughlyEqual(0f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(20f));
            Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values4A.Value.IsRoughlyEqual(20f));

            // Set 2A to 0 and check that it didn't get recalculated when 1B changes (because it doesn't observe it)
            values2A.__internal__value = 0f;
            EntityManager.SetComponentData(entity2, new AttributeA { Values = values2A });
            attributeChanger.AddBaseValue(attribute1B, 1f);

            values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
            values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;

            Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2A.Value.IsRoughlyEqual(0f));
            Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2B.Value.IsRoughlyEqual(21f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(21f));
            Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values4A.Value.IsRoughlyEqual(20f));

            values2A.__internal__value = 0f;
            EntityManager.SetComponentData(entity2, new AttributeA { Values = values2A });
            values2B.__internal__value = 0f;
            EntityManager.SetComponentData(entity2, new AttributeB { Values = values2B });
            values3A.__internal__value = 0f;
            EntityManager.SetComponentData(entity3, new AttributeA { Values = values3A });
            attributeChanger.AddBaseValue(attribute1C, 1f);

            values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
            values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;

            Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2A.Value.IsRoughlyEqual(0f));
            Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2B.Value.IsRoughlyEqual(0f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(0f));
            Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values4A.Value.IsRoughlyEqual(21f));
        }

        [Test]
        public void SelfRemoveModifierAndObserver()
        {
            Entity entity1 = CreateAttributesEntity(true, true, true);
            Entity entity2 = CreateAttributesEntity(true, true, true);
            Entity entity3 = CreateAttributesEntity(true, true, true);
            AttributeReference attributeA = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attributeB = new AttributeReference(entity2, (int)AttributeType.B);
            AttributeReference attributeC = new AttributeReference(entity3, (int)AttributeType.C);

            AttributeChanger attributeChanger = CreateAttributeChanger();

            DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            DynamicBuffer<AttributeModifier> modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
            DynamicBuffer<AttributeObserver> observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
            DynamicBuffer<AttributeModifier> modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
            DynamicBuffer<AttributeObserver> observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);

            AttributeValues valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;

            attributeChanger.AddModifier(attributeA, AttributeModifier.Create_AddFromAttribute(attributeB), out ModifierReference modifier1);
            attributeChanger.AddModifier(attributeC, AttributeModifier.Create_AddFromAttribute(attributeA), out ModifierReference modifier2);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(20f));
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(1, observers1.Length);
            Assert.AreEqual(0, modifiers2.Length);
            Assert.AreEqual(1, observers2.Length);
            Assert.AreEqual(1, modifiers3.Length);
            Assert.AreEqual(0, observers3.Length);

            EntityManager.DestroyEntity(entity2);
            EntityManager.DestroyEntity(entity3);

            modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);

            attributeChanger = CreateAttributeChanger();
            attributeChanger.RecalculateAttributeAndAllObservers(attributeA);

            valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(valuesA.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, modifiers1.Length);
            Assert.AreEqual(0, observers1.Length);
        }


        [Test]
        public void DestroyObservedAttribute()
        {
            Entity entity1 = CreateAttributesEntity(true, true, true);
            Entity entity2 = CreateAttributesEntity(true, true, true);
            Entity entity3 = CreateAttributesEntity(true, true, true);
            AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
            AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
            AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
            AttributeReference attribute2B = new AttributeReference(entity2, (int)AttributeType.B);
            AttributeReference attribute2C = new AttributeReference(entity2, (int)AttributeType.C);
            AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);
            AttributeReference attribute3B = new AttributeReference(entity3, (int)AttributeType.B);
            AttributeReference attribute3C = new AttributeReference(entity3, (int)AttributeType.C);

            AttributeChanger attributeChanger = CreateAttributeChanger();

            AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            AttributeValues values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
            AttributeValues values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
            AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            AttributeValues values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
            AttributeValues values2C = EntityManager.GetComponentData<AttributeC>(entity2).Values;
            AttributeValues values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            AttributeValues values3B = EntityManager.GetComponentData<AttributeB>(entity3).Values;
            AttributeValues values3C = EntityManager.GetComponentData<AttributeC>(entity3).Values;
            DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            DynamicBuffer<AttributeModifier> modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
            DynamicBuffer<AttributeObserver> observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
            DynamicBuffer<AttributeModifier> modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
            DynamicBuffer<AttributeObserver> observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);

            ModifierReference modifierReference = default;
            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifierReference);
            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifierReference);
            attributeChanger.AddModifier(attribute1B, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifierReference);
            attributeChanger.AddModifier(attribute1C, AttributeModifier.Create_AddFromAttribute(attribute2B), out modifierReference);
            attributeChanger.AddModifier(attribute2A, AttributeModifier.Create_AddFromAttribute(attribute2B), out modifierReference);
            attributeChanger.AddModifier(attribute2C, AttributeModifier.Create_AddFromAttribute(attribute1B), out modifierReference);
            attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute2C), out modifierReference);
            attributeChanger.AddModifier(attribute2A, AttributeModifier.Create_AddFromAttribute(attribute3B), out modifierReference);
            attributeChanger.AddModifier(attribute3C, AttributeModifier.Create_AddFromAttribute(attribute1A), out modifierReference);

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
            values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
            values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
            values2C = EntityManager.GetComponentData<AttributeC>(entity2).Values;
            values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            values3B = EntityManager.GetComponentData<AttributeB>(entity3).Values;
            values3C = EntityManager.GetComponentData<AttributeC>(entity3).Values;
            modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
            observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
            modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
            observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(70f));
            Assert.IsTrue(values1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1B.Value.IsRoughlyEqual(40f));
            Assert.IsTrue(values1C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1C.Value.IsRoughlyEqual(20f));
            Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2A.Value.IsRoughlyEqual(30f));
            Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2B.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2C.Value.IsRoughlyEqual(50f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(60f));
            Assert.IsTrue(values3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3B.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values3C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3C.Value.IsRoughlyEqual(80f));
            Assert.AreEqual(4, modifiers1.Length);
            Assert.AreEqual(2, observers1.Length);
            Assert.AreEqual(3, modifiers2.Length);
            Assert.AreEqual(5, observers2.Length);
            Assert.AreEqual(2, modifiers3.Length);
            Assert.AreEqual(1, observers3.Length);

            // Destroy entity and prepare attributes owner for destruction
            {
                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

                Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out DynamicBuffer<AttributeCommand> attributeCommands);
                MakeTestEntity(ref ecb, commandsEntity);

                AttributeUtilities.NotifyAttributesOwnerDestruction(entity2, ref observers2, ref attributeCommands);
                 
                ecb.DestroyEntity(entity2);
                ecb.Playback(EntityManager);
                ecb.Dispose();

                // After ecb playback, process commands to recalculate
                UpdateProcessCommandsSystem();
            }

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
            values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
            values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            values3B = EntityManager.GetComponentData<AttributeB>(entity3).Values;
            values3C = EntityManager.GetComponentData<AttributeC>(entity3).Values;
            modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
            observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1B.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values1C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1C.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3B.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values3C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3C.Value.IsRoughlyEqual(20f));
            Assert.AreEqual(0, modifiers1.Length);
            Assert.AreEqual(1, observers1.Length);
            Assert.AreEqual(1, modifiers3.Length);
            Assert.AreEqual(1, observers3.Length);
        }

        [Test]
        public void InfiniteObserversLoopPrevention()
        {
            //                                             6->\
            // Try to create an infinite observers loop: 1->2->3->4
            //                                                  \->1  (this one would cause the infinite loop)
            //                                                   \->5
            // ( A->B means "A observes B" )

            ModifierReference tmpModifierID = default;

            Entity entity1 = CreateAttributesEntity(true, false, false);
            Entity entity2 = CreateAttributesEntity(true, false, false);
            Entity entity3 = CreateAttributesEntity(true, false, false);
            Entity entity4 = CreateAttributesEntity(true, false, false);
            Entity entity5 = CreateAttributesEntity(true, false, false);
            Entity entity6 = CreateAttributesEntity(true, false, false);

            AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
            AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);
            AttributeReference attribute4A = new AttributeReference(entity4, (int)AttributeType.A);
            AttributeReference attribute5A = new AttributeReference(entity5, (int)AttributeType.A);
            AttributeReference attribute6A = new AttributeReference(entity6, (int)AttributeType.A);

            AttributeChanger attributeChanger = CreateAttributeChanger();

            attributeChanger.AddModifier(attribute6A, AttributeModifier.Create_AddFromAttribute(attribute3A), out tmpModifierID);
            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), out tmpModifierID);
            attributeChanger.AddModifier(attribute2A, AttributeModifier.Create_AddFromAttribute(attribute3A), out tmpModifierID);
            attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute4A), out tmpModifierID);
            attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute1A), out tmpModifierID);
            attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute5A), out tmpModifierID);

            AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            AttributeValues values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            AttributeValues values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;
            AttributeValues values5A = EntityManager.GetComponentData<AttributeA>(entity5).Values;
            DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            DynamicBuffer<AttributeModifier> modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
            DynamicBuffer<AttributeModifier> modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
            DynamicBuffer<AttributeModifier> modifiers4 = EntityManager.GetBuffer<AttributeModifier>(entity4);
            DynamicBuffer<AttributeModifier> modifiers5 = EntityManager.GetBuffer<AttributeModifier>(entity5);
            DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
            DynamicBuffer<AttributeObserver> observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
            DynamicBuffer<AttributeObserver> observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);
            DynamicBuffer<AttributeObserver> observers4 = EntityManager.GetBuffer<AttributeObserver>(entity4);
            DynamicBuffer<AttributeObserver> observers5 = EntityManager.GetBuffer<AttributeObserver>(entity5);

            // Check that the modifier making 3 observe 1 wasn't added, because it would've caused a loop
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(1, modifiers2.Length);
            Assert.AreEqual(2, modifiers3.Length); // 2 successful, 1 unsuccessful
            Assert.AreEqual(0, modifiers4.Length);
            Assert.AreEqual(0, modifiers5.Length);
            Assert.AreEqual(0, observers1.Length);
            Assert.AreEqual(1, observers2.Length);
            Assert.AreEqual(2, observers3.Length);
            Assert.AreEqual(1, observers4.Length);
            Assert.AreEqual(1, observers5.Length);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(50f));
            Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2A.Value.IsRoughlyEqual(40f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(30f));
            Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values4A.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values5A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values5A.Value.IsRoughlyEqual(10f));

            attributeChanger.AddBaseValue(attribute3A, 1f);

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
            values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
            values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;
            values5A = EntityManager.GetComponentData<AttributeA>(entity5).Values;

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(51f));
            Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values2A.Value.IsRoughlyEqual(41f));
            Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(11f));
            Assert.IsTrue(values3A.Value.IsRoughlyEqual(31f));
            Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values4A.Value.IsRoughlyEqual(10f));
            Assert.IsTrue(values5A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(values5A.Value.IsRoughlyEqual(10f));
        }

        [Test]
        public void ValidateLoopDetectionTakesObservedTypeIntoAccount()
        {
            // - make 1A observe 1C
            // - make 1C observe 2A
            // Before making 1C an observer of 2A, LoopDetection will check if 2A already observes 1C to prevent a loop.
            // It will look at the observers of 1C and find 1A, so it does LoopDetection of if 2A already observes 1A.
            // It will look at the observers of 1A. If it didn't take observed attribute type into account, it would find
            // 1A as an observer to check again, creating an infinite loop of checking id 2A observes 1A.
            // So make sure it knows that 1A isn't an observer of 1A (just because it's an observer on the same entity as 1C).

            Entity entity1 = CreateAttributesEntity(true, true, true);
            Entity entity2 = CreateAttributesEntity(true, true, true);
            AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
            AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
            AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
            AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);

            AttributeChanger attributeChanger = CreateAttributeChanger();

            attributeChanger.SetBaseValue(attribute1A, 1f);
            attributeChanger.SetBaseValue(attribute1B, 2f);
            attributeChanger.SetBaseValue(attribute1C, 3f);
            attributeChanger.SetBaseValue(attribute2A, 4f);

            ModifierReference modifier = default;
            attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute1C), out modifier);

            AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
            DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(1f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(4f));
            Assert.AreEqual(1, modifiers1.Length);
            Assert.AreEqual(1, observers1.Length);

            attributeChanger.AddModifier(attribute1C, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifier);

            values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
            AttributeValues values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
            AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;

            Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(1f));
            Assert.IsTrue(values1A.Value.IsRoughlyEqual(8f));
            Assert.IsTrue(values1C.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(values1C.Value.IsRoughlyEqual(7f));
            Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(4f));
            Assert.IsTrue(values2A.Value.IsRoughlyEqual(4f));
        }
    }
}