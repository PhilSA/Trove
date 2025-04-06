
using Unity.Entities;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Trove.EventSystems.Tests
{
    [TestFixture]
    public class EntityEventTests
    {
        public const int EventsPerSystemOrThreadForR1 = 3;
        public const int EventsPerSystemOrThreadForR2 = 1;
        public const int ParallelCount = 3;

        public const int MainThreadQueueEventKey = 1;
        public const int MainThreadStreamEventKey = 2;
        public const int SingleJobQueueEventKey = 3;
        public const int SingleJobStreamEventKey = 4;
        public const int ParallelJobQueueEventKey = 5;
        public const int ParallelJobStreamEventKey = 6;

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
            World.EntityManager.AddComponentData(receiverEntity1, new HasTestEntityEvents());
            World.EntityManager.AddComponentData(receiverEntity2, new HasTestEntityEvents());
            World.EntityManager.SetComponentEnabled<HasTestEntityEvents>(receiverEntity1, false);
            World.EntityManager.SetComponentEnabled<HasTestEntityEvents>(receiverEntity2, false);
            World.EntityManager.AddBuffer<TestEntityEventBufferElement>(receiverEntity1);
            World.EntityManager.AddBuffer<TestEntityEventBufferElement>(receiverEntity2);

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

            Assert.IsTrue(World.EntityManager.IsComponentEnabled<HasTestEntityEvents>(receiverEntity1));
            Assert.IsTrue(World.EntityManager.IsComponentEnabled<HasTestEntityEvents>(receiverEntity2));

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

            World.Unmanaged.GetExistingSystemState<EntityEventTests_MainThreadQueueWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_MainThreadStreamWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_SingleJobQueueWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_SingleJobStreamWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_ParallelJobQueueWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_ParallelJobStreamWriterSystem>().Enabled = false;

            World.Update();
            World.EntityManager.CompleteAllTrackedJobs();

            Assert.IsFalse(World.EntityManager.IsComponentEnabled<HasTestEntityEvents>(receiverEntity1));
            Assert.IsFalse(World.EntityManager.IsComponentEnabled<HasTestEntityEvents>(receiverEntity2));
            Assert.AreEqual(0, World.EntityManager.GetBuffer<TestEntityEventBufferElement>(receiverEntity1).Length);
            Assert.AreEqual(0, World.EntityManager.GetBuffer<TestEntityEventBufferElement>(receiverEntity2).Length);

            World.Unmanaged.GetExistingSystemState<EntityEventTests_MainThreadQueueWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_MainThreadStreamWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_SingleJobQueueWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_SingleJobStreamWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_ParallelJobQueueWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<EntityEventTests_ParallelJobStreamWriterSystem>().Enabled = true;
        }
    }

    #region EventWriters
    [UpdateBefore(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_MainThreadQueueWriterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            TestEntityEventsSingleton singleton = SystemAPI.GetSingleton<TestEntityEventsSingleton>();
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();
            NativeQueue<TestEntityEventForEntity> eventsQueue = singleton.QueueEventsManager.CreateWriter();

            for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR1; i++)
            {
                eventsQueue.Enqueue(new TestEntityEventForEntity
                {
                    AffectedEntity = testSingleton.ReceiverEntity1,
                    Event = new TestEntityEventBufferElement
                    {
                        Val = EntityEventTests.MainThreadQueueEventKey,
                    },
                });
            }

            for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR2; i++)
            {
                eventsQueue.Enqueue(new TestEntityEventForEntity
                {
                    AffectedEntity = testSingleton.ReceiverEntity2,
                    Event = new TestEntityEventBufferElement
                    {
                        Val = EntityEventTests.MainThreadQueueEventKey,
                    },
                });
            }
        }
    }

    [UpdateBefore(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_MainThreadStreamWriterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            TestEntityEventsSingleton singleton = SystemAPI.GetSingleton<TestEntityEventsSingleton>();
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();
            EntityStreamEventsManager<TestEntityEventForEntity, TestEntityEventBufferElement>.Writer eventsStream = singleton.StreamEventsManager.CreateWriter(1);

            eventsStream.BeginForEachIndex(0);

            for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR1; i++)
            {
                eventsStream.Write(new TestEntityEventForEntity
                {
                    AffectedEntity = testSingleton.ReceiverEntity1,
                    Event = new TestEntityEventBufferElement
                    {
                        Val = EntityEventTests.MainThreadStreamEventKey,
                    },
                });
            }

            for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR2; i++)
            {
                eventsStream.Write(new TestEntityEventForEntity
                {
                    AffectedEntity = testSingleton.ReceiverEntity2,
                    Event = new TestEntityEventBufferElement
                    {
                        Val = EntityEventTests.MainThreadStreamEventKey,
                    },
                });
            }

            eventsStream.EndForEachIndex();
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_SingleJobQueueWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestEntityEventsSingleton singleton = SystemAPI.GetSingleton<TestEntityEventsSingleton>();
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();

            state.Dependency = new WriteJob
            {
                EventsQueue = singleton.QueueEventsManager.CreateWriter(),
                TestSingleton = testSingleton,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJob
        {
            public NativeQueue<TestEntityEventForEntity> EventsQueue;
            public EntityEventTestSingleton TestSingleton;

            public void Execute()
            {
                for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR1; i++)
                {
                    EventsQueue.Enqueue(new TestEntityEventForEntity
                    {
                        AffectedEntity = TestSingleton.ReceiverEntity1,
                        Event = new TestEntityEventBufferElement
                        {
                            Val = EntityEventTests.SingleJobQueueEventKey,
                        },
                    });
                }

                for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR2; i++)
                {
                    EventsQueue.Enqueue(new TestEntityEventForEntity
                    {
                        AffectedEntity = TestSingleton.ReceiverEntity2,
                        Event = new TestEntityEventBufferElement
                        {
                            Val = EntityEventTests.SingleJobQueueEventKey,
                        },
                    });
                }
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_SingleJobStreamWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestEntityEventsSingleton singleton = SystemAPI.GetSingleton<TestEntityEventsSingleton>();
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();

            state.Dependency = new WriteJob
            {
                EventsStream = singleton.StreamEventsManager.CreateWriter(1),
                TestSingleton = testSingleton,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJob
        {
            public EntityStreamEventsManager<TestEntityEventForEntity, TestEntityEventBufferElement>.Writer EventsStream;
            public EntityEventTestSingleton TestSingleton;

            public void Execute()
            {
                EventsStream.BeginForEachIndex(0);

                for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR1; i++)
                {
                    EventsStream.Write(new TestEntityEventForEntity
                    {
                        AffectedEntity = TestSingleton.ReceiverEntity1,
                        Event = new TestEntityEventBufferElement
                        {
                            Val = EntityEventTests.SingleJobStreamEventKey,
                        },
                    });
                }

                for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR2; i++)
                {
                    EventsStream.Write(new TestEntityEventForEntity
                    {
                        AffectedEntity = TestSingleton.ReceiverEntity2,
                        Event = new TestEntityEventBufferElement
                        {
                            Val = EntityEventTests.SingleJobStreamEventKey,
                        },
                    });
                }

                EventsStream.EndForEachIndex();
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_ParallelJobQueueWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestEntityEventsSingleton singleton = SystemAPI.GetSingleton<TestEntityEventsSingleton>();
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();

            state.Dependency = new WriteJob
            {
                EventsQueue = singleton.QueueEventsManager.CreateWriter().AsParallelWriter(),
                TestSingleton = testSingleton,
            }.Schedule(EntityEventTests.ParallelCount, 1, state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJobParallelFor
        {
            public NativeQueue<TestEntityEventForEntity>.ParallelWriter EventsQueue;
            public EntityEventTestSingleton TestSingleton;

            public void Execute(int index)
            {
                for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR1; i++)
                {
                    EventsQueue.Enqueue(new TestEntityEventForEntity
                    {
                        AffectedEntity = TestSingleton.ReceiverEntity1,
                        Event = new TestEntityEventBufferElement
                        {
                            Val = EntityEventTests.ParallelJobQueueEventKey,
                        },
                    });
                }

                for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR2; i++)
                {
                    EventsQueue.Enqueue(new TestEntityEventForEntity
                    {
                        AffectedEntity = TestSingleton.ReceiverEntity2,
                        Event = new TestEntityEventBufferElement
                        {
                            Val = EntityEventTests.ParallelJobQueueEventKey,
                        },
                    });
                }
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_ParallelJobStreamWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestEntityEventsSingleton singleton = SystemAPI.GetSingleton<TestEntityEventsSingleton>();
            EntityEventTestSingleton testSingleton = SystemAPI.GetSingleton<EntityEventTestSingleton>();

            state.Dependency = new WriteJob
            {
                EventsStream = singleton.StreamEventsManager.CreateWriter(EntityEventTests.ParallelCount),
                TestSingleton = testSingleton,
            }.Schedule(EntityEventTests.ParallelCount, 1, state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJobParallelFor
        {
            public EntityStreamEventsManager<TestEntityEventForEntity, TestEntityEventBufferElement>.Writer EventsStream;
            public EntityEventTestSingleton TestSingleton;

            public void Execute(int index)
            {
                EventsStream.BeginForEachIndex(index);

                for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR1; i++)
                {
                    EventsStream.Write(new TestEntityEventForEntity
                    {
                        AffectedEntity = TestSingleton.ReceiverEntity1,
                        Event = new TestEntityEventBufferElement
                        {
                            Val = EntityEventTests.ParallelJobStreamEventKey,
                        },
                    });
                }

                for (int i = 0; i < EntityEventTests.EventsPerSystemOrThreadForR2; i++)
                {
                    EventsStream.Write(new TestEntityEventForEntity
                    {
                        AffectedEntity = TestSingleton.ReceiverEntity2,
                        Event = new TestEntityEventBufferElement
                        {
                            Val = EntityEventTests.ParallelJobStreamEventKey,
                        },
                    });
                }

                EventsStream.EndForEachIndex();
            }
        }
    }
    #endregion

    #region EventReaders
    [UpdateAfter(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_MainThreadReaderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
            state.RequireForUpdate<EntityEventTestSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach(var (eventReceiver, eventsBuffer, entity) in SystemAPI.Query<RefRW<EntityEventReceiver>, DynamicBuffer<TestEntityEventBufferElement>>().WithEntityAccess())
            {
                for (int i = 0; i < eventsBuffer.Length; i++)
                {
                    TestEntityEventBufferElement e = eventsBuffer[i];
                    switch (e.Val)
                    {
                        case 1:
                            eventReceiver.ValueRW.MainThread_EventCounterVal1++;
                            break;
                        case 2:
                            eventReceiver.ValueRW.MainThread_EventCounterVal2++;
                            break;
                        case 3:
                            eventReceiver.ValueRW.MainThread_EventCounterVal3++;
                            break;
                        case 4:
                            eventReceiver.ValueRW.MainThread_EventCounterVal4++;
                            break;
                        case 5:
                            eventReceiver.ValueRW.MainThread_EventCounterVal5++;
                            break;
                        case 6:
                            eventReceiver.ValueRW.MainThread_EventCounterVal6++;
                            break;
                    }
                }
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_SingleJobReaderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
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
            public void Execute(Entity entity, ref EntityEventReceiver eventReceiver, in DynamicBuffer<TestEntityEventBufferElement> eventsBuffer)
            {
                for (int i = 0; i < eventsBuffer.Length; i++)
                {
                    TestEntityEventBufferElement e = eventsBuffer[i];
                    switch (e.Val)
                    {
                        case 1:
                            eventReceiver.SingleJob_EventCounterVal1++;
                            break;
                        case 2:
                            eventReceiver.SingleJob_EventCounterVal2++;
                            break;
                        case 3:
                            eventReceiver.SingleJob_EventCounterVal3++;
                            break;
                        case 4:
                            eventReceiver.SingleJob_EventCounterVal4++;
                            break;
                        case 5:
                            eventReceiver.SingleJob_EventCounterVal5++;
                            break;
                        case 6:
                            eventReceiver.SingleJob_EventCounterVal6++;
                            break;
                    }
                }
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(TestEntityEventSystem))]
    public partial struct EntityEventTests_ParallelJobReaderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestEntityEventsSingleton>();
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
            public void Execute(Entity entity, ref EntityEventReceiver eventReceiver, in DynamicBuffer<TestEntityEventBufferElement> eventsBuffer)
            {
                for (int i = 0; i < eventsBuffer.Length; i++)
                {
                    TestEntityEventBufferElement e = eventsBuffer[i];
                    switch (e.Val)
                    {
                        case 1:
                            eventReceiver.ParallelJob_EventCounterVal1++;
                            break;
                        case 2:
                            eventReceiver.ParallelJob_EventCounterVal2++;
                            break;
                        case 3:
                            eventReceiver.ParallelJob_EventCounterVal3++;
                            break;
                        case 4:
                            eventReceiver.ParallelJob_EventCounterVal4++;
                            break;
                        case 5:
                            eventReceiver.ParallelJob_EventCounterVal5++;
                            break;
                        case 6:
                            eventReceiver.ParallelJob_EventCounterVal6++;
                            break;
                    }
                }
            }
        }
    }
    #endregion
}