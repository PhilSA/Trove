
using Unity.Entities;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Trove.EventSystems.Tests
{
    [TestFixture]
    public class EntityPolymorphicEventTests
    {
        public const int EventsPerSystemOrThreadForR1 = 3;
        public const int EventsPerSystemOrThreadForR2 = 1;
        public const int ParallelCount = 3;

        public const int MainThreadStreamEventKeyA = 1;
        public const int MainThreadStreamEventKeyB = 2;
        public const int SingleJobStreamEventKeyA = 3;
        public const int SingleJobStreamEventKeyB = 4;
        public const int ParallelJobStreamEventKeyA = 5;
        public const int ParallelJobStreamEventKeyB = 6;

        public World World => World.DefaultGameObjectInjectionWorld;

        [SetUp]
        public void SetUp()
        { }

        [TearDown]
        public void TearDown()
        {
            EventTestUtilities.DestroyTestEntities(World);
        }

        [Test]
        public void EntityEventTest1()
        {
            // Create event receivers
            Entity receiverEntity1 = EventTestUtilities.CreateTestEntity(World.EntityManager);
            Entity receiverEntity2 = EventTestUtilities.CreateTestEntity(World.EntityManager);
            World.EntityManager.AddComponentData(receiverEntity1, new EntityEventReceiver());
            World.EntityManager.AddComponentData(receiverEntity2, new EntityEventReceiver());
            World.EntityManager.AddComponentData(receiverEntity1, new HasTestEntityPolymorphicEvents());
            World.EntityManager.AddComponentData(receiverEntity2, new HasTestEntityPolymorphicEvents());
            World.EntityManager.SetComponentEnabled<HasTestEntityPolymorphicEvents>(receiverEntity1, false);
            World.EntityManager.SetComponentEnabled<HasTestEntityPolymorphicEvents>(receiverEntity2, false);
            World.EntityManager.AddBuffer<TestEntityPolymorphicEventBufferElement>(receiverEntity1);
            World.EntityManager.AddBuffer<TestEntityPolymorphicEventBufferElement>(receiverEntity2);

            // Create test singleton
            Entity testSingleton = EventTestUtilities.CreateTestEntity(World.EntityManager);
            World.EntityManager.AddComponentData(testSingleton, new EntityEventTestSingleton
            {
                ReceiverEntity1 = receiverEntity1,
                ReceiverEntity2 = receiverEntity2,
            });

            World.Update();
            World.EntityManager.CompleteAllTrackedJobs();

            EntityEventReceiver eventReceiver1 = World.EntityManager.GetComponentData<EntityEventReceiver>(receiverEntity1);
            EntityEventReceiver eventReceiver2 = World.EntityManager.GetComponentData<EntityEventReceiver>(receiverEntity2);

            Assert.IsTrue(World.EntityManager.IsComponentEnabled<HasTestEntityPolymorphicEvents>(receiverEntity1));
            Assert.IsTrue(World.EntityManager.IsComponentEnabled<HasTestEntityPolymorphicEvents>(receiverEntity2));

            UnityEngine.Debug.Log($"Main" +
                $"{eventReceiver1.MainThread_EventCounterVal1} " +
                $"{eventReceiver1.MainThread_EventCounterVal2} " +
                $"{eventReceiver1.MainThread_EventCounterVal3} " +
                $"{eventReceiver1.MainThread_EventCounterVal4} " +
                $"{eventReceiver1.MainThread_EventCounterVal5} " +
                $"{eventReceiver1.MainThread_EventCounterVal6} " +
                "Single" +
                $"{eventReceiver1.SingleJob_EventCounterVal1} " +
                $"{eventReceiver1.SingleJob_EventCounterVal2} " +
                $"{eventReceiver1.SingleJob_EventCounterVal3} " +
                $"{eventReceiver1.SingleJob_EventCounterVal4} " +
                $"{eventReceiver1.SingleJob_EventCounterVal5} " +
                $"{eventReceiver1.SingleJob_EventCounterVal6} " +
                "Parallel" +
                $"{eventReceiver1.ParallelJob_EventCounterVal1} " +
                $"{eventReceiver1.ParallelJob_EventCounterVal2} " +
                $"{eventReceiver1.ParallelJob_EventCounterVal3} " +
                $"{eventReceiver1.ParallelJob_EventCounterVal4} " +
                $"{eventReceiver1.ParallelJob_EventCounterVal5} " +
                $"{eventReceiver1.ParallelJob_EventCounterVal6} ");

            {
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.MainThread_EventCounterVal1);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.MainThread_EventCounterVal2);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.MainThread_EventCounterVal3);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.MainThread_EventCounterVal4);
                Assert.AreEqual(EventsPerSystemOrThreadForR1 * ParallelCount, eventReceiver1.MainThread_EventCounterVal5);
                Assert.AreEqual(EventsPerSystemOrThreadForR1 * ParallelCount, eventReceiver1.MainThread_EventCounterVal6);

                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.MainThread_EventCounterVal1);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.MainThread_EventCounterVal2);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.MainThread_EventCounterVal3);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.MainThread_EventCounterVal4);
                Assert.AreEqual(EventsPerSystemOrThreadForR2 * ParallelCount, eventReceiver2.MainThread_EventCounterVal5);
                Assert.AreEqual(EventsPerSystemOrThreadForR2 * ParallelCount, eventReceiver2.MainThread_EventCounterVal6);
            }

            {
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.SingleJob_EventCounterVal1);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.SingleJob_EventCounterVal2);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.SingleJob_EventCounterVal3);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.SingleJob_EventCounterVal4);
                Assert.AreEqual(EventsPerSystemOrThreadForR1 * ParallelCount, eventReceiver1.SingleJob_EventCounterVal5);
                Assert.AreEqual(EventsPerSystemOrThreadForR1 * ParallelCount, eventReceiver1.SingleJob_EventCounterVal6);

                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.SingleJob_EventCounterVal1);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.SingleJob_EventCounterVal2);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.SingleJob_EventCounterVal3);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.SingleJob_EventCounterVal4);
                Assert.AreEqual(EventsPerSystemOrThreadForR2 * ParallelCount, eventReceiver2.SingleJob_EventCounterVal5);
                Assert.AreEqual(EventsPerSystemOrThreadForR2 * ParallelCount, eventReceiver2.SingleJob_EventCounterVal6);
            }

            {
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.ParallelJob_EventCounterVal1);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.ParallelJob_EventCounterVal2);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.ParallelJob_EventCounterVal3);
                Assert.AreEqual(EventsPerSystemOrThreadForR1, eventReceiver1.ParallelJob_EventCounterVal4);
                Assert.AreEqual(EventsPerSystemOrThreadForR1 * ParallelCount, eventReceiver1.ParallelJob_EventCounterVal5);
                Assert.AreEqual(EventsPerSystemOrThreadForR1 * ParallelCount, eventReceiver1.ParallelJob_EventCounterVal6);

                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.ParallelJob_EventCounterVal1);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.ParallelJob_EventCounterVal2);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.ParallelJob_EventCounterVal3);
                Assert.AreEqual(EventsPerSystemOrThreadForR2, eventReceiver2.ParallelJob_EventCounterVal4);
                Assert.AreEqual(EventsPerSystemOrThreadForR2 * ParallelCount, eventReceiver2.ParallelJob_EventCounterVal5);
                Assert.AreEqual(EventsPerSystemOrThreadForR2 * ParallelCount, eventReceiver2.ParallelJob_EventCounterVal6);
            }

            World.Unmanaged.GetExistingSystemState<EntityPolymorphicEventTests_MainThreadStreamWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<EntityPolymorphicEventTests_SingleJobStreamWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<EntityPolymorphicEventTests_ParallelJobStreamWriterSystem>().Enabled = false;

            World.Update();
            World.EntityManager.CompleteAllTrackedJobs();

            Assert.IsFalse(World.EntityManager.IsComponentEnabled<HasTestEntityPolymorphicEvents>(receiverEntity1));
            Assert.IsFalse(World.EntityManager.IsComponentEnabled<HasTestEntityPolymorphicEvents>(receiverEntity2));
            Assert.AreEqual(0, World.EntityManager.GetBuffer<TestEntityPolymorphicEventBufferElement>(receiverEntity1).Length);
            Assert.AreEqual(0, World.EntityManager.GetBuffer<TestEntityPolymorphicEventBufferElement>(receiverEntity2).Length);

            World.Unmanaged.GetExistingSystemState<EntityPolymorphicEventTests_MainThreadStreamWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<EntityPolymorphicEventTests_SingleJobStreamWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<EntityPolymorphicEventTests_ParallelJobStreamWriterSystem>().Enabled = true;
        }
    }

    #region EventWriters
    [UpdateBefore(typeof(TestEntityPolymorphicEventSystem))]
    public partial struct EntityPolymorphicEventTests_MainThreadStreamWriterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityPolymorphicEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            TestEntityPolymorphicEventsSingleton singleton = SystemAPI.GetSingletonRW<TestEntityPolymorphicEventsSingleton>().ValueRW;
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();
            NativeStream.Writer eventsStream = singleton.StreamEventsManager.CreateEventStream(1).AsWriter();

            eventsStream.BeginForEachIndex(0);

            for (int i = 0; i < EntityPolymorphicEventTests.EventsPerSystemOrThreadForR1; i++)
            {
                TestEntityPolymorphicEventManager.Write(ref eventsStream, testSingleton.ReceiverEntity1, new TestEntityPolymorphicEventA
                {
                    Val = EntityPolymorphicEventTests.MainThreadStreamEventKeyA,
                });
                TestEntityPolymorphicEventManager.Write(ref eventsStream, testSingleton.ReceiverEntity1, new TestEntityPolymorphicEventB
                {
                    Val1 = EntityPolymorphicEventTests.MainThreadStreamEventKeyB,
                    Val2 = EntityPolymorphicEventTests.MainThreadStreamEventKeyB,
                    Val3 = EntityPolymorphicEventTests.MainThreadStreamEventKeyB,
                });
            }

            for (int i = 0; i < EntityPolymorphicEventTests.EventsPerSystemOrThreadForR2; i++)
            {
                TestEntityPolymorphicEventManager.Write(ref eventsStream, testSingleton.ReceiverEntity2, new TestEntityPolymorphicEventA
                {
                    Val = EntityPolymorphicEventTests.MainThreadStreamEventKeyA,
                });
                TestEntityPolymorphicEventManager.Write(ref eventsStream, testSingleton.ReceiverEntity2, new TestEntityPolymorphicEventB
                {
                    Val1 = EntityPolymorphicEventTests.MainThreadStreamEventKeyB,
                    Val2 = EntityPolymorphicEventTests.MainThreadStreamEventKeyB,
                    Val3 = EntityPolymorphicEventTests.MainThreadStreamEventKeyB,
                });
            }

            eventsStream.EndForEachIndex();
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestEntityPolymorphicEventSystem))]
    public partial struct EntityPolymorphicEventTests_SingleJobStreamWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityPolymorphicEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestEntityPolymorphicEventsSingleton singleton = SystemAPI.GetSingletonRW<TestEntityPolymorphicEventsSingleton>().ValueRW;
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();

            state.Dependency = new WriteJob
            {
                EventsStream = singleton.StreamEventsManager.CreateEventStream(1).AsWriter(),
                TestSingleton = testSingleton,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJob
        {
            public NativeStream.Writer EventsStream;
            public EntityEventTestSingleton TestSingleton;

            public void Execute()
            {
                EventsStream.BeginForEachIndex(0);

                for (int i = 0; i < EntityPolymorphicEventTests.EventsPerSystemOrThreadForR1; i++)
                {
                    TestEntityPolymorphicEventManager.Write(ref EventsStream, TestSingleton.ReceiverEntity1, new TestEntityPolymorphicEventA
                    {
                        Val = EntityPolymorphicEventTests.SingleJobStreamEventKeyA,
                    });
                    TestEntityPolymorphicEventManager.Write(ref EventsStream, TestSingleton.ReceiverEntity1, new TestEntityPolymorphicEventB
                    {
                        Val1 = EntityPolymorphicEventTests.SingleJobStreamEventKeyB,
                        Val2 = EntityPolymorphicEventTests.SingleJobStreamEventKeyB,
                        Val3 = EntityPolymorphicEventTests.SingleJobStreamEventKeyB,
                    });
                }

                for (int i = 0; i < EntityPolymorphicEventTests.EventsPerSystemOrThreadForR2; i++)
                {
                    TestEntityPolymorphicEventManager.Write(ref EventsStream, TestSingleton.ReceiverEntity2, new TestEntityPolymorphicEventA
                    {
                        Val = EntityPolymorphicEventTests.SingleJobStreamEventKeyA,
                    });
                    TestEntityPolymorphicEventManager.Write(ref EventsStream, TestSingleton.ReceiverEntity2, new TestEntityPolymorphicEventB
                    {
                        Val1 = EntityPolymorphicEventTests.SingleJobStreamEventKeyB,
                        Val2 = EntityPolymorphicEventTests.SingleJobStreamEventKeyB,
                        Val3 = EntityPolymorphicEventTests.SingleJobStreamEventKeyB,
                    });
                }

                EventsStream.EndForEachIndex();
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestEntityPolymorphicEventSystem))]
    public partial struct EntityPolymorphicEventTests_ParallelJobStreamWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityPolymorphicEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestEntityPolymorphicEventsSingleton singleton = SystemAPI.GetSingletonRW<TestEntityPolymorphicEventsSingleton>().ValueRW;
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();

            state.Dependency = new WriteJob
            {
                EventsStream = singleton.StreamEventsManager.CreateEventStream(EntityPolymorphicEventTests.ParallelCount).AsWriter(),
                TestSingleton = testSingleton,
            }.Schedule(EntityPolymorphicEventTests.ParallelCount, 1, state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJobParallelFor
        {
            public NativeStream.Writer EventsStream;
            public EntityEventTestSingleton TestSingleton;

            public void Execute(int index)
            {
                EventsStream.BeginForEachIndex(index);

                for (int i = 0; i < EntityPolymorphicEventTests.EventsPerSystemOrThreadForR1; i++)
                {
                    TestEntityPolymorphicEventManager.Write(ref EventsStream, TestSingleton.ReceiverEntity1, new TestEntityPolymorphicEventA
                    {
                        Val = EntityPolymorphicEventTests.ParallelJobStreamEventKeyA,
                    });
                    TestEntityPolymorphicEventManager.Write(ref EventsStream, TestSingleton.ReceiverEntity1, new TestEntityPolymorphicEventB
                    {
                        Val1 = EntityPolymorphicEventTests.ParallelJobStreamEventKeyB,
                        Val2 = EntityPolymorphicEventTests.ParallelJobStreamEventKeyB,
                        Val3 = EntityPolymorphicEventTests.ParallelJobStreamEventKeyB,
                    });
                }

                for (int i = 0; i < EntityPolymorphicEventTests.EventsPerSystemOrThreadForR2; i++)
                {
                    TestEntityPolymorphicEventManager.Write(ref EventsStream, TestSingleton.ReceiverEntity2, new TestEntityPolymorphicEventA
                    {
                        Val = EntityPolymorphicEventTests.ParallelJobStreamEventKeyA,
                    });
                    TestEntityPolymorphicEventManager.Write(ref EventsStream, TestSingleton.ReceiverEntity2, new TestEntityPolymorphicEventB
                    {
                        Val1 = EntityPolymorphicEventTests.ParallelJobStreamEventKeyB,
                        Val2 = EntityPolymorphicEventTests.ParallelJobStreamEventKeyB,
                        Val3 = EntityPolymorphicEventTests.ParallelJobStreamEventKeyB,
                    });
                }

                EventsStream.EndForEachIndex();
            }
        }
    }
    #endregion

    #region EventReaders
    [UpdateAfter(typeof(TestEntityPolymorphicEventSystem))]
    public partial struct EntityPolymorphicEventTests_MainThreadReaderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityPolymorphicEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        public unsafe void OnUpdate(ref SystemState state)
        {
            foreach(var (eventReceiver, eventsBuffer, entity) in SystemAPI.Query<RefRW<EntityEventReceiver>, DynamicBuffer<TestEntityPolymorphicEventBufferElement>>().WithEntityAccess())
            {
                int readIndex = 0;
                byte* bufferPtr = (byte*)eventsBuffer.GetUnsafeReadOnlyPtr();
                while (TestEntityPolymorphicEventManager.ExecuteNextEvent(bufferPtr, eventsBuffer.Length, ref readIndex, ref eventReceiver.ValueRW, 1))
                { }
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(TestEntityPolymorphicEventSystem))]
    public partial struct EntityPolymorphicEventTests_SingleJobReaderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityPolymorphicEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ReadJob
            {
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct ReadJob : IJobEntity
        {
            public unsafe void Execute(Entity entity, ref EntityEventReceiver eventReceiver, in DynamicBuffer<TestEntityPolymorphicEventBufferElement> eventsBuffer)
            {
                int readIndex = 0;
                byte* bufferPtr = (byte*)eventsBuffer.GetUnsafeReadOnlyPtr();
                while (TestEntityPolymorphicEventManager.ExecuteNextEvent(bufferPtr, eventsBuffer.Length, ref readIndex, ref eventReceiver, 2))
                { }
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(TestEntityPolymorphicEventSystem))]
    public partial struct EntityPolymorphicEventTests_ParallelJobReaderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityPolymorphicEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ReadJob
            {
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ReadJob : IJobEntity
        {
            public unsafe void Execute(Entity entity, ref EntityEventReceiver eventReceiver, in DynamicBuffer<TestEntityPolymorphicEventBufferElement> eventsBuffer)
            {
                int readIndex = 0;
                byte* bufferPtr = (byte*)eventsBuffer.GetUnsafeReadOnlyPtr();
                while (TestEntityPolymorphicEventManager.ExecuteNextEvent(bufferPtr, eventsBuffer.Length, ref readIndex, ref eventReceiver, 3))
                { }
            }
        }
    }
    #endregion
}