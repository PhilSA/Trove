using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Trove.Stats;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

[assembly: RegisterGenericComponentType(typeof(StatModifier<Trove.Stats.Tests.StatsTestsStatModifier, Trove.Stats.Tests.StatsTestsStatModifier.Stack>))]

namespace Trove.Stats.Tests
{
    public struct TestEntity : IComponentData
    {
        public int ID;
    }

    public static class StatsTestUtilities
    {
        public static bool IsRoughlyEqual(this float a, float b, float error = 0.001f)
        {
            return math.distance(a, b) <= error;
        }
    }

    [InternalBufferCapacity(8)]
    public struct StatsTestsStatModifier : IStatsModifier<StatsTestsStatModifier.Stack>
    {
        public enum Type
        {
            Add,
            AddFromStat,
            AddFromTwoStatsMax,
            AddInt,
        }

        public struct Stack : IStatsModifierStack
        {
            public bool IsIntStack;
            public float Add;
            public float Multiplier;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                Add = 0f;
                Multiplier = 1f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Apply(in float statBaseValue, ref float statValue)
            {
                if (IsIntStack)
                {
                    int intStatValue = StatsUtilities.AsInt(statValue);
                    intStatValue += StatsUtilities.AsInt(Add);
                    intStatValue *= StatsUtilities.AsInt(Multiplier);
                    
                    statValue = StatsUtilities.AsFloat(intStatValue);
                }
                else
                {
                    statValue = statBaseValue;
                    statValue += Add;
                    statValue *= Multiplier;
                }
            }
        }

        public Type ModifierType;
        public float ValueA;
        public StatHandle StatHandleA;
        public StatHandle StatHandleB;

        public bool MustRemove;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObservedStatsToList(ref UnsafeList<StatHandle> observedStatHandles)
        {
            switch (ModifierType)
            {
                case (Type.AddFromStat):
                    observedStatHandles.Add(StatHandleA);
                    break;
                case (Type.AddFromTwoStatsMax):
                    observedStatHandles.Add(StatHandleA);
                    observedStatHandles.Add(StatHandleB);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(ref StatsReader statsReader, ref Stack stack, out bool shouldProduceModifierTriggerEvent)
        {
            shouldProduceModifierTriggerEvent = false;
            switch (ModifierType)
            {
                case (Type.Add):
                {
                    stack.Add += ValueA;
                    break;
                }
                case (Type.AddFromStat):
                {
                    if (statsReader.TryGetStat(StatHandleA, out float statAValue, out float _))
                    {
                        stack.Add += statAValue;
                        break;
                    }

                    MustRemove = true;
                    shouldProduceModifierTriggerEvent = true;
                    break;
                }
                case (Type.AddFromTwoStatsMax):
                {
                    if (statsReader.TryGetStat(StatHandleA, out float statAValue, out float _) &&
                        statsReader.TryGetStat(StatHandleB, out float statBValue, out float _))
                    {
                        stack.Add += math.max(statAValue, statBValue);
                        break;
                    }
                    
                    MustRemove = true;
                    shouldProduceModifierTriggerEvent = true;
                    break;
                }
                case (Type.AddInt):
                {
                    stack.IsIntStack = true;
                    int stackAddInt = StatsUtilities.AsInt(stack.Add);
                    stackAddInt += StatsUtilities.AsInt(ValueA);
                    stack.Add = StatsUtilities.AsFloat(stackAddInt);
                    break;
                }
            }
        }
    }

    [TestFixture]
    public class StatsTests
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

        public StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> CreateStatsWorld()
        {
            ref SystemState state = ref World.GetOrCreateSystemManaged<SimulationSystemGroup>().CheckedStateRef;
            StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorld = 
                new StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(ref state);
            statsWorld.Update(ref state);
            return statsWorld;
        }

        public void UpdateStatsWorld(ref StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorld)
        {
            ref SystemState state = ref World.GetOrCreateSystemManaged<SimulationSystemGroup>().CheckedStateRef;
            statsWorld.Update(ref state);
        }

        public Entity CreateStatsEntity(
            int id,
            float baseValue,
            bool produceChangeEvents,
            out StatHandle statHandleA,
            out StatHandle statHandleB,
            out StatHandle statHandleC)
        {
            StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorld = CreateStatsWorld();
            Entity entity = CreateTestEntity(id);
            StatsUtilities.AddStatsComponents<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(entity, EntityManager);
            
            UpdateStatsWorld(ref statsWorld);
            
            bool success = true;
            success &= statsWorld.TryCreateStat(entity, baseValue, produceChangeEvents, out statHandleA);
            success &= statsWorld.TryCreateStat(entity, baseValue, produceChangeEvents, out statHandleB);
            success &= statsWorld.TryCreateStat(entity, baseValue, produceChangeEvents, out statHandleC);
            Assert.IsTrue(success);
            
            return entity;
        }

        private bool GetStatAndModifiersAndObserversCount(StatHandle statHandle, ref StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorld, out Stat stat, out int modifiersCount,
            out int observersCount)
        {
            bool success = true;
            success &= StatsUtilities.TryGetStat(statHandle, ref statsWorld._statsLookup, out stat);
            success &= statsWorld.TryCalculateModifiersCount(statHandle, out modifiersCount);
            success &= statsWorld.TryCalculateObserversCount(statHandle, out observersCount);
            return success;
        }

        private void AssertBufferLengths(Entity entity, int statsLength, int modifiersLength, int observersLength)
        {
            Assert.AreEqual(statsLength, EntityManager.GetBuffer<Stat>(entity).Length);
            Assert.AreEqual(modifiersLength, EntityManager.GetBuffer<StatModifier<StatsTestsStatModifier, StatsTestsStatModifier.Stack>>(entity).Length);
            Assert.AreEqual(observersLength, EntityManager.GetBuffer<StatObserver>(entity).Length);
        }

        [Test]
        public void BasicStatOperations()
        {
            bool success = false;
            StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorld = CreateStatsWorld();

            Entity entity1 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle1A, out StatHandle statHandle1B,
                out StatHandle statHandle1C);
            
            UpdateStatsWorld(ref statsWorld);
            
            success = GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out Stat stat1A, out int stat1AModifiersCount, out int stat1AObserversCount);
            Assert.IsTrue(success);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);

