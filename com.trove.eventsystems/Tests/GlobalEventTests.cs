
using Unity.Entities;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Trove.EventSystems.Tests
{
    [TestFixture]
    public class GlobalEventTests 
    {
        public const int EventsPerSystemOrThread = 5;
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
        public void GlobalEventTest1()
        {
            NativeHashMap<int, int> eventsCounter_MainThread = new NativeHashMap<int, int>(10, Allocator.Persistent);
            NativeHashMap<int, int> eventsCounter_SingleJob = new NativeHashMap<int, int>(10, Allocator.Persistent);
            NativeHashMap<int, int> eventsCounter_Parallel1 = new NativeHashMap<int, int>(10, Allocator.Persistent);
            NativeHashMap<int, int> eventsCounter_Parallel2 = new NativeHashMap<int, int>(10, Allocator.Persistent);
            NativeHashMap<int, int> eventsCounter_Parallel3 = new NativeHashMap<int, int>(10, Allocator.Persistent);

            Entity mainThread_SingletonEntity = EventTestUtilities.CreateTestEntity(World.EntityManager);
            Entity singleJob_SingletonEntity = EventTestUtilities.CreateTestEntity(World.EntityManager);
            Entity parallelJob_SingletonEntity = EventTestUtilities.CreateTestEntity(World.EntityManager);
            World.EntityManager.AddComponentData(mainThread_SingletonEntity, new GlobalEventTests_MainThreadReaderSystem.Singleton
            {
                EventsCounter = eventsCounter_MainThread,
            });
            World.EntityManager.AddComponentData(singleJob_SingletonEntity, new GlobalEventTests_SingleJobReaderSystem.Singleton
            {
                EventsCounter = eventsCounter_SingleJob,
            });
            World.EntityManager.AddComponentData(parallelJob_SingletonEntity, new GlobalEventTests_ParallelJobReaderSystem.Singleton
            {
                EventsCounter1 = eventsCounter_Parallel1,
                EventsCounter2 = eventsCounter_Parallel2,
                EventsCounter3 = eventsCounter_Parallel3,
            });

            World.Update();
            World.EntityManager.CompleteAllTrackedJobs();

            bool gotSingleton1 = EventTestUtilities.TryGetSingleton(World.EntityManager, out GlobalEventTests_MainThreadReaderSystem.Singleton s1);
            Assert.IsTrue(gotSingleton1);
            {
                Assert.AreEqual(EventsPerSystemOrThread, s1.EventsCounter[MainThreadQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s1.EventsCounter[MainThreadStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s1.EventsCounter[SingleJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s1.EventsCounter[SingleJobStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s1.EventsCounter[ParallelJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s1.EventsCounter[ParallelJobStreamEventKey]);
            }

            bool gotSingleton2 = EventTestUtilities.TryGetSingleton(World.EntityManager, out GlobalEventTests_SingleJobReaderSystem.Singleton s2);
            Assert.IsTrue(gotSingleton2);
            {
                Assert.AreEqual(EventsPerSystemOrThread, s2.EventsCounter[MainThreadQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s2.EventsCounter[MainThreadStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s2.EventsCounter[SingleJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s2.EventsCounter[SingleJobStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s2.EventsCounter[ParallelJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s2.EventsCounter[ParallelJobStreamEventKey]);
            }

            bool gotSingleton3 = EventTestUtilities.TryGetSingleton(World.EntityManager, out GlobalEventTests_ParallelJobReaderSystem.Singleton s3);
            Assert.IsTrue(gotSingleton3);
            {
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter1[MainThreadQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter1[MainThreadStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter1[SingleJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter1[SingleJobStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter1[ParallelJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter1[ParallelJobStreamEventKey]);

                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter2[MainThreadQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter2[MainThreadStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter2[SingleJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter2[SingleJobStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter2[ParallelJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter2[ParallelJobStreamEventKey]);

                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter3[MainThreadQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter3[MainThreadStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter3[SingleJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter3[SingleJobStreamEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter3[ParallelJobQueueEventKey]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter3[ParallelJobStreamEventKey]);
            }

            World.Unmanaged.GetExistingSystemState<GlobalEventTests_MainThreadQueueWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_MainThreadStreamWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_SingleJobQueueWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_SingleJobStreamWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_ParallelJobQueueWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_ParallelJobStreamWriterSystem>().Enabled = false;

            World.Update();
            World.EntityManager.CompleteAllTrackedJobs();

            EventTestUtilities.TryGetSingleton(World.EntityManager, out TestGlobalEventsSingleton globalEventsSingleton);
            Assert.AreEqual(0, globalEventsSingleton.ReadEventsList.Length);

            World.Unmanaged.GetExistingSystemState<GlobalEventTests_MainThreadQueueWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_MainThreadStreamWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_SingleJobQueueWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_SingleJobStreamWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_ParallelJobQueueWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<GlobalEventTests_ParallelJobStreamWriterSystem>().Enabled = true;

            eventsCounter_MainThread.Dispose();
            eventsCounter_SingleJob.Dispose();
            eventsCounter_Parallel1.Dispose();
            eventsCounter_Parallel2.Dispose();
            eventsCounter_Parallel3.Dispose();
        }
    }

    #region EventWriters
    [UpdateBefore(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_MainThreadQueueWriterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();
            NativeQueue<TestGlobalEvent> eventsQueue = singleton.QueueEventsManager.CreateWriter();

            for (int i = 0; i < GlobalEventTests.EventsPerSystemOrThread; i++)
            {
                eventsQueue.Enqueue(new TestGlobalEvent
                {
                    Val = GlobalEventTests.MainThreadQueueEventKey,
                });
            }
        }
    }

    [UpdateBefore(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_MainThreadStreamWriterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();
            GlobalStreamEventsManager<TestGlobalEvent>.Writer eventsStream = singleton.StreamEventsManager.CreateWriter(1);

            eventsStream.BeginForEachIndex(0);
            for (int i = 0; i < GlobalEventTests.EventsPerSystemOrThread; i++)
            {
                eventsStream.Write(new TestGlobalEvent
                {
                    Val = GlobalEventTests.MainThreadStreamEventKey,
                });
            }
            eventsStream.EndForEachIndex();
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_SingleJobQueueWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();

            state.Dependency = new WriteJob
            {
                EventsQueue = singleton.QueueEventsManager.CreateWriter(),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJob
        {
            public NativeQueue<TestGlobalEvent> EventsQueue;

            public void Execute()
            {
                for (int i = 0; i < GlobalEventTests.EventsPerSystemOrThread; i++)
                {
                    EventsQueue.Enqueue(new TestGlobalEvent
                    {
                        Val = GlobalEventTests.SingleJobQueueEventKey,
                    });
                }
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_SingleJobStreamWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();

            state.Dependency = new WriteJob
            {
                EventsStream = singleton.StreamEventsManager.CreateWriter(1),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJob
        {
            public GlobalStreamEventsManager<TestGlobalEvent>.Writer EventsStream;

            public void Execute()
            {
                EventsStream.BeginForEachIndex(0);
                for (int i = 0; i < GlobalEventTests.EventsPerSystemOrThread; i++)
                {
                    EventsStream.Write(new TestGlobalEvent
                    {
                        Val = GlobalEventTests.SingleJobStreamEventKey,
                    });
                }
                EventsStream.EndForEachIndex();
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_ParallelJobQueueWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();

            state.Dependency = new WriteJob
            {
                EventsQueue = singleton.QueueEventsManager.CreateWriter().AsParallelWriter(),
            }.Schedule(GlobalEventTests.ParallelCount, 1, state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJobParallelFor
        {
            public NativeQueue<TestGlobalEvent>.ParallelWriter EventsQueue;

            public void Execute(int index)
            {
                for (int i = 0; i < GlobalEventTests.EventsPerSystemOrThread; i++)
                {
                    EventsQueue.Enqueue(new TestGlobalEvent
                    {
                        Val = GlobalEventTests.ParallelJobQueueEventKey,
                    });
                }
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_ParallelJobStreamWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();

            state.Dependency = new WriteJob
            {
                EventsStream = singleton.StreamEventsManager.CreateWriter(GlobalEventTests.ParallelCount),
            }.Schedule(GlobalEventTests.ParallelCount, 1, state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJobParallelFor
        {
            public GlobalStreamEventsManager<TestGlobalEvent>.Writer EventsStream;

            public void Execute(int index)
            {
                EventsStream.BeginForEachIndex(index);
                for (int i = 0; i < GlobalEventTests.EventsPerSystemOrThread; i++)
                {
                    EventsStream.Write(new TestGlobalEvent
                    {
                        Val = GlobalEventTests.ParallelJobStreamEventKey,
                    });
                }
                EventsStream.EndForEachIndex();
            }
        }
    }
    #endregion

    #region EventReaders
    [UpdateAfter(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_MainThreadReaderSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public NativeHashMap<int, int> EventsCounter;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
            state.RequireForUpdate<Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.QueryBuilder().WithAll<TestGlobalEventsSingleton>().Build().CompleteDependency();
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();
            Singleton counterSingleton = SystemAPI.GetSingleton<Singleton>();

            counterSingleton.EventsCounter.Clear();
            for (int i = 0; i < singleton.ReadEventsList.Length; i++)
            {
                TestGlobalEvent e = singleton.ReadEventsList[i];
                EventTestUtilities.AddEventToCounter(ref counterSingleton.EventsCounter, e.Val);
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_SingleJobReaderSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public NativeHashMap<int, int> EventsCounter;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
            state.RequireForUpdate<Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();
            Singleton counterSingleton = SystemAPI.GetSingleton<Singleton>();

            counterSingleton.EventsCounter.Clear();
            state.Dependency = new ReadJob
            {
                EventsCounter = counterSingleton.EventsCounter,
                EventsList = singleton.ReadEventsList,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public struct ReadJob : IJob
        {
            public NativeHashMap<int, int> EventsCounter;
            [ReadOnly]
            public NativeList<TestGlobalEvent> EventsList;

            public void Execute()
            {
                for (int i = 0; i < EventsList.Length; i++)
                {
                    TestGlobalEvent e = EventsList[i];
                    EventTestUtilities.AddEventToCounter(ref EventsCounter, e.Val);
                }
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(TestGlobalEventSystem))]
    public partial struct GlobalEventTests_ParallelJobReaderSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public NativeHashMap<int, int> EventsCounter1;
            public NativeHashMap<int, int> EventsCounter2;
            public NativeHashMap<int, int> EventsCounter3;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestGlobalEventsSingleton>();
            state.RequireForUpdate<Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestGlobalEventsSingleton singleton = SystemAPI.GetSingleton<TestGlobalEventsSingleton>();
            Singleton counterSingleton = SystemAPI.GetSingleton<Singleton>();

            counterSingleton.EventsCounter1.Clear();
            counterSingleton.EventsCounter2.Clear();
            counterSingleton.EventsCounter3.Clear();
            state.Dependency = new ReadJob
            {
                EventsCounter1 = counterSingleton.EventsCounter1,
                EventsCounter2 = counterSingleton.EventsCounter2,
                EventsCounter3 = counterSingleton.EventsCounter3,
                EventsList = singleton.ReadEventsList,
            }.Schedule(GlobalEventTests.ParallelCount, 1, state.Dependency);
        }

        [BurstCompile]
        public struct ReadJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeHashMap<int, int> EventsCounter1;
            [NativeDisableParallelForRestriction]
            public NativeHashMap<int, int> EventsCounter2;
            [NativeDisableParallelForRestriction]
            public NativeHashMap<int, int> EventsCounter3;
            [ReadOnly]
            public NativeList<TestGlobalEvent> EventsList;

            public void Execute(int index)
            {
                NativeHashMap<int, int> targetCounter = default;
                switch (index)
                {
                    case 0:
                        targetCounter = EventsCounter1;
                        break;
                    case 1:
                        targetCounter = EventsCounter2;
                        break;
                    case 2:
                        targetCounter = EventsCounter3;
                        break;
                }

                for (int i = 0; i < EventsList.Length; i++)
                {
                    TestGlobalEvent e = EventsList[i];
                    EventTestUtilities.AddEventToCounter(ref targetCounter, e.Val);
                }
            }
        }
    }
    #endregion
}