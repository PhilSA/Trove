using System;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Trove.Stats;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

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
    public struct StatsTestsStatModifier : IBufferElementData, IStatsModifier<StatsTestsStatModifier.Stack>
    {
        public enum Type
        {
            Add,
            AddFromStat,
            AddFromTwoStats,
            AddFromTwoStatsMax,
            AddInt,
            SelfRemoveWhenStatAbove1000,
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

        public uint Id { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObservedStatsToList(ref NativeList<StatHandle> observedStatHandles)
        {
            switch (ModifierType)
            {
                case (Type.AddFromStat):
                case (Type.SelfRemoveWhenStatAbove1000):
                    observedStatHandles.Add(StatHandleA);
                    break;
                case (Type.AddFromTwoStats):
                case (Type.AddFromTwoStatsMax):
                    observedStatHandles.Add(StatHandleA);
                    observedStatHandles.Add(StatHandleB);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(ref StatsReader statsReader, ref Stack stack, out bool shouldProduceModifierTriggerEvent)
        {
            shouldProduceModifierTriggerEvent = true;
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
                        return;
                    }

                    MustRemove = true;
                    break;
                }
                case (Type.AddFromTwoStats):
                {
                    if (statsReader.TryGetStat(StatHandleA, out float statAValue, out float _) &&
                        statsReader.TryGetStat(StatHandleB, out float statBValue, out float _))
                    {
                        stack.Add += statAValue;
                        stack.Add += statBValue;
                        return;
                    }
                    
                    MustRemove = true;
                    break;
                }
                case (Type.AddFromTwoStatsMax):
                {
                    if (statsReader.TryGetStat(StatHandleA, out float statAValue, out float _) &&
                        statsReader.TryGetStat(StatHandleB, out float statBValue, out float _))
                    {
                        stack.Add += math.max(statAValue, statBValue);
                        return;
                    }
                    
                    MustRemove = true;
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
                case (Type.SelfRemoveWhenStatAbove1000):
                {
                    if (statsReader.TryGetStat(StatHandleA, out float statAValue, out float _))
                    {
                        if (statAValue > 1000f)
                        {
                            MustRemove = true;
                        }
                        return;
                    }

                    MustRemove = true; 
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
            EntityManager.CompleteAllTrackedJobs();
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

        public StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> CreateStatsWorld()
        {
            ref SystemState state = ref World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().CheckedStateRef;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = 
                new StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(ref state);
            statsAccessor.Update(ref state); 
            return statsAccessor;
        }

        public void UpdateStatsWorld(ref StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor)
        {
            ref SystemState state = ref World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().CheckedStateRef;
            statsAccessor.Update(ref state);
        }

        public Entity CreateStatsEntity(
            int id,
            float baseValue,
            bool produceChangeEvents,
            out StatHandle statHandleA,
            out StatHandle statHandleB,
            out StatHandle statHandleC)
        {
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            Entity entity = CreateTestEntity(id);
            StatsUtilities.AddStatsComponents<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(entity, EntityManager);
            
            UpdateStatsWorld(ref statsAccessor);
            
            bool success = true;
            success &= statsAccessor.TryCreateStat(entity, baseValue, produceChangeEvents, out statHandleA);
            success &= statsAccessor.TryCreateStat(entity, baseValue, produceChangeEvents, out statHandleB);
            success &= statsAccessor.TryCreateStat(entity, baseValue, produceChangeEvents, out statHandleC);
            Assert.IsTrue(success);
            
            return entity;
        }

        private bool GetStatAndModifiersAndObserversCount(StatHandle statHandle, ref StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor, out Stat stat, out int modifiersCount,
            out int observersCount)
        {
            bool success = true;
            success &= StatsUtilities.TryGetStat(statHandle, in statsAccessor._statsLookup, out stat);
            success &= statsAccessor.TryGetStatModifiersCount(statHandle, out modifiersCount);
            success &= statsAccessor.TryGetObserversCount(statHandle, out observersCount);
            return success;
        }

        private void AssertBufferValidElementLengths(Entity entity, int statsLength, int modifiersLength, int observersLength)
        {
            if (EntityManager.HasBuffer<Stat>(entity))
            {
                Assert.AreEqual(statsLength, EntityManager.GetBuffer<Stat>(entity).Length);
            }
            else
            {
                Assert.AreEqual(0, statsLength);
            }

            if (EntityManager.HasBuffer<StatsTestsStatModifier>(entity))
            {
                DynamicBuffer<StatsTestsStatModifier> buffer = EntityManager.GetBuffer<StatsTestsStatModifier>(entity);
                Assert.AreEqual(modifiersLength, buffer.Length);
            }
            else
            {
                Assert.AreEqual(0, modifiersLength);
            }

            if (EntityManager.HasBuffer<StatObserver>(entity))
            {
                DynamicBuffer<StatObserver> buffer = EntityManager.GetBuffer<StatObserver>(entity);
                Assert.AreEqual(observersLength, buffer.Length);
            }
            else
            {
                Assert.AreEqual(0, observersLength);
            }
        }

        [Test]
        public void BasicStatOperations()
        {
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);

            Entity entity1 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle1A, out StatHandle statHandle1B,
                out StatHandle statHandle1C);
            
            UpdateStatsWorld(ref statsAccessor);
            
            success = GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out Stat stat1A, out int stat1AModifiersCount, out int stat1AObserversCount);
            Assert.IsTrue(success);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);

            success = statsAccessor.TrySetStatBaseValue(statHandle1A, 2f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(2f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(2f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);

            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(3f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);

            success = statsAccessor.TryMultiplyStatBaseValue(statHandle1A, 2f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(6f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(6f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);

            success = statsAccessor.TryMultiplyStatBaseValue(statHandle1A, -2f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(-12f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(-12f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);

            success = statsAccessor.TryAddStatBaseValue(statHandle1A, -4f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A, out stat1AModifiersCount, out stat1AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(-16f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(-16f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            
            statsWorldStorage.Dispose();
        }

        [Test]
        public void SameEntityStatModifiers()
        {
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);

            Entity entity1 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle1A, out StatHandle statHandle1B,
                out StatHandle statHandle1C);
            
            UpdateStatsWorld(ref statsAccessor);

            success = statsAccessor.TrySetStatBaseValue(statHandle1A, 5f, ref statsWorldStorage); 
            Assert.IsTrue(success);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out Stat stat1A,
                out int stat1AModifiersCount, out int stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out Stat stat1B,
                out int stat1BModifiersCount, out int stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out Stat stat1C,
                out int stat1CModifiersCount, out int stat1CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);

            // -------------------------------------------------
            // Add modifiers
            // -------------------------------------------------
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 2f,
                },
                out StatModifierHandle modifier1, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            Assert.IsTrue(modifier1.AffectedStatHandle == statHandle1B);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(1, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 1, 0);

            success = statsAccessor.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier2, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(17f));
            Assert.AreEqual(2, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 2, 1);

            success = statsAccessor.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier3, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(22f));
            Assert.AreEqual(3, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 3, 2);

            success = statsAccessor.TryAddStatModifier(
                statHandle1C,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1B,
                },
                out StatModifierHandle modifier4, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 4, 3);

            success = statsAccessor.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 4f,
                },
                out StatModifierHandle modifier5, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 5, 3);

            // -------------------------------------------------
            // Change values
            // -------------------------------------------------
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 2f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 5, 3);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1B, 5f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 5, 3);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1C, 9f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 5, 3);
            
            // -------------------------------------------------
            // Remove modifiers and change values
            // -------------------------------------------------

            success = statsAccessor.TryRemoveStatModifier(modifier2, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 4, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 4, 2);

            success = statsAccessor.TryRemoveStatModifier(modifier5, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 3, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 3, 2);

            success = statsAccessor.TryRemoveStatModifier(modifier4, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 2, 1);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 2, 1);

            success = statsAccessor.TryRemoveStatModifier(modifier1, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 1);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 1);

            success = statsAccessor.TryRemoveStatModifier(modifier3, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            
            statsWorldStorage.Dispose();
        }

        [Test]
        public void CrossEntityStatModifiers()
        {
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);

            Entity entity1 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle1A, out StatHandle statHandle1B,
                out StatHandle statHandle1C);
            Entity entity2 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle2A, out StatHandle statHandle2B,
                out StatHandle statHandle2C);
            Entity entity3 = CreateStatsEntity(0, 10f, true, out StatHandle statHandle3A, out StatHandle statHandle3B,
                out StatHandle statHandle3C);
            
            UpdateStatsWorld(ref statsAccessor);

            success = statsAccessor.TrySetStatBaseValue(statHandle1A, 5f, ref statsWorldStorage); 
            Assert.IsTrue(success);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out Stat stat1A,
                out int stat1AModifiersCount, out int stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out Stat stat1B,
                out int stat1BModifiersCount, out int stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out Stat stat1C,
                out int stat1CModifiersCount, out int stat1CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out Stat stat2A,
                out int stat2AModifiersCount, out int stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2B, ref statsAccessor, out Stat stat2B,
                out int stat2BModifiersCount, out int stat2BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out Stat stat2C,
                out int stat2CModifiersCount, out int stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out Stat stat3A,
                out int stat3AModifiersCount, out int stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out Stat stat3B,
                out int stat3BModifiersCount, out int stat3BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3C, ref statsAccessor, out Stat stat3C,
                out int stat3CModifiersCount, out int stat3CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 3, 0, 0);
            AssertBufferValidElementLengths(entity3, 3, 0, 0);

            // -------------------------------------------------
            // Add modifiers
            // -------------------------------------------------
            
            success = statsAccessor.TryAddStatModifier(
                statHandle2A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 2f,
                },
                out StatModifierHandle modifier1, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            Assert.IsTrue(modifier1.AffectedStatHandle == statHandle2A);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(12f));
            Assert.AreEqual(1, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 3, 1, 0);
            AssertBufferValidElementLengths(entity3, 3, 0, 0);

            success = statsAccessor.TryAddStatModifier(
                statHandle2A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier2, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(1, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(17f));
            Assert.AreEqual(2, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 1);
            AssertBufferValidElementLengths(entity2, 3, 2, 0);
            AssertBufferValidElementLengths(entity3, 3, 0, 0);

            success = statsAccessor.TryAddStatModifier(
                statHandle2A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier3, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(5f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(22f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 2);
            AssertBufferValidElementLengths(entity2, 3, 3, 0);
            AssertBufferValidElementLengths(entity3, 3, 0, 0);

            success = statsAccessor.TryAddStatModifier(
                statHandle3A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle2A,
                },
                out StatModifierHandle modifier4, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 2);
            AssertBufferValidElementLengths(entity2, 3, 3, 1);
            AssertBufferValidElementLengths(entity3, 3, 1, 0);

            success = statsAccessor.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.Add,
                    ValueA = 4f,
                },
                out StatModifierHandle modifier5, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 2);
            AssertBufferValidElementLengths(entity2, 3, 3, 1);
            AssertBufferValidElementLengths(entity3, 3, 1, 0);

            success = statsAccessor.TryAddStatModifier(
                statHandle3B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3A,
                },
                out StatModifierHandle modifier6, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 2);
            AssertBufferValidElementLengths(entity2, 3, 3, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 1);

            success = statsAccessor.TryAddStatModifier(
                statHandle2C,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3B,
                },
                out StatModifierHandle modifier7, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 2);
            AssertBufferValidElementLengths(entity2, 3, 4, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 2);

            // -------------------------------------------------
            // Change values
            // -------------------------------------------------
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 2f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 2);
            AssertBufferValidElementLengths(entity2, 3, 4, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle2A, 5f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 2);
            AssertBufferValidElementLengths(entity2, 3, 4, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle3A, 9f, ref statsWorldStorage); 
            Assert.IsTrue(success);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2B, ref statsAccessor, out stat2B,
                out stat2BModifiersCount, out stat2BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
                out stat3BModifiersCount, out stat3BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3C, ref statsAccessor, out stat3C,
                out stat3CModifiersCount, out stat3CObserversCount);

            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(11f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(2, stat1AObserversCount);
            Assert.IsTrue(stat1B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1B.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            Assert.IsTrue(stat1C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1C.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1CModifiersCount);
            Assert.AreEqual(0, stat1CObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(15f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(39f));
            Assert.AreEqual(3, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            Assert.IsTrue(stat2B.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2B.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat2BModifiersCount);
            Assert.AreEqual(0, stat2BObserversCount);
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
            Assert.IsTrue(stat3C.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat3C.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat3CModifiersCount);
            Assert.AreEqual(0, stat3CObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 1, 2);
            AssertBufferValidElementLengths(entity2, 3, 4, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 2);
            
            // -------------------------------------------------
            // Remove modifiers and change values
            // -------------------------------------------------

            success = statsAccessor.TryRemoveStatModifier(modifier2, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 1);
            AssertBufferValidElementLengths(entity2, 3, 3, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 1, 1);
            AssertBufferValidElementLengths(entity2, 3, 3, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 2);

            success = statsAccessor.TryRemoveStatModifier(modifier5, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 1);
            AssertBufferValidElementLengths(entity2, 3, 3, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 1);
            AssertBufferValidElementLengths(entity2, 3, 3, 1);
            AssertBufferValidElementLengths(entity3, 3, 2, 2);

            success = statsAccessor.TryRemoveStatModifier(modifier4, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 1);
            AssertBufferValidElementLengths(entity2, 3, 3, 0);
            AssertBufferValidElementLengths(entity3, 3, 1, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 1);
            AssertBufferValidElementLengths(entity2, 3, 3, 0);
            AssertBufferValidElementLengths(entity3, 3, 1, 2);

            success = statsAccessor.TryRemoveStatModifier(modifier1, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 1);
            AssertBufferValidElementLengths(entity2, 3, 2, 0);
            AssertBufferValidElementLengths(entity3, 3, 1, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 1);
            AssertBufferValidElementLengths(entity2, 3, 2, 0);
            AssertBufferValidElementLengths(entity3, 3, 1, 2);

            success = statsAccessor.TryRemoveStatModifier(modifier3, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 3, 1, 0);
            AssertBufferValidElementLengths(entity3, 3, 1, 2);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle1A, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 3, 1, 0);
            AssertBufferValidElementLengths(entity3, 3, 1, 2);

            success = statsAccessor.TryRemoveStatModifier(modifier6, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 3, 1, 0);
            AssertBufferValidElementLengths(entity3, 3, 0, 1);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle2C, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 3, 1, 0);
            AssertBufferValidElementLengths(entity3, 3, 0, 1);

            success = statsAccessor.TryRemoveStatModifier(modifier7, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 3, 0, 0);
            AssertBufferValidElementLengths(entity3, 3, 0, 0);
            
            success = statsAccessor.TryAddStatBaseValue(statHandle2C, 1f, ref statsWorldStorage); 
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2C, ref statsAccessor, out stat2C,
                out stat2CModifiersCount, out stat2CObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3A, ref statsAccessor, out stat3A,
                out stat3AModifiersCount, out stat3AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3B, ref statsAccessor, out stat3B,
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
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 3, 0, 0);
            AssertBufferValidElementLengths(entity3, 3, 0, 0);
            
            statsWorldStorage.Dispose();
        }

        [Test]
        public void InvalidStatOperations()
        {
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);

            // Invalid stats entity cases
            {
                Entity entity = CreateTestEntity(0);
                UpdateStatsWorld(ref statsAccessor);
                
                StatHandle invalidEntityStatHandle = new StatHandle(entity, 0);
                
                success = statsAccessor.TryCreateStat(entity, 10f, false, out _);
                Assert.IsFalse(success);
                
                success = statsAccessor.TryGetStat(invalidEntityStatHandle, out _, out _);
                Assert.IsFalse(success);
                
                success = statsAccessor.TryAddStatBaseValue(invalidEntityStatHandle, 1f, ref statsWorldStorage);
                Assert.IsFalse(success);
                
                success = statsAccessor.TryAddStatModifier(
                    invalidEntityStatHandle,
                    new StatsTestsStatModifier
                    {
                        ModifierType = StatsTestsStatModifier.Type.Add,
                        ValueA = 1f,
                    },
                    out StatModifierHandle modifier1, 
                    ref statsWorldStorage);
                Assert.IsFalse(success);
                
                success = statsAccessor.TryRemoveStatModifier(modifier1, ref statsWorldStorage);
                Assert.IsFalse(success);
            
                statsWorldStorage.Dispose();
            }
            
            // Valid stats entity cases
            {
                Entity entity = CreateStatsEntity(0, 10f, true, out StatHandle statHandleA,
                    out StatHandle statHandleB, out StatHandle statHandleC);
                
                UpdateStatsWorld(ref statsAccessor);
                
                // Invalid stats handle
                {
                    StatHandle statHandleInvalid = statHandleA;
                    statHandleInvalid.Index = 99999;
                    
                    success = statsAccessor.TryGetStat(statHandleInvalid, out _, out _);
                    Assert.IsFalse(success);
                    
                    success = statsAccessor.TryAddStatBaseValue(statHandleInvalid, 1f, ref statsWorldStorage);
                    Assert.IsFalse(success);
                    
                    success = statsAccessor.TryAddStatModifier(
                        statHandleInvalid,
                        new StatsTestsStatModifier
                        {
                            ModifierType = StatsTestsStatModifier.Type.Add,
                            ValueA = 1f,
                        },
                        out StatModifierHandle modifier2, 
                        ref statsWorldStorage);
                    Assert.IsFalse(success);
                    
                    success = statsAccessor.TryRemoveStatModifier(modifier2, ref statsWorldStorage);
                    Assert.IsFalse(success);
                }
            }
            
            statsWorldStorage.Dispose();
        }

        [Test]
        public void AddRemoveModifierCases()
        {
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);

            Entity entity1 = CreateStatsEntity(0, 10f, true, 
                out StatHandle statHandle1A, out StatHandle statHandle1B, out StatHandle statHandle1C);

            UpdateStatsWorld(ref statsAccessor);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out Stat stat1A,
                out int stat1AModifiersCount, out int stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out Stat stat1B,
                out int stat1BModifiersCount, out int stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out Stat stat1C,
                out int stat1CModifiersCount, out int stat1CObserversCount);
            
            // Added modifiers to stats (parenthesis is observed stat):
            // Modifiers buffer: 1-A(C), 2-B(A), 3-A(C), 4-B(A), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1C,
                },
                out StatModifierHandle modifier1, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier2, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1C,
                },
                out StatModifierHandle modifier3, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier4, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1C,
                },
                out StatModifierHandle modifier5, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier6, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1C,
                },
                out StatModifierHandle modifier7, 
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1B,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1A,
                },
                out StatModifierHandle modifier8, ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                out stat1BModifiersCount, out stat1BObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                out stat1CModifiersCount, out stat1CObserversCount);
            
            Assert.AreEqual(4, stat1AModifiersCount);
            Assert.AreEqual(4, stat1AObserversCount);
            Assert.AreEqual(4, stat1BModifiersCount);
            Assert.AreEqual(0, stat1BObserversCount);
            Assert.AreEqual(0, stat1CModifiersCount);
            Assert.AreEqual(4, stat1CObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 8, 8);
            
            // Remove middle modifier
            {
                // Modifiers buffer: 1-A(C), 2-B(A), 3-A(C), 4-B(A), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
                
                success = statsAccessor.TryRemoveStatModifier(modifier4, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 1-A(C), 2-B(A), 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(4, stat1AModifiersCount);
                Assert.AreEqual(3, stat1AObserversCount);
                Assert.AreEqual(3, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(4, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 7, 7);
                
                // Try removing it a second time
                success = statsAccessor.TryRemoveStatModifier(modifier4, ref statsWorldStorage);
                Assert.IsFalse(success);
            }
            
            // Remove first modifier for stat but not for buffer
            {
                // Modifiers buffer: 1-A(C), 2-B(A), 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
                
                success = statsAccessor.TryRemoveStatModifier(modifier2, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 1-A(C), 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(4, stat1AModifiersCount);
                Assert.AreEqual(2, stat1AObserversCount);
                Assert.AreEqual(2, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(4, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 6, 6);
                
                // Try removing it a second time
                success = statsAccessor.TryRemoveStatModifier(modifier2, ref statsWorldStorage);
                Assert.IsFalse(success);
            }
            
            // Remove first modifier in buffer
            {
                // Modifiers buffer: 1-A(C), 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
                
                success = statsAccessor.TryRemoveStatModifier(modifier1, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(3, stat1AModifiersCount);
                Assert.AreEqual(2, stat1AObserversCount);
                Assert.AreEqual(2, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(3, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 5, 5);
                
                // Try removing it a second time
                success = statsAccessor.TryRemoveStatModifier(modifier1, ref statsWorldStorage);
                Assert.IsFalse(success);
            }
            
            // Try to add new modifier then remove
            {
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
                
                success = statsAccessor.TryAddStatModifier(
                    statHandle1B,
                    new StatsTestsStatModifier
                    {
                        ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                        StatHandleA = statHandle1A,
                    },
                    out StatModifierHandle modifier9, 
                    ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A), 9-B(A)
            
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(3, stat1AModifiersCount);
                Assert.AreEqual(3, stat1AObserversCount);
                Assert.AreEqual(3, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(3, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 6, 6);
                
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A), 9-B(A)
                
                success = statsAccessor.TryRemoveStatModifier(modifier9, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
            
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(3, stat1AModifiersCount);
                Assert.AreEqual(2, stat1AObserversCount);
                Assert.AreEqual(2, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(3, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 5, 5);
                
                // Try removing it a second time
                success = statsAccessor.TryRemoveStatModifier(modifier9, ref statsWorldStorage);
                Assert.IsFalse(success);
            }
            
            // Remove last for stat but not for buffer
            {
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A), 7-A(C), 8-B(A)
                
                success = statsAccessor.TryRemoveStatModifier(modifier7, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A), 8-B(A)
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(2, stat1AModifiersCount);
                Assert.AreEqual(2, stat1AObserversCount);
                Assert.AreEqual(2, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(2, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 4, 4);
                
                // Try removing it a second time
                success = statsAccessor.TryRemoveStatModifier(modifier7, ref statsWorldStorage);
                Assert.IsFalse(success);
            }
            
            // Remove last modifier in buffer
            {
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A), 8-B(A)
                
                success = statsAccessor.TryRemoveStatModifier(modifier8, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A)
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(2, stat1AModifiersCount);
                Assert.AreEqual(1, stat1AObserversCount);
                Assert.AreEqual(1, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(2, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 3, 3);
                
                // Try removing it a second time
                success = statsAccessor.TryRemoveStatModifier(modifier8, ref statsWorldStorage);
                Assert.IsFalse(success);
            }
            
            // RemoveAll
            {
                // Modifiers buffer: 3-A(C), 5-A(C), 6-B(A)
                
                success = statsAccessor.TryRemoveAllStatModifiersOfStat(statHandle1A, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 6-B(A)
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(0, stat1AModifiersCount);
                Assert.AreEqual(1, stat1AObserversCount);
                Assert.AreEqual(1, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(0, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 1, 1);
                
                // Try a second time
                
                success = statsAccessor.TryRemoveAllStatModifiersOfStat(statHandle1A, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(0, stat1AModifiersCount);
                Assert.AreEqual(1, stat1AObserversCount);
                Assert.AreEqual(1, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(0, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 1, 1);
                
                // Modifiers buffer: 6-B(A)
                
                success = statsAccessor.TryRemoveAllStatModifiersOfStat(statHandle1C, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 6-B(A)
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(0, stat1AModifiersCount);
                Assert.AreEqual(1, stat1AObserversCount);
                Assert.AreEqual(1, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(0, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 1, 1);
                
                // Modifiers buffer: 6-B(A)
                
                success = statsAccessor.TryRemoveAllStatModifiersOfStat(statHandle1B, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                // Modifiers buffer: 
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(0, stat1AModifiersCount);
                Assert.AreEqual(0, stat1AObserversCount);
                Assert.AreEqual(0, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(0, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 0, 0);
                
                // Try a second time 
                success = statsAccessor.TryRemoveAllStatModifiersOfStat(statHandle1B, ref statsWorldStorage);
                Assert.IsTrue(success);
                
                GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                    out stat1AModifiersCount, out stat1AObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1B, ref statsAccessor, out stat1B,
                    out stat1BModifiersCount, out stat1BObserversCount);
                GetStatAndModifiersAndObserversCount(statHandle1C, ref statsAccessor, out stat1C,
                    out stat1CModifiersCount, out stat1CObserversCount);
            
                Assert.AreEqual(0, stat1AModifiersCount);
                Assert.AreEqual(0, stat1AObserversCount);
                Assert.AreEqual(0, stat1BModifiersCount);
                Assert.AreEqual(0, stat1BObserversCount);
                Assert.AreEqual(0, stat1CModifiersCount);
                Assert.AreEqual(0, stat1CObserversCount);
                AssertBufferValidElementLengths(entity1, 3, 0, 0);
            }
            
            statsWorldStorage.Dispose();
        }

        [Test]
        public void StatEventsTest()
        {
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);

            Entity entity1 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle1, out StatHandle statHandle2, out StatHandle statHandle3);

            UpdateStatsWorld(ref statsAccessor);
            
            // This modifier does not change stat value. No stat change
            // Triggers mod 1
            success = statsAccessor.TryAddStatModifier(
                statHandle1,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.SelfRemoveWhenStatAbove1000,
                    StatHandleA = statHandle2,
                },
                out StatModifierHandle modifier1, 
                ref statsWorldStorage);
            
            // Changes stat 2
            // Triggers mod 2 and mod 1
            success = statsAccessor.TryAddStatModifier(
                statHandle2,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3,
                },
                out StatModifierHandle modifier2, 
                ref statsWorldStorage);
            
            // Changes stat 3 and 2
            // Triggers mod 2 and mod 1
            statsAccessor.TryAddStatBaseValue(statHandle3, 1f, ref statsWorldStorage);

            int stat1ChangeEvents = 0;
            int stat2ChangeEvents = 0;
            int stat3ChangeEvents = 0;
            int modifier1TriggerEvents = 0;
            int modifier2TriggerEvents = 0;
            
            for (int i = 0; i < statsWorldStorage.StatChangeEventsList.Length; i++)
            {
                StatChangeEvent changedStat = statsWorldStorage.StatChangeEventsList[i];

                if (changedStat.StatHandle == statHandle1)
                {
                    stat1ChangeEvents++;
                }
                if (changedStat.StatHandle == statHandle2)
                {
                    stat2ChangeEvents++;
                }
                if (changedStat.StatHandle == statHandle3)
                {
                    stat3ChangeEvents++;
                }
            }
            
            for (int i = 0; i < statsWorldStorage.ModifierTriggerEventsList.Length; i++)
            {
                StatModifierHandle triggeredModifierHandle = statsWorldStorage.ModifierTriggerEventsList[i].ModifierHandle;

                if (triggeredModifierHandle == modifier1)
                {
                    modifier1TriggerEvents++;
                }
                if (triggeredModifierHandle == modifier2)
                {
                    modifier2TriggerEvents++;
                }
            }
            
            Assert.AreEqual(3, statsWorldStorage.StatChangeEventsList.Length);
            Assert.AreEqual(5, statsWorldStorage.ModifierTriggerEventsList.Length);
            Assert.AreEqual(0, stat1ChangeEvents);
            Assert.AreEqual(2, stat2ChangeEvents);
            Assert.AreEqual(1, stat3ChangeEvents);
            Assert.AreEqual(3, modifier1TriggerEvents);
            Assert.AreEqual(2, modifier2TriggerEvents);
            
            statsWorldStorage.Dispose();
        }

        [Test]
        public void DestroyObservedStat()
        {
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);

            Entity entity1 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle1A, out _, out _);
            Entity entity2 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle2A, out _, out _);

            UpdateStatsWorld(ref statsAccessor);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out Stat stat1A,
                out int stat1AModifiersCount, out int stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out Stat stat2A,
                out int stat2AModifiersCount, out int stat2AObserversCount);

            // Make 1A an observer of 2A
            success = statsAccessor.TryAddStatModifier(
                statHandle1A,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle2A,
                },
                out StatModifierHandle modifier1, 
                ref statsWorldStorage);
            Assert.IsTrue(success);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(20f));
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(1, stat2AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 1, 0);
            AssertBufferValidElementLengths(entity2, 3, 0, 1);
            
            // Destroy entity2 (so destroy stat 2A)
            EntityManager.DestroyEntity(entity2);
            UpdateStatsWorld(ref statsAccessor);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            // TODO: A stats reaction isn't triggered on destroy by default...
            // it would have to be triggered manually in some destroy pipeline.
            // But in theory we could also cache the last known value in the modifier struct...
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(20f)); 
            Assert.AreEqual(1, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(0f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(0f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 1, 0);
            AssertBufferValidElementLengths(entity2, 0, 0, 0);
            
            // Remove modifier
            success = statsAccessor.TryRemoveStatModifier(modifier1, ref statsWorldStorage);
            Assert.IsTrue(success);

            GetStatAndModifiersAndObserversCount(statHandle1A, ref statsAccessor, out stat1A,
                out stat1AModifiersCount, out stat1AObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2A, ref statsAccessor, out stat2A,
                out stat2AModifiersCount, out stat2AObserversCount);
            
            Assert.IsTrue(stat1A.BaseValue.IsRoughlyEqual(10f));
            Assert.IsTrue(stat1A.Value.IsRoughlyEqual(10f));
            Assert.AreEqual(0, stat1AModifiersCount);
            Assert.AreEqual(0, stat1AObserversCount);
            Assert.IsTrue(stat2A.BaseValue.IsRoughlyEqual(0f));
            Assert.IsTrue(stat2A.Value.IsRoughlyEqual(0f));
            Assert.AreEqual(0, stat2AModifiersCount);
            Assert.AreEqual(0, stat2AObserversCount);
            AssertBufferValidElementLengths(entity1, 3, 0, 0);
            AssertBufferValidElementLengths(entity2, 0, 0, 0);
            
            statsWorldStorage.Dispose();
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
            
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);
            
            Entity entity1 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle1, out StatHandle statHandle4, out StatHandle statHandle7);
            Entity entity2 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle2, out StatHandle statHandle5, out StatHandle statHandle8);
            Entity entity3 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle3, out StatHandle statHandle6, out StatHandle statHandle9);

            UpdateStatsWorld(ref statsAccessor);

            statsAccessor.TrySetStatBaseValue(statHandle1, 1f, ref statsWorldStorage);
            statsAccessor.TrySetStatBaseValue(statHandle2, 2f, ref statsWorldStorage);
            statsAccessor.TrySetStatBaseValue(statHandle3, 3f, ref statsWorldStorage);
            statsAccessor.TrySetStatBaseValue(statHandle4, 4f, ref statsWorldStorage);
            statsAccessor.TrySetStatBaseValue(statHandle5, 5f, ref statsWorldStorage);
            statsAccessor.TrySetStatBaseValue(statHandle6, 6f, ref statsWorldStorage);
            statsAccessor.TrySetStatBaseValue(statHandle7, 7f, ref statsWorldStorage);
            statsAccessor.TrySetStatBaseValue(statHandle8, 8f, ref statsWorldStorage);
            statsAccessor.TrySetStatBaseValue(statHandle9, 9f, ref statsWorldStorage);

            GetStatAndModifiersAndObserversCount(statHandle1, ref statsAccessor, out Stat stat1,
                out int stat1ModifiersCount, out int stat1ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2, ref statsAccessor, out Stat stat2,
                out int stat2ModifiersCount, out int stat2ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3, ref statsAccessor, out Stat stat3,
                out int stat3ModifiersCount, out int stat3ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle4, ref statsAccessor, out Stat stat4,
                out int stat4ModifiersCount, out int stat4ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle5, ref statsAccessor, out Stat stat5,
                out int stat5ModifiersCount, out int stat5ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle6, ref statsAccessor, out Stat stat6,
                out int stat6ModifiersCount, out int stat6ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle7, ref statsAccessor, out Stat stat7,
                out int stat7ModifiersCount, out int stat7ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle8, ref statsAccessor, out Stat stat8,
                out int stat8ModifiersCount, out int stat8ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle9, ref statsAccessor, out Stat stat9,
                out int stat9ModifiersCount, out int stat9ObserversCount);
            
            Assert.IsTrue(stat1.Value.IsRoughlyEqual(1f));
            Assert.IsTrue(stat2.Value.IsRoughlyEqual(2f));
            Assert.IsTrue(stat3.Value.IsRoughlyEqual(3f));
            Assert.IsTrue(stat4.Value.IsRoughlyEqual(4f));
            Assert.IsTrue(stat5.Value.IsRoughlyEqual(5f));
            Assert.IsTrue(stat6.Value.IsRoughlyEqual(6f));
            Assert.IsTrue(stat7.Value.IsRoughlyEqual(7f));
            Assert.IsTrue(stat8.Value.IsRoughlyEqual(8f));
            Assert.IsTrue(stat9.Value.IsRoughlyEqual(9f));
            
            statsAccessor.TryAddStatModifier(
                statHandle2,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1,
                },
                out StatModifierHandle modifier2_1, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle2,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3,
                },
                out StatModifierHandle modifier2_3, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle3,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1,
                },
                out StatModifierHandle modifier3_1, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle4,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle2,
                },
                out StatModifierHandle modifier4_2, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle5,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromTwoStats,
                    StatHandleA = statHandle2,
                    StatHandleB = statHandle3,
                },
                out StatModifierHandle modifier5_23, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle6,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3,
                },
                out StatModifierHandle modifier6_3, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle7,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromTwoStats,
                    StatHandleA = statHandle4,
                    StatHandleB = statHandle5,
                },
                out StatModifierHandle modifier7_45, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle8,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromTwoStats,
                    StatHandleA = statHandle5,
                    StatHandleB = statHandle6,
                },
                out StatModifierHandle modifier8_56, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle8,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle7,
                },
                out StatModifierHandle modifier8_7, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle9,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle5,
                },
                out StatModifierHandle modifier9_5, 
                ref statsWorldStorage);
            statsAccessor.TryAddStatModifier(
                statHandle9,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromTwoStats,
                    StatHandleA = statHandle7,
                    StatHandleB = statHandle8,
                },
                out StatModifierHandle modifier9_78, 
                ref statsWorldStorage);

            GetStatAndModifiersAndObserversCount(statHandle1, ref statsAccessor, out stat1,
                out stat1ModifiersCount, out stat1ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2, ref statsAccessor, out stat2,
                out stat2ModifiersCount, out stat2ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3, ref statsAccessor, out stat3,
                out stat3ModifiersCount, out stat3ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle4, ref statsAccessor, out stat4,
                out stat4ModifiersCount, out stat4ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle5, ref statsAccessor, out stat5,
                out stat5ModifiersCount, out stat5ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle6, ref statsAccessor, out stat6,
                out stat6ModifiersCount, out stat6ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle7, ref statsAccessor, out stat7,
                out stat7ModifiersCount, out stat7ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle8, ref statsAccessor, out stat8,
                out stat8ModifiersCount, out stat8ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle9, ref statsAccessor, out stat9,
                out stat9ModifiersCount, out stat9ObserversCount);
            
            Assert.IsTrue(stat1.BaseValue.IsRoughlyEqual(1f));
            Assert.IsTrue(stat1.Value.IsRoughlyEqual(1f));
            Assert.IsTrue(stat2.BaseValue.IsRoughlyEqual(2f));
            Assert.IsTrue(stat2.Value.IsRoughlyEqual(7f)); // Add 1 and 3
            Assert.IsTrue(stat3.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(stat3.Value.IsRoughlyEqual(4f)); // Add 1
            Assert.IsTrue(stat4.BaseValue.IsRoughlyEqual(4f));
            Assert.IsTrue(stat4.Value.IsRoughlyEqual(11f)); // Add 2
            Assert.IsTrue(stat5.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat5.Value.IsRoughlyEqual(16f)); // Add 2 and 3
            Assert.IsTrue(stat6.BaseValue.IsRoughlyEqual(6f));
            Assert.IsTrue(stat6.Value.IsRoughlyEqual(10f)); // Add 3
            Assert.IsTrue(stat7.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat7.Value.IsRoughlyEqual(34f)); // Add 4 and 5
            Assert.IsTrue(stat8.BaseValue.IsRoughlyEqual(8f));
            Assert.IsTrue(stat8.Value.IsRoughlyEqual(68f)); // Add 5 and 6 and 7
            Assert.IsTrue(stat9.BaseValue.IsRoughlyEqual(9f));
            Assert.IsTrue(stat9.Value.IsRoughlyEqual(127f)); // Add 5 and 7 and 8
            
            statsAccessor.TryAddStatBaseValue(statHandle1, 1f, ref statsWorldStorage);

            GetStatAndModifiersAndObserversCount(statHandle1, ref statsAccessor, out stat1,
                out stat1ModifiersCount, out stat1ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2, ref statsAccessor, out stat2,
                out stat2ModifiersCount, out stat2ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3, ref statsAccessor, out stat3,
                out stat3ModifiersCount, out stat3ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle4, ref statsAccessor, out stat4,
                out stat4ModifiersCount, out stat4ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle5, ref statsAccessor, out stat5,
                out stat5ModifiersCount, out stat5ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle6, ref statsAccessor, out stat6,
                out stat6ModifiersCount, out stat6ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle7, ref statsAccessor, out stat7,
                out stat7ModifiersCount, out stat7ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle8, ref statsAccessor, out stat8,
                out stat8ModifiersCount, out stat8ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle9, ref statsAccessor, out stat9,
                out stat9ModifiersCount, out stat9ObserversCount);
            
            Assert.IsTrue(stat1.BaseValue.IsRoughlyEqual(2f));
            Assert.IsTrue(stat1.Value.IsRoughlyEqual(2f));
            Assert.IsTrue(stat2.BaseValue.IsRoughlyEqual(2f));
            Assert.IsTrue(stat2.Value.IsRoughlyEqual(9f)); // Add 1 and 3
            Assert.IsTrue(stat3.BaseValue.IsRoughlyEqual(3f));
            Assert.IsTrue(stat3.Value.IsRoughlyEqual(5f)); // Add 1
            Assert.IsTrue(stat4.BaseValue.IsRoughlyEqual(4f));
            Assert.IsTrue(stat4.Value.IsRoughlyEqual(13f)); // Add 2
            Assert.IsTrue(stat5.BaseValue.IsRoughlyEqual(5f));
            Assert.IsTrue(stat5.Value.IsRoughlyEqual(19f)); // Add 2 and 3
            Assert.IsTrue(stat6.BaseValue.IsRoughlyEqual(6f));
            Assert.IsTrue(stat6.Value.IsRoughlyEqual(11f)); // Add 3
            Assert.IsTrue(stat7.BaseValue.IsRoughlyEqual(7f));
            Assert.IsTrue(stat7.Value.IsRoughlyEqual(39f)); // Add 4 and 5
            Assert.IsTrue(stat8.BaseValue.IsRoughlyEqual(8f));
            Assert.IsTrue(stat8.Value.IsRoughlyEqual(77f)); // Add 5 and 6 and 7
            Assert.IsTrue(stat9.BaseValue.IsRoughlyEqual(9f));
            Assert.IsTrue(stat9.Value.IsRoughlyEqual(144f)); // Add 5 and 7 and 8
            
            statsWorldStorage.Dispose();
        }
        
        [Test]
        public void InfiniteObserversLoopPrevention()
        {
            //                                             6->\
            // Try to create an infinite observers loop: 1->2->3->4
            //                                                  \->1  (this one would cause the infinite loop)
            //                                                   \->5
            // ( A->B means "A observes B" )
        
            bool success = false;
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);
            
            Entity entity1 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle1, out StatHandle statHandle4, out _);
            Entity entity2 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle2, out StatHandle statHandle5, out _);
            Entity entity3 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle3, out StatHandle statHandle6, out _);

            UpdateStatsWorld(ref statsAccessor);

            GetStatAndModifiersAndObserversCount(statHandle1, ref statsAccessor, out Stat stat1,
                out int stat1ModifiersCount, out int stat1ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle2, ref statsAccessor, out Stat stat2,
                out int stat2ModifiersCount, out int stat2ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle3, ref statsAccessor, out Stat stat3,
                out int stat3ModifiersCount, out int stat3ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle4, ref statsAccessor, out Stat stat4,
                out int stat4ModifiersCount, out int stat4ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle5, ref statsAccessor, out Stat stat5,
                out int stat5ModifiersCount, out int stat5ObserversCount);
            GetStatAndModifiersAndObserversCount(statHandle6, ref statsAccessor, out Stat stat6,
                out int stat6ModifiersCount, out int stat6ObserversCount);
            
            // This one shouldn't work (self-observing stat)
            success = statsAccessor.TryAddStatModifier(
                statHandle1,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1,
                },
                out _,
                ref statsWorldStorage);
            Assert.IsFalse(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1, ref statsAccessor, out stat1,
                out stat1ModifiersCount, out stat1ObserversCount);
            Assert.AreEqual(0, stat1ModifiersCount);
            
            success = statsAccessor.TryAddStatModifier(
                statHandle1,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle2,
                },
                out _,
                ref statsWorldStorage);
            Assert.IsTrue(success);
            success = statsAccessor.TryAddStatModifier(
                statHandle2,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3,
                },
                out _,
                ref statsWorldStorage);
            Assert.IsTrue(success);
            success = statsAccessor.TryAddStatModifier(
                statHandle3,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle4,
                },
                out _,
                ref statsWorldStorage);
            Assert.IsTrue(success);
            success = statsAccessor.TryAddStatModifier(
                statHandle6,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle3,
                },
                out _,
                ref statsWorldStorage);
            Assert.IsTrue(success);
            success = statsAccessor.TryAddStatModifier(
                statHandle3,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle5,
                },
                out _,
                ref statsWorldStorage);
            Assert.IsTrue(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1, ref statsAccessor, out stat1,
                out stat1ModifiersCount, out stat1ObserversCount);
            Assert.AreEqual(1, stat1ModifiersCount);
            
            // This one shouldn't work
            success = statsAccessor.TryAddStatModifier(
                statHandle3,
                new StatsTestsStatModifier
                {
                    ModifierType = StatsTestsStatModifier.Type.AddFromStat,
                    StatHandleA = statHandle1,
                },
                out _,
                ref statsWorldStorage);
            Assert.IsFalse(success);
            
            GetStatAndModifiersAndObserversCount(statHandle1, ref statsAccessor, out stat1,
                out stat1ModifiersCount, out stat1ObserversCount);
            Assert.AreEqual(1, stat1ModifiersCount);
            
            statsWorldStorage.Dispose();
        }

        [Test]
        public void StatWorldJobExceptionsTest()
        {
            EntityManager.CompleteAllTrackedJobs();
            
            StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsAccessor = CreateStatsWorld();
            StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> statsWorldStorage = new StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack>(Allocator.Persistent);
            
            Entity entity1 = CreateStatsEntity(0, 10f, true,
                out StatHandle statHandle1, out StatHandle statHandle2, out _);
            
            EntityManager.CompleteAllTrackedJobs();
            UpdateStatsWorld(ref statsAccessor);
            
            JobHandle dep1 = default;
            JobHandle dep2 = default;

            Assert.DoesNotThrow(() =>
            {
                dep1 = new StatsWorldJob
                {
                    StatHandle = statHandle1,
                    StatsAccessor = statsAccessor,
                    StatsWorldStorage = statsWorldStorage,
                }.Schedule(default);
            });
            
            dep1.Complete();
            dep2.Complete();
            EntityManager.CompleteAllTrackedJobs();
            UpdateStatsWorld(ref statsAccessor);

            // Two single-thread jobs using the same StatsWorld in parallel
            Assert.Throws<InvalidOperationException>(() =>
            {
                dep1 = new StatsWorldJob
                {
                    StatHandle = statHandle1,
                    StatsAccessor = statsAccessor,
                    StatsWorldStorage = statsWorldStorage,
                }.Schedule(default);
                dep2 = new StatsWorldJob
                {
                    StatHandle = statHandle1,
                    StatsAccessor = statsAccessor,
                    StatsWorldStorage = statsWorldStorage,
                }.Schedule(default);
            });
                
            JobHandle dep3 = JobHandle.CombineDependencies(dep1, dep2);
            dep3.Complete();
            dep1.Complete();
            dep2.Complete();
            EntityManager.CompleteAllTrackedJobs();
            UpdateStatsWorld(ref statsAccessor);

            // A parallel-for job using a StatsWorld
            Assert.Throws<InvalidOperationException>(() =>
            {
                JobHandle dep1 = new StatsWorldParallelJob
                {
                    StatHandle = statHandle1,
                    StatsAccessor = statsAccessor,
                    StatsWorldStorage = statsWorldStorage,
                }.Schedule(10, 1, default);
            });
            
            dep1.Complete();
            dep2.Complete();
            EntityManager.CompleteAllTrackedJobs();
            UpdateStatsWorld(ref statsAccessor);
            
            // A parallel-for job using a [ReadOnly] StatsWorld (throws inside the job)
            {
                dep1 = new StatsWorldReadOnlyParallelJob
                {
                    StatHandle = statHandle1,
                    StatsAccessor = statsAccessor,
                    StatsWorldStorage = statsWorldStorage,
                }.Schedule(3, 1, default);
                
                dep1.Complete();
            }
            
            dep1.Complete();
            dep2.Complete();
            EntityManager.CompleteAllTrackedJobs();
            UpdateStatsWorld(ref statsAccessor);
            
            statsWorldStorage.Dispose();
        }

        private struct StatsWorldJob : IJob
        {
            public StatHandle StatHandle;
            public StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> StatsAccessor;
            public StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> StatsWorldStorage;
            
            public void Execute()
            {
                StatsAccessor.TryAddStatBaseValue(StatHandle, 1f, ref StatsWorldStorage);
            }
        }

        private struct StatsWorldParallelJob : IJobParallelFor
        {
            public StatHandle StatHandle;
            public StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> StatsAccessor;
            public StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> StatsWorldStorage;
            
            public void Execute(int index)
            {
                StatsAccessor.TryAddStatBaseValue(StatHandle, 1f, ref StatsWorldStorage);
            }
        }

        private struct StatsWorldReadOnlyParallelJob : IJobParallelFor
        {
            public StatHandle StatHandle;
            [ReadOnly]
            public StatsAccessor<StatsTestsStatModifier, StatsTestsStatModifier.Stack> StatsAccessor;
            [ReadOnly]
            public StatsWorldData<StatsTestsStatModifier, StatsTestsStatModifier.Stack> StatsWorldStorage;
            
            public void Execute(int index)
            {
                bool success = true;
                try
                {
                    StatsAccessor.TryAddStatBaseValue(StatHandle, 1f, ref StatsWorldStorage);
                }
                catch (InvalidOperationException e)
                {
                    Debug.Log(e);
                    success = false;
                }
                Assert.IsFalse(success);
            }
        }
    }
}