            success = statsWorld.TrySetStatBaseValue(statHandle1A, 2f); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(2f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(2f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);

            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(3f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);

            success = statsWorld.TryMultiplyStatBaseValue(statHandle1A, 2f); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(6f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(6f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);

            success = statsWorld.TryMultiplyStatBaseValue(statHandle1A, -2f); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(-12f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(-12f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);

            success = statsWorld.TryAddStatBaseValue(statHandle1A, -4f); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(-16f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(-16f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
        }

        [Test]
        public void SameEntityStatModifiers()
        {
            bool success = false;
            StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorld = CreateStatsWorld();

            Entity entity1 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle1A, out StatHandle statHandle1B,
                out StatHandle statHandle1C);
            
            UpdateStatsWorld(ref statsWorld);

            success = statsWorld.TrySetStatBaseValue(statHandle1A, 5f); 
            Assert.IsTrue(success);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out Stat stat1A,
                out int stat1AModifiersCount, out int stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out Stat stat1B,
                out int stat1BModifiersCount, out int stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out Stat stat1C,
                out int stat1CModifiersCount, out int stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);

            // -------------------------------------------------
            // Add modifiers
            // -------------------------------------------------
            
            success = statsWorld.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 2f,
                },
                out StatModifierHandle modifier1);
            Assert.IsTrue(success);
            
            Assert.IsTrue(modifier1.AffectedStatHandle == statHandle1B);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(1, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            AssertBufferLengths(entity1, 3, 1, 0);

            success = statsWorld.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier2);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(17f));
            Assert.AreEqual(2, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            AssertBufferLengths(entity1, 3, 2, 1);

            success = statsWorld.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier3);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(22f));
            Assert.AreEqual(3, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            AssertBufferLengths(entity1, 3, 3, 2);

            success = statsWorld.TryAddStatModifier(
                statHandle1C,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1B,
                },
                out StatModifierHandle modifier4);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(22f));
            Assert.AreEqual(3, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(32f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 4, 3);

            success = statsWorld.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 4f,
                },
                out StatModifierHandle modifier5);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(9f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(30f));
            Assert.AreEqual(3, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(40f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 5, 3);

            // -------------------------------------------------
            // Change values
            // -------------------------------------------------
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 2f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(34f));
            Assert.AreEqual(3, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(44f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 5, 3);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1B, 5f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(3, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(49f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 5, 3);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1C, 9f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(3, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(58f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 5, 3);
            
            // -------------------------------------------------
            // Remove modifiers and change values
            // -------------------------------------------------

            success = statsWorld.TryRemoveStatModifier(modifier2);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(28f));
            Assert.AreEqual(2, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(47f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 4, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(8f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(29f));
            Assert.AreEqual(2, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(48f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 4, 2);

            success = statsWorld.TryRemoveStatModifier(modifier5);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(8f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(8f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(25f));
            Assert.AreEqual(2, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(44f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 3, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(9f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(9f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(26f));
            Assert.AreEqual(2, stat1BModifiersCount);
            Assert.AreEqual(1, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(45f));
            Assert.AreEqual(1, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 3, 2);

            success = statsWorld.TryRemoveStatModifier(modifier4);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(9f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(9f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(26f));
            Assert.AreEqual(2, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 2, 1);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(27f));
            Assert.AreEqual(2, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 2, 1);

            success = statsWorld.TryRemoveStatModifier(modifier1);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(25f));
            Assert.AreEqual(1, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 1, 1);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(11f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(26f));
            Assert.AreEqual(1, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 1, 1);

            success = statsWorld.TryRemoveStatModifier(modifier3);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(11f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(15f));
            Assert.AreEqual(0, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsWorld, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsWorld, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(12f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(15f));
            Assert.AreEqual(0, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
        }

        [Test]
        public void CrossEntityStatModifiers()
        {
            bool success = false;
            StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorld = CreateStatsWorld();

            Entity entity1 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle1A, out StatHandle statHandle1B,
                out StatHandle statHandle1C);
            Entity entity2 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle2A, out StatHandle statHandle2B,
                out StatHandle statHandle2C);
            Entity entity3 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle3A, out StatHandle statHandle3B,
                out StatHandle statHandle3C);
            
            UpdateStatsWorld(ref statsWorld);

            success = statsWorld.TrySetStatBaseValue(statHandle1A, 5f); 
            Assert.IsTrue(success);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out Stat stat1A,
                out int stat1AModifiersCount, out int stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out Stat stat2A,
                out int stat2AModifiersCount, out int stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out Stat stat2C,
                out int stat2CModifiersCount, out int stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out Stat stat3A,
                out int stat3AModifiersCount, out int stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out Stat stat3B,
                out int stat3BModifiersCount, out int stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            AssertBufferLengths(entity2, 3, 0, 0);
            AssertBufferLengths(entity3, 3, 0, 0);

            // -------------------------------------------------
            // Add modifiers
            // -------------------------------------------------
            
            success = statsWorld.TryAddStatModifier(
                statHandle2A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 2f,
                },
                out StatModifierHandle modifier1);
            Assert.IsTrue(success);
            
            Assert.IsTrue(modifier1.AffectedStatHandle == statHandle2A);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(1, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            AssertBufferLengths(entity2, 3, 1, 0);
            AssertBufferLengths(entity3, 3, 0, 0);

            success = statsWorld.TryAddStatModifier(
                statHandle2A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier2);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(17f));
            Assert.AreEqual(2, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 1);
            AssertBufferLengths(entity2, 3, 2, 0);
            AssertBufferLengths(entity3, 3, 0, 0);

            success = statsWorld.TryAddStatModifier(
                statHandle2A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier3);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(22f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 2);
            AssertBufferLengths(entity2, 3, 3, 0);
            AssertBufferLengths(entity3, 3, 0, 0);

            success = statsWorld.TryAddStatModifier(
                statHandle3A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle2A,
                },
                out StatModifierHandle modifier4);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(22f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(32f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(0, stat3AObserversCount);
            AssertBufferLengths(entity1, 3, 0, 2);
            AssertBufferLengths(entity2, 3, 3, 1);
            AssertBufferLengths(entity3, 3, 1, 0);

            success = statsWorld.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 4f,
                },
                out StatModifierHandle modifier5);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(9f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(30f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(40f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(0, stat3AObserversCount);
            AssertBufferLengths(entity1, 3, 1, 2);
            AssertBufferLengths(entity2, 3, 3, 1);
            AssertBufferLengths(entity3, 3, 1, 0);

            success = statsWorld.TryAddStatModifier(
                statHandle3B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3A,
                },
                out StatModifierHandle modifier6);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(9f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(30f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(40f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(50f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(0, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 1, 2);
            AssertBufferLengths(entity2, 3, 3, 1);
            AssertBufferLengths(entity3, 3, 2, 1);

            success = statsWorld.TryAddStatModifier(
                statHandle2C,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3B,
                },
                out StatModifierHandle modifier7);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(9f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(30f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(60f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(40f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(50f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 1, 2);
            AssertBufferLengths(entity2, 3, 4, 1);
            AssertBufferLengths(entity3, 3, 2, 2);

            // -------------------------------------------------
            // Change values
            // -------------------------------------------------
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 2f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(34f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(64f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(44f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(54f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 1, 2);
            AssertBufferLengths(entity2, 3, 4, 1);
            AssertBufferLengths(entity3, 3, 2, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle2A, 5f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(69f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(49f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(59f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 1, 2);
            AssertBufferLengths(entity2, 3, 4, 1);
            AssertBufferLengths(entity3, 3, 2, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle3A, 9f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(78f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(58f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(68f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 1, 2);
            AssertBufferLengths(entity2, 3, 4, 1);
            AssertBufferLengths(entity3, 3, 2, 2);
            
            // -------------------------------------------------
            // Remove modifiers and change values
            // -------------------------------------------------

            success = statsWorld.TryRemoveStatModifier(modifier2);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(28f));
            Assert.AreEqual(2, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(67f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(47f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(57f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 1, 1);
            AssertBufferLengths(entity2, 3, 3, 1);
            AssertBufferLengths(entity3, 3, 2, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(8f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(29f));
            Assert.AreEqual(2, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(68f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(48f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(58f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 1, 1);
            AssertBufferLengths(entity2, 3, 3, 1);
            AssertBufferLengths(entity3, 3, 2, 2);

            success = statsWorld.TryRemoveStatModifier(modifier5);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(8f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(8f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(25f));
            Assert.AreEqual(2, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(64f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(44f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(54f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 1);
            AssertBufferLengths(entity2, 3, 3, 1);
            AssertBufferLengths(entity3, 3, 2, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(9f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(9f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(26f));
            Assert.AreEqual(2, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(65f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(45f));
            Assert.AreEqual(1, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(55f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 1);
            AssertBufferLengths(entity2, 3, 3, 1);
            AssertBufferLengths(entity3, 3, 2, 2);

            success = statsWorld.TryRemoveStatModifier(modifier4);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(9f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(9f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(26f));
            Assert.AreEqual(2, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(39));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(29f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 1);
            AssertBufferLengths(entity2, 3, 3, 0);
            AssertBufferLengths(entity3, 3, 1, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(27f));
            Assert.AreEqual(2, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(29f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 1);
            AssertBufferLengths(entity2, 3, 3, 0);
            AssertBufferLengths(entity3, 3, 1, 2);

            success = statsWorld.TryRemoveStatModifier(modifier1);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(25f));
            Assert.AreEqual(1, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(29f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 1);
            AssertBufferLengths(entity2, 3, 2, 0);
            AssertBufferLengths(entity3, 3, 1, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(11f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(26f));
            Assert.AreEqual(1, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(29f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 1);
            AssertBufferLengths(entity2, 3, 2, 0);
            AssertBufferLengths(entity3, 3, 1, 2);

            success = statsWorld.TryRemoveStatModifier(modifier3);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(11f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(15f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(29f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            AssertBufferLengths(entity2, 3, 1, 0);
            AssertBufferLengths(entity3, 3, 1, 2);
            
            success = statsWorld.TryAddStatBaseValue(statHandle1A, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(12f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(15f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(1, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(29f));
            Assert.AreEqual(1, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            AssertBufferLengths(entity2, 3, 1, 0);
            AssertBufferLengths(entity3, 3, 1, 2);

            success = statsWorld.TryRemoveStatModifier(modifier6);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(12f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(15f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(20f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(0, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            AssertBufferLengths(entity2, 3, 1, 0);
            AssertBufferLengths(entity3, 3, 0, 1);
            
            success = statsWorld.TryAddStatBaseValue(statHandle2C, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(12f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(15f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(11f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(21f));
            Assert.AreEqual(1, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(0, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat3BModifiersCount);
            Assert.AreEqual(1, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            AssertBufferLengths(entity2, 3, 1, 0);
            AssertBufferLengths(entity3, 3, 0, 1);

            success = statsWorld.TryRemoveStatModifier(modifier7);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(12f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(15f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(11f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(0, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(0, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat3BModifiersCount);
            Assert.AreEqual(0, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            AssertBufferLengths(entity2, 3, 0, 0);
            AssertBufferLengths(entity3, 3, 0, 0);
            
            success = statsWorld.TryAddStatBaseValue(statHandle2C, 1f); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsWorld, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsWorld, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(12f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(15f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            Assert.IsTrue(stat2C.BaseValue.IsRoughlyEqual(12f));
            Assert.IsTrue(stat2C.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(0, stat2CModifiersCount);
            Assert.AreEqual(0, stat2CObserversCount);
            Assert.IsTrue(stat3A.BaseValue.IsRoughlyEqual(19f));
            Assert.IsTrue(stat3A.Value.IsRoughlyEqual(19f));
            Assert.AreEqual(0, stat3AModifiersCount);
            Assert.AreEqual(0, stat3AObserversCount);
            Assert.IsTrue(stat3B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3B.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat3BModifiersCount);
            Assert.AreEqual(0, stat3BObserversCount);
            AssertBufferLengths(entity1, 3, 0, 0);
            AssertBufferLengths(entity2, 3, 0, 0);
            AssertBufferLengths(entity3, 3, 0, 0);
        }

        [Test]
        public void ModifierAddRemoveCases()
        {
            bool success = false;
            StatsWorld<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorld = CreateStatsWorld();

            Entity entity1 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle1A, out StatHandle statHandle1B,
                out StatHandle statHandle1C);
            Entity entity2 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle2A, out StatHandle statHandle2B,
                out StatHandle statHandle2C);
            Entity entity3 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle3A, out StatHandle statHandle3B,
                out StatHandle statHandle3C);
            
            UpdateStatsWorld(ref statsWorld);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out Stat stat1A,
                out int stat1AModifiersCount, out int stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out Stat stat2A,
                out int stat2AModifiersCount, out int stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out Stat stat3A,
                out int stat3AModifiersCount, out int stat3AObserversCount);

            // -------------------------------------------------
            // Add modifier
            // -------------------------------------------------
            
            // Null entity
            
            // No StatsOwner buffer
            
            // No Stats buffer
            
            // No modifiers buffer
            
            // Invalid StatHandle (entity)
            
            // Invalid StatHandle (index)
            
            // -------------------------------------------------
            // Remove modifier
            // -------------------------------------------------
            
            // Remove first in buffer
            
            // Remove middle
            
            // Remove last in buffer
            
            // Remove first for stat but not for buffer
            
            // Remove last for stat but not for buffer
            
            // RemoveAll
            
            // Remove same handle multiple times


            success = statsWorld.TryAddStatModifier(
                statHandle2A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 2f,
                },
                out StatModifierHandle modifier1);
            Assert.IsTrue(success);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsWorld, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsWorld, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsWorld, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
        }

        // [Test]
        // public void ComplexTree()
        // {
        //     /*
        //      *           1
        //      *         /   \
        //      *        2 --> 3
        //      *       /  \ /  \
        //      *      4    5    6
        //      *       \ / | \ /
        //      *        7 <-- 8
        //      *         \ | /
        //      *           9
        //      *           
        //      * (    A                       )
        //      * (   /   means B observes A   )
        //      * (  B  (bottom observes top)  )
        //      * 
        //      * ( A --> B means A observes B )
        //     */
        //
        //     Entity entity1 = CreateStatsEntity(true, true, true);
        //     Entity entity2 = CreateStatsEntity(true, true, true);
        //     Entity entity3 = CreateStatsEntity(true, true, true);
        //     Entity entity4 = CreateStatsEntity(true, true, true);
        //     Entity entity5 = CreateStatsEntity(true, true, true);
        //     Entity entity6 = CreateStatsEntity(true, true, true);
        //     Entity entity7 = CreateStatsEntity(true, true, true);
        //     Entity entity8 = CreateStatsEntity(true, true, true);
        //     Entity entity9 = CreateStatsEntity(true, true, true);
        //     AttributeReference attribute1 = new AttributeReference(entity1, (int)AttributeType.A);
        //     AttributeReference attribute2 = new AttributeReference(entity2, (int)AttributeType.A);
        //     AttributeReference attribute3 = new AttributeReference(entity3, (int)AttributeType.A);
        //     AttributeReference attribute4 = new AttributeReference(entity4, (int)AttributeType.A);
        //     AttributeReference attribute5 = new AttributeReference(entity5, (int)AttributeType.A);
        //     AttributeReference attribute6 = new AttributeReference(entity6, (int)AttributeType.A);
        //     AttributeReference attribute7 = new AttributeReference(entity7, (int)AttributeType.A);
        //     AttributeReference attribute8 = new AttributeReference(entity8, (int)AttributeType.A);
        //     AttributeReference attribute9 = new AttributeReference(entity9, (int)AttributeType.A);
        //
        //     ModifierReference modifier = default;
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //     attributeChanger.AddModifier(attribute2, AttributeModifier.Create_AddFromAttribute(attribute1), out modifier);
        //     attributeChanger.AddModifier(attribute2, AttributeModifier.Create_AddFromAttribute(attribute3), out modifier);
        //     attributeChanger.AddModifier(attribute3, AttributeModifier.Create_AddFromAttribute(attribute1), out modifier);
        //     attributeChanger.AddModifier(attribute4, AttributeModifier.Create_AddFromAttribute(attribute2), out modifier);
        //     attributeChanger.AddModifier(attribute5, AttributeModifier.Create_AddFromAttribute(attribute2), out modifier);
        //     attributeChanger.AddModifier(attribute5, AttributeModifier.Create_AddFromAttribute(attribute3), out modifier);
        //     attributeChanger.AddModifier(attribute6, AttributeModifier.Create_AddFromAttribute(attribute3), out modifier);
        //     attributeChanger.AddModifier(attribute7, AttributeModifier.Create_AddFromAttribute(attribute4), out modifier);
        //     attributeChanger.AddModifier(attribute7, AttributeModifier.Create_AddFromAttribute(attribute5), out modifier);
        //     attributeChanger.AddModifier(attribute8, AttributeModifier.Create_AddFromAttribute(attribute5), out modifier);
        //     attributeChanger.AddModifier(attribute8, AttributeModifier.Create_AddFromAttribute(attribute6), out modifier);
        //     attributeChanger.AddModifier(attribute8, AttributeModifier.Create_AddFromAttribute(attribute7), out modifier);
        //     attributeChanger.AddModifier(attribute9, AttributeModifier.Create_AddFromAttribute(attribute5), out modifier);
        //     attributeChanger.AddModifier(attribute9, AttributeModifier.Create_AddFromAttribute(attribute7), out modifier);
        //     attributeChanger.AddModifier(attribute9, AttributeModifier.Create_AddFromAttribute(attribute8), out modifier);
        //
        //     AttributeValues values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     AttributeValues values2 = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     AttributeValues values3 = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     AttributeValues values4 = EntityManager.GetComponentData<AttributeA>(entity4).Values;
        //     AttributeValues values5 = EntityManager.GetComponentData<AttributeA>(entity5).Values;
        //     AttributeValues values6 = EntityManager.GetComponentData<AttributeA>(entity6).Values;
        //     AttributeValues values7 = EntityManager.GetComponentData<AttributeA>(entity7).Values;
        //     AttributeValues values8 = EntityManager.GetComponentData<AttributeA>(entity8).Values;
        //     AttributeValues values9 = EntityManager.GetComponentData<AttributeA>(entity9).Values;
        //
        //     Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2.Value.IsRoughlyEqual(40f));
        //     Assert.IsTrue(values3.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3.Value.IsRoughlyEqual(20f));
        //     Assert.IsTrue(values4.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values4.Value.IsRoughlyEqual(50f));
        //     Assert.IsTrue(values5.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values5.Value.IsRoughlyEqual(70f));
        //     Assert.IsTrue(values6.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values6.Value.IsRoughlyEqual(30f));
        //     Assert.IsTrue(values7.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values7.Value.IsRoughlyEqual(130f));
        //     Assert.IsTrue(values8.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values8.Value.IsRoughlyEqual(240f));
        //     Assert.IsTrue(values9.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values9.Value.IsRoughlyEqual(450f));
        // }
        //
        // [Test]
        // public void SelfEntityObserver()
        // {
        //     // Create an attribute with a modifier that affects another attribute on itself
        //
        //     Entity entity1 = CreateStatsEntity(true, true, true);
        //     AttributeReference attribute1 = new AttributeReference(entity1, (int)AttributeType.A);
        //     AttributeReference attribute2 = new AttributeReference(entity1, (int)AttributeType.B);
        //
        //     ModifierReference modifier = default;
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //     attributeChanger.AddModifier(attribute1, AttributeModifier.Create_AddFromAttribute(attribute2), out modifier);
        //
        //     AttributeValues values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //
        //     Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1.Value.IsRoughlyEqual(20f));
        //     Assert.AreEqual(1, modifiers1.Length);
        //     Assert.AreEqual(1, observers1.Length);
        //
        //     attributeChanger.AddBaseValue(attribute2, 5f);
        //
        //     values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //
        //     Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1.Value.IsRoughlyEqual(25f));
        // }
        //
        // [Test]
        // public void SelfAttributeObserver()
        // {
        //     // Create an attribute with a modifier that affects itself
        //
        //     Entity entity1 = CreateStatsEntity(true, true, true);
        //     AttributeReference attribute1 = new AttributeReference(entity1, (int)AttributeType.A);
        //
        //     ModifierReference modifier = default;
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //     attributeChanger.AddModifier(attribute1, AttributeModifier.Create_AddFromAttribute(attribute1), out modifier);
        //
        //     AttributeValues values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //
        //     Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1.Value.IsRoughlyEqual(10f));
        //     Assert.AreEqual(0, modifiers1.Length);
        //     Assert.AreEqual(0, observers1.Length);
        //
        //     attributeChanger.AddBaseValue(attribute1, 5f);
        //
        //     values1 = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //
        //     Assert.IsTrue(values1.BaseValue.IsRoughlyEqual(15f));
        //     Assert.IsTrue(values1.Value.IsRoughlyEqual(15f));
        // }
        //
        // [Test]
        // public void ModifiersFiltering()
        // {
        //     // Create an attribute with modifiers affecting 3 different attributes on it.
        //     // Validate that only the modifiers on the specific attributes are taken into account.
        //
        //     Entity entity1 = CreateStatsEntity(true, true, true);
        //     Entity entity2 = CreateStatsEntity(true, true, true);
        //     Entity entity3 = CreateStatsEntity(true, true, true);
        //     Entity entity4 = CreateStatsEntity(true, true, true);
        //     AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
        //     AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
        //     AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
        //     AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
        //     AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);
        //     AttributeReference attribute4A = new AttributeReference(entity4, (int)AttributeType.A);
        //
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //
        //     attributeChanger.SetBaseValue(attribute1A, 1f);
        //     attributeChanger.SetBaseValue(attribute1B, 2f);
        //     attributeChanger.SetBaseValue(attribute1C, 3f);
        //     attributeChanger.SetBaseValue(attribute2A, 4f);
        //     attributeChanger.SetBaseValue(attribute3A, 5f);
        //     attributeChanger.SetBaseValue(attribute4A, 6f);
        //
        //     ModifierReference modifier = default;
        //     attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifier);
        //     attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute1C), out modifier);
        //     attributeChanger.AddModifier(attribute1B, AttributeModifier.Create_AddFromAttribute(attribute3A), out modifier);
        //     attributeChanger.AddModifier(attribute1C, AttributeModifier.Create_AddFromAttribute(attribute4A), out modifier);
        //
        //     AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     AttributeValues values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
        //     AttributeValues values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
        //
        //     Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(1f));
        //     Assert.IsTrue(values1A.Value.IsRoughlyEqual(14f));
        //     Assert.IsTrue(values1B.BaseValue.IsRoughlyEqual(2f));
        //     Assert.IsTrue(values1B.Value.IsRoughlyEqual(7f));
        //     Assert.IsTrue(values1C.BaseValue.IsRoughlyEqual(3f));
        //     Assert.IsTrue(values1C.Value.IsRoughlyEqual(9f));
        // }
        //
        // [Test]
        // public void ObserversFiltering()
        // {
        //     // Create an entity observed by 3 different attributes (each observing a different attribute on the entity).
        //     // Validate that only the specific observers are recalculated.
        //
        //     Entity entity1 = CreateStatsEntity(true, true, true);
        //     Entity entity2 = CreateStatsEntity(true, true, true);
        //     Entity entity3 = CreateStatsEntity(true, true, true);
        //     Entity entity4 = CreateStatsEntity(true, true, true);
        //     AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
        //     AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
        //     AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
        //     AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
        //     AttributeReference attribute2B = new AttributeReference(entity2, (int)AttributeType.B);
        //     AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);
        //     AttributeReference attribute4A = new AttributeReference(entity4, (int)AttributeType.A);
        //
        //     ModifierReference modifier = default;
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //     attributeChanger.AddModifier(attribute2A, AttributeModifier.Create_AddFromAttribute(attribute1A), out modifier);
        //     attributeChanger.AddModifier(attribute2B, AttributeModifier.Create_AddFromAttribute(attribute1B), out modifier);
        //     attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute1B), out modifier);
        //     attributeChanger.AddModifier(attribute4A, AttributeModifier.Create_AddFromAttribute(attribute1C), out modifier);
        //
        //     AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     AttributeValues values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
        //     AttributeValues values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     AttributeValues values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;
        //
        //     Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2A.Value.IsRoughlyEqual(20f));
        //     Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2B.Value.IsRoughlyEqual(20f));
        //     Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3A.Value.IsRoughlyEqual(20f));
        //     Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values4A.Value.IsRoughlyEqual(20f));
        //
        //     // Set 2B to 0 and check that it didn't get recalculated when 1A changes (because it doesn't observe it)
        //     values2B.__internal__value = 0f;
        //     EntityManager.SetComponentData(entity2, new AttributeB { Values = values2B });
        //     attributeChanger.AddBaseValue(attribute1A, 1f);
        //
        //     values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
        //     values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;
        //
        //     Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2A.Value.IsRoughlyEqual(21f));
        //     Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2B.Value.IsRoughlyEqual(0f));
        //     Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3A.Value.IsRoughlyEqual(20f));
        //     Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values4A.Value.IsRoughlyEqual(20f));
        //
        //     // Set 2A to 0 and check that it didn't get recalculated when 1B changes (because it doesn't observe it)
        //     values2A.__internal__value = 0f;
        //     EntityManager.SetComponentData(entity2, new AttributeA { Values = values2A });
        //     attributeChanger.AddBaseValue(attribute1B, 1f);
        //
        //     values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
        //     values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;
        //
        //     Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2A.Value.IsRoughlyEqual(0f));
        //     Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2B.Value.IsRoughlyEqual(21f));
        //     Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3A.Value.IsRoughlyEqual(21f));
        //     Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values4A.Value.IsRoughlyEqual(20f));
        //
        //     values2A.__internal__value = 0f;
        //     EntityManager.SetComponentData(entity2, new AttributeA { Values = values2A });
        //     values2B.__internal__value = 0f;
        //     EntityManager.SetComponentData(entity2, new AttributeB { Values = values2B });
        //     values3A.__internal__value = 0f;
        //     EntityManager.SetComponentData(entity3, new AttributeA { Values = values3A });
        //     attributeChanger.AddBaseValue(attribute1C, 1f);
        //
        //     values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
        //     values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;
        //
        //     Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2A.Value.IsRoughlyEqual(0f));
        //     Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2B.Value.IsRoughlyEqual(0f));
        //     Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3A.Value.IsRoughlyEqual(0f));
        //     Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values4A.Value.IsRoughlyEqual(21f));
        // }
        //
        // [Test]
        // public void SelfRemoveModifierAndObserver()
        // {
        //     Entity entity1 = CreateStatsEntity(true, true, true);
        //     Entity entity2 = CreateStatsEntity(true, true, true);
        //     Entity entity3 = CreateStatsEntity(true, true, true);
        //     AttributeReference attributeA = new AttributeReference(entity1, (int)AttributeType.A);
        //     AttributeReference attributeB = new AttributeReference(entity2, (int)AttributeType.B);
        //     AttributeReference attributeC = new AttributeReference(entity3, (int)AttributeType.C);
        //
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //
        //     DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //     DynamicBuffer<AttributeModifier> modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
        //     DynamicBuffer<AttributeObserver> observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
        //     DynamicBuffer<AttributeModifier> modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
        //     DynamicBuffer<AttributeObserver> observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);
        //
        //     AttributeValues valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //
        //     attributeChanger.AddModifier(attributeA, AttributeModifier.Create_AddFromAttribute(attributeB), out ModifierReference modifier1);
        //     attributeChanger.AddModifier(attributeC, AttributeModifier.Create_AddFromAttribute(attributeA), out ModifierReference modifier2);
        //
        //     valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(valuesA.Value.IsRoughlyEqual(20f));
        //     Assert.AreEqual(1, modifiers1.Length);
        //     Assert.AreEqual(1, observers1.Length);
        //     Assert.AreEqual(0, modifiers2.Length);
        //     Assert.AreEqual(1, observers2.Length);
        //     Assert.AreEqual(1, modifiers3.Length);
        //     Assert.AreEqual(0, observers3.Length);
        //
        //     EntityManager.DestroyEntity(entity2);
        //     EntityManager.DestroyEntity(entity3);
        //
        //     modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //
        //     attributeChanger = CreateStatsWorld();
        //     attributeChanger.RecalculateAttributeAndAllObservers(attributeA);
        //
        //     valuesA = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     Assert.IsTrue(valuesA.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(valuesA.Value.IsRoughlyEqual(10f));
        //     Assert.AreEqual(0, modifiers1.Length);
        //     Assert.AreEqual(0, observers1.Length);
        // }
        //
        //
        // [Test]
        // public void DestroyObservedAttribute()
        // {
        //     Entity entity1 = CreateStatsEntity(true, true, true);
        //     Entity entity2 = CreateStatsEntity(true, true, true);
        //     Entity entity3 = CreateStatsEntity(true, true, true);
        //     AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
        //     AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
        //     AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
        //     AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
        //     AttributeReference attribute2B = new AttributeReference(entity2, (int)AttributeType.B);
        //     AttributeReference attribute2C = new AttributeReference(entity2, (int)AttributeType.C);
        //     AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);
        //     AttributeReference attribute3B = new AttributeReference(entity3, (int)AttributeType.B);
        //     AttributeReference attribute3C = new AttributeReference(entity3, (int)AttributeType.C);
        //
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //
        //     AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     AttributeValues values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
        //     AttributeValues values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
        //     AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     AttributeValues values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
        //     AttributeValues values2C = EntityManager.GetComponentData<AttributeC>(entity2).Values;
        //     AttributeValues values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     AttributeValues values3B = EntityManager.GetComponentData<AttributeB>(entity3).Values;
        //     AttributeValues values3C = EntityManager.GetComponentData<AttributeC>(entity3).Values;
        //     DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //     DynamicBuffer<AttributeModifier> modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
        //     DynamicBuffer<AttributeObserver> observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
        //     DynamicBuffer<AttributeModifier> modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
        //     DynamicBuffer<AttributeObserver> observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);
        //
        //     ModifierReference modifierReference = default;
        //     attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifierReference);
        //     attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifierReference);
        //     attributeChanger.AddModifier(attribute1B, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifierReference);
        //     attributeChanger.AddModifier(attribute1C, AttributeModifier.Create_AddFromAttribute(attribute2B), out modifierReference);
        //     attributeChanger.AddModifier(attribute2A, AttributeModifier.Create_AddFromAttribute(attribute2B), out modifierReference);
        //     attributeChanger.AddModifier(attribute2C, AttributeModifier.Create_AddFromAttribute(attribute1B), out modifierReference);
        //     attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute2C), out modifierReference);
        //     attributeChanger.AddModifier(attribute2A, AttributeModifier.Create_AddFromAttribute(attribute3B), out modifierReference);
        //     attributeChanger.AddModifier(attribute3C, AttributeModifier.Create_AddFromAttribute(attribute1A), out modifierReference);
        //
        //     values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
        //     values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
        //     values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     values2B = EntityManager.GetComponentData<AttributeB>(entity2).Values;
        //     values2C = EntityManager.GetComponentData<AttributeC>(entity2).Values;
        //     values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     values3B = EntityManager.GetComponentData<AttributeB>(entity3).Values;
        //     values3C = EntityManager.GetComponentData<AttributeC>(entity3).Values;
        //     modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //     modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
        //     observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
        //     modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
        //     observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);
        //
        //     Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1A.Value.IsRoughlyEqual(70f));
        //     Assert.IsTrue(values1B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1B.Value.IsRoughlyEqual(40f));
        //     Assert.IsTrue(values1C.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1C.Value.IsRoughlyEqual(20f));
        //     Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2A.Value.IsRoughlyEqual(30f));
        //     Assert.IsTrue(values2B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2B.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2C.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2C.Value.IsRoughlyEqual(50f));
        //     Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3A.Value.IsRoughlyEqual(60f));
        //     Assert.IsTrue(values3B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3B.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3C.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3C.Value.IsRoughlyEqual(80f));
        //     Assert.AreEqual(4, modifiers1.Length);
        //     Assert.AreEqual(2, observers1.Length);
        //     Assert.AreEqual(3, modifiers2.Length);
        //     Assert.AreEqual(5, observers2.Length);
        //     Assert.AreEqual(2, modifiers3.Length);
        //     Assert.AreEqual(1, observers3.Length);
        //
        //     // Destroy entity and prepare attributes owner for destruction
        //     {
        //         EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        //
        //         Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out DynamicBuffer<AttributeCommand> attributeCommands);
        //         MakeTestEntity(ref ecb, commandsEntity);
        //
        //         AttributeUtilities.NotifyAttributesOwnerDestruction(entity2, ref observers2, ref attributeCommands);
        //          
        //         ecb.DestroyEntity(entity2);
        //         ecb.Playback(EntityManager);
        //         ecb.Dispose();
        //
        //         // After ecb playback, process commands to recalculate
        //         UpdateProcessCommandsSystem();
        //     }
        //
        //     values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     values1B = EntityManager.GetComponentData<AttributeB>(entity1).Values;
        //     values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
        //     values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     values3B = EntityManager.GetComponentData<AttributeB>(entity3).Values;
        //     values3C = EntityManager.GetComponentData<AttributeC>(entity3).Values;
        //     modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //     modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
        //     observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);
        //
        //     Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1A.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1B.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1C.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1C.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3A.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3B.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3B.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3C.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3C.Value.IsRoughlyEqual(20f));
        //     Assert.AreEqual(0, modifiers1.Length);
        //     Assert.AreEqual(1, observers1.Length);
        //     Assert.AreEqual(1, modifiers3.Length);
        //     Assert.AreEqual(1, observers3.Length);
        // }
        //
        // [Test]
        // public void InfiniteObserversLoopPrevention()
        // {
        //     //                                             6->\
        //     // Try to create an infinite observers loop: 1->2->3->4
        //     //                                                  \->1  (this one would cause the infinite loop)
        //     //                                                   \->5
        //     // ( A->B means "A observes B" )
        //
        //     ModifierReference tmpModifierID = default;
        //
        //     Entity entity1 = CreateStatsEntity(true, false, false);
        //     Entity entity2 = CreateStatsEntity(true, false, false);
        //     Entity entity3 = CreateStatsEntity(true, false, false);
        //     Entity entity4 = CreateStatsEntity(true, false, false);
        //     Entity entity5 = CreateStatsEntity(true, false, false);
        //     Entity entity6 = CreateStatsEntity(true, false, false);
        //
        //     AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
        //     AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
        //     AttributeReference attribute3A = new AttributeReference(entity3, (int)AttributeType.A);
        //     AttributeReference attribute4A = new AttributeReference(entity4, (int)AttributeType.A);
        //     AttributeReference attribute5A = new AttributeReference(entity5, (int)AttributeType.A);
        //     AttributeReference attribute6A = new AttributeReference(entity6, (int)AttributeType.A);
        //
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //
        //     attributeChanger.AddModifier(attribute6A, AttributeModifier.Create_AddFromAttribute(attribute3A), out tmpModifierID);
        //     attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute2A), out tmpModifierID);
        //     attributeChanger.AddModifier(attribute2A, AttributeModifier.Create_AddFromAttribute(attribute3A), out tmpModifierID);
        //     attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute4A), out tmpModifierID);
        //     attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute1A), out tmpModifierID);
        //     attributeChanger.AddModifier(attribute3A, AttributeModifier.Create_AddFromAttribute(attribute5A), out tmpModifierID);
        //
        //     AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     AttributeValues values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     AttributeValues values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;
        //     AttributeValues values5A = EntityManager.GetComponentData<AttributeA>(entity5).Values;
        //     DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     DynamicBuffer<AttributeModifier> modifiers2 = EntityManager.GetBuffer<AttributeModifier>(entity2);
        //     DynamicBuffer<AttributeModifier> modifiers3 = EntityManager.GetBuffer<AttributeModifier>(entity3);
        //     DynamicBuffer<AttributeModifier> modifiers4 = EntityManager.GetBuffer<AttributeModifier>(entity4);
        //     DynamicBuffer<AttributeModifier> modifiers5 = EntityManager.GetBuffer<AttributeModifier>(entity5);
        //     DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //     DynamicBuffer<AttributeObserver> observers2 = EntityManager.GetBuffer<AttributeObserver>(entity2);
        //     DynamicBuffer<AttributeObserver> observers3 = EntityManager.GetBuffer<AttributeObserver>(entity3);
        //     DynamicBuffer<AttributeObserver> observers4 = EntityManager.GetBuffer<AttributeObserver>(entity4);
        //     DynamicBuffer<AttributeObserver> observers5 = EntityManager.GetBuffer<AttributeObserver>(entity5);
        //
        //     // Check that the modifier making 3 observe 1 wasn't added, because it would've caused a loop
        //     Assert.AreEqual(1, modifiers1.Length);
        //     Assert.AreEqual(1, modifiers2.Length);
        //     Assert.AreEqual(2, modifiers3.Length); // 2 successful, 1 unsuccessful
        //     Assert.AreEqual(0, modifiers4.Length);
        //     Assert.AreEqual(0, modifiers5.Length);
        //     Assert.AreEqual(0, observers1.Length);
        //     Assert.AreEqual(1, observers2.Length);
        //     Assert.AreEqual(2, observers3.Length);
        //     Assert.AreEqual(1, observers4.Length);
        //     Assert.AreEqual(1, observers5.Length);
        //
        //     Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1A.Value.IsRoughlyEqual(50f));
        //     Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2A.Value.IsRoughlyEqual(40f));
        //     Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values3A.Value.IsRoughlyEqual(30f));
        //     Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values4A.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values5A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values5A.Value.IsRoughlyEqual(10f));
        //
        //     attributeChanger.AddBaseValue(attribute3A, 1f);
        //
        //     values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //     values3A = EntityManager.GetComponentData<AttributeA>(entity3).Values;
        //     values4A = EntityManager.GetComponentData<AttributeA>(entity4).Values;
        //     values5A = EntityManager.GetComponentData<AttributeA>(entity5).Values;
        //
        //     Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values1A.Value.IsRoughlyEqual(51f));
        //     Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values2A.Value.IsRoughlyEqual(41f));
        //     Assert.IsTrue(values3A.BaseValue.IsRoughlyEqual(11f));
        //     Assert.IsTrue(values3A.Value.IsRoughlyEqual(31f));
        //     Assert.IsTrue(values4A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values4A.Value.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values5A.BaseValue.IsRoughlyEqual(10f));
        //     Assert.IsTrue(values5A.Value.IsRoughlyEqual(10f));
        // }
        //
        // [Test]
        // public void ValidateLoopDetectionTakesObservedTypeIntoAccount()
        // {
        //     // - make 1A observe 1C
        //     // - make 1C observe 2A
        //     // Before making 1C an observer of 2A, LoopDetection will check if 2A already observes 1C to prevent a loop.
        //     // It will look at the observers of 1C and find 1A, so it does LoopDetection of if 2A already observes 1A.
        //     // It will look at the observers of 1A. If it didn't take observed attribute type into account, it would find
        //     // 1A as an observer to check again, creating an infinite loop of checking id 2A observes 1A.
        //     // So make sure it knows that 1A isn't an observer of 1A (just because it's an observer on the same entity as 1C).
        //
        //     Entity entity1 = CreateStatsEntity(true, true, true);
        //     Entity entity2 = CreateStatsEntity(true, true, true);
        //     AttributeReference attribute1A = new AttributeReference(entity1, (int)AttributeType.A);
        //     AttributeReference attribute1B = new AttributeReference(entity1, (int)AttributeType.B);
        //     AttributeReference attribute1C = new AttributeReference(entity1, (int)AttributeType.C);
        //     AttributeReference attribute2A = new AttributeReference(entity2, (int)AttributeType.A);
        //
        //     AttributeChanger attributeChanger = CreateStatsWorld();
        //
        //     attributeChanger.SetBaseValue(attribute1A, 1f);
        //     attributeChanger.SetBaseValue(attribute1B, 2f);
        //     attributeChanger.SetBaseValue(attribute1C, 3f);
        //     attributeChanger.SetBaseValue(attribute2A, 4f);
        //
        //     ModifierReference modifier = default;
        //     attributeChanger.AddModifier(attribute1A, AttributeModifier.Create_AddFromAttribute(attribute1C), out modifier);
        //
        //     AttributeValues values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     DynamicBuffer<AttributeModifier> modifiers1 = EntityManager.GetBuffer<AttributeModifier>(entity1);
        //     DynamicBuffer<AttributeObserver> observers1 = EntityManager.GetBuffer<AttributeObserver>(entity1);
        //
        //     Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(1f));
        //     Assert.IsTrue(values1A.Value.IsRoughlyEqual(4f));
        //     Assert.AreEqual(1, modifiers1.Length);
        //     Assert.AreEqual(1, observers1.Length);
        //
        //     attributeChanger.AddModifier(attribute1C, AttributeModifier.Create_AddFromAttribute(attribute2A), out modifier);
        //
        //     values1A = EntityManager.GetComponentData<AttributeA>(entity1).Values;
        //     AttributeValues values1C = EntityManager.GetComponentData<AttributeC>(entity1).Values;
        //     AttributeValues values2A = EntityManager.GetComponentData<AttributeA>(entity2).Values;
        //
        //     Assert.IsTrue(values1A.BaseValue.IsRoughlyEqual(1f));
        //     Assert.IsTrue(values1A.Value.IsRoughlyEqual(8f));
        //     Assert.IsTrue(values1C.BaseValue.IsRoughlyEqual(3f));
        //     Assert.IsTrue(values1C.Value.IsRoughlyEqual(7f));
        //     Assert.IsTrue(values2A.BaseValue.IsRoughlyEqual(4f));
        //     Assert.IsTrue(values2A.Value.IsRoughlyEqual(4f));
        // }
    }
}