#if HAS_TROVE_POLYMORPHICSTRUCTS
using Unity.Entities;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace Trove.EventSystems.Tests
{
    [TestFixture]
    public class GlobalPolymorphicEventTests 
    {
        public const int EventsPerSystemOrThread = 5;
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
        public void GlobalPolymorphicEventTest1()
        {
            NativeHashMap<int, int> eventsCounter_MainThread = new NativeHashMap<int, int>(10, Allocator.Persistent);
            NativeHashMap<int, int> eventsCounter_SingleJob = new NativeHashMap<int, int>(10, Allocator.Persistent);
            NativeHashMap<int, int> eventsCounter_Parallel1 = new NativeHashMap<int, int>(10, Allocator.Persistent);
            NativeHashMap<int, int> eventsCounter_Parallel2 = new NativeHashMap<int, int>(10, Allocator.Persistent);
            NativeHashMap<int, int> eventsCounter_Parallel3 = new NativeHashMap<int, int>(10, Allocator.Persistent);

            Entity mainThread_SingletonEntity = EventTestUtilities.CreateTestEntity(World.EntityManager);
            Entity singleJob_SingletonEntity = EventTestUtilities.CreateTestEntity(World.EntityManager);
            Entity parallelJob_SingletonEntity = EventTestUtilities.CreateTestEntity(World.EntityManager);
            World.EntityManager.AddComponentData(mainThread_SingletonEntity, new GlobalPolymorphicEventTests_MainThreadReaderSystem.Singleton
            {
                EventsCounter = eventsCounter_MainThread,
            });
            World.EntityManager.AddComponentData(singleJob_SingletonEntity, new GlobalPolymorphicEventTests_SingleJobReaderSystem.Singleton
            {
                EventsCounter = eventsCounter_SingleJob,
            });
            World.EntityManager.AddComponentData(parallelJob_SingletonEntity, new GlobalPolymorphicEventTests_ParallelJobReaderSystem.Singleton
            {
                EventsCounter1 = eventsCounter_Parallel1,
                EventsCounter2 = eventsCounter_Parallel2,
                EventsCounter3 = eventsCounter_Parallel3,
            });

            World.Update();
            World.EntityManager.CompleteAllTrackedJobs();

            bool gotSingleton1 = EventTestUtilities.TryGetSingleton(World.EntityManager, out GlobalPolymorphicEventTests_MainThreadReaderSystem.Singleton s1);
            Assert.IsTrue(gotSingleton1);
            {
                Assert.AreEqual(EventsPerSystemOrThread, s1.EventsCounter[MainThreadStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s1.EventsCounter[MainThreadStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread, s1.EventsCounter[SingleJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s1.EventsCounter[SingleJobStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s1.EventsCounter[ParallelJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s1.EventsCounter[ParallelJobStreamEventKeyB]);
            }

            bool gotSingleton2 = EventTestUtilities.TryGetSingleton(World.EntityManager, out GlobalPolymorphicEventTests_SingleJobReaderSystem.Singleton s2);
            Assert.IsTrue(gotSingleton2);
            {
                Assert.AreEqual(EventsPerSystemOrThread, s2.EventsCounter[MainThreadStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s2.EventsCounter[MainThreadStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread, s2.EventsCounter[SingleJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s2.EventsCounter[SingleJobStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s2.EventsCounter[ParallelJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s2.EventsCounter[ParallelJobStreamEventKeyB]);
            }

            bool gotSingleton3 = EventTestUtilities.TryGetSingleton(World.EntityManager, out GlobalPolymorphicEventTests_ParallelJobReaderSystem.Singleton s3);
            Assert.IsTrue(gotSingleton3);
            {
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter1[MainThreadStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter1[MainThreadStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter1[SingleJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter1[SingleJobStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter1[ParallelJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter1[ParallelJobStreamEventKeyB]);

                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter2[MainThreadStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter2[MainThreadStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter2[SingleJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter2[SingleJobStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter2[ParallelJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter2[ParallelJobStreamEventKeyB]);

                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter3[MainThreadStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter3[MainThreadStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter3[SingleJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread, s3.EventsCounter3[SingleJobStreamEventKeyB]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter3[ParallelJobStreamEventKeyA]);
                Assert.AreEqual(EventsPerSystemOrThread * ParallelCount, s3.EventsCounter3[ParallelJobStreamEventKeyB]);
            }

            World.Unmanaged.GetExistingSystemState<GlobalPolymorphicEventTests_MainThreadStreamWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<GlobalPolymorphicEventTests_SingleJobStreamWriterSystem>().Enabled = false;
            World.Unmanaged.GetExistingSystemState<GlobalPolymorphicEventTests_ParallelJobStreamWriterSystem>().Enabled = false;

            World.Update();
            World.EntityManager.CompleteAllTrackedJobs();

            EventTestUtilities.TryGetSingleton(World.EntityManager, out TestIGlobalPolyByteArrayEventsSingleton globalEventsSingleton);
            Assert.AreEqual(0, globalEventsSingleton.ReadEventsList.Length);

            World.Unmanaged.GetExistingSystemState<GlobalPolymorphicEventTests_MainThreadStreamWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<GlobalPolymorphicEventTests_SingleJobStreamWriterSystem>().Enabled = true;
            World.Unmanaged.GetExistingSystemState<GlobalPolymorphicEventTests_ParallelJobStreamWriterSystem>().Enabled = true;

            eventsCounter_MainThread.Dispose();
            eventsCounter_SingleJob.Dispose();
            eventsCounter_Parallel1.Dispose();
            eventsCounter_Parallel2.Dispose();
            eventsCounter_Parallel3.Dispose();
        }
    }

    #region EventWriters
    [UpdateBefore(typeof(TestGlobalPolymorphicEventSystem))]
    public partial struct GlobalPolymorphicEventTests_MainThreadStreamWriterSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestIGlobalPolyByteArrayEventsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            TestIGlobalPolyByteArrayEventsSingleton singleton = SystemAPI.GetSingleton<TestIGlobalPolyByteArrayEventsSingleton>();
            GlobalPolyByteArrayStreamEventsManager<PolyTestGlobalPolymorphicEvent>.Writer eventsStream = singleton.StreamEventsManager.CreateWriter(1);

            eventsStream.BeginForEachIndex(0);
            for (int i = 0; i < GlobalPolymorphicEventTests.EventsPerSystemOrThread; i++)
            {
                eventsStream.Write(new TestGlobalPolymorphicEventA
                {
                    Val = GlobalPolymorphicEventTests.MainThreadStreamEventKeyA,
                });
                eventsStream.Write(new TestGlobalPolymorphicEventB
                {
                    Val1 = GlobalPolymorphicEventTests.MainThreadStreamEventKeyB,
                    Val2 = GlobalPolymorphicEventTests.MainThreadStreamEventKeyB,
                    Val3 = GlobalPolymorphicEventTests.MainThreadStreamEventKeyB,
                });
            }
            eventsStream.EndForEachIndex();
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestGlobalPolymorphicEventSystem))]
    public partial struct GlobalPolymorphicEventTests_SingleJobStreamWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestIGlobalPolyByteArrayEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestIGlobalPolyByteArrayEventsSingleton singleton = SystemAPI.GetSingleton<TestIGlobalPolyByteArrayEventsSingleton>();

            state.Dependency = new WriteJob
            {
                EventsStream = singleton.StreamEventsManager.CreateWriter(1),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJob
        {
            public GlobalPolyByteArrayStreamEventsManager<PolyTestGlobalPolymorphicEvent>.Writer EventsStream;

            public void Execute()
            {
                EventsStream.BeginForEachIndex(0);
                for (int i = 0; i < GlobalPolymorphicEventTests.EventsPerSystemOrThread; i++)
                {
                    EventsStream.Write(new TestGlobalPolymorphicEventA
                    {
                        Val = GlobalPolymorphicEventTests.SingleJobStreamEventKeyA,
                    });
                    EventsStream.Write(new TestGlobalPolymorphicEventB
                    {
                        Val1 = GlobalPolymorphicEventTests.SingleJobStreamEventKeyB,
                        Val2 = GlobalPolymorphicEventTests.SingleJobStreamEventKeyB,
                        Val3 = GlobalPolymorphicEventTests.SingleJobStreamEventKeyB,
                    });
                }
                EventsStream.EndForEachIndex();
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(TestGlobalPolymorphicEventSystem))]
    public partial struct GlobalPolymorphicEventTests_ParallelJobStreamWriterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestIGlobalPolyByteArrayEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestIGlobalPolyByteArrayEventsSingleton singleton = SystemAPI.GetSingleton<TestIGlobalPolyByteArrayEventsSingleton>();

            state.Dependency = new WriteJob
            {
                EventsStream = singleton.StreamEventsManager.CreateWriter(GlobalPolymorphicEventTests.ParallelCount),
            }.Schedule(GlobalPolymorphicEventTests.ParallelCount, 1, state.Dependency);
        }

        [BurstCompile]
        public struct WriteJob : IJobParallelFor
        {
            public GlobalPolyByteArrayStreamEventsManager<PolyTestGlobalPolymorphicEvent>.Writer EventsStream;

            public void Execute(int index)
            {
                EventsStream.BeginForEachIndex(index);
                for (int i = 0; i < GlobalPolymorphicEventTests.EventsPerSystemOrThread; i++)
                {
                    EventsStream.Write(new TestGlobalPolymorphicEventA
                    {
                        Val = GlobalPolymorphicEventTests.ParallelJobStreamEventKeyA,
                    });
                    EventsStream.Write(new TestGlobalPolymorphicEventB
                    {
                        Val1 = GlobalPolymorphicEventTests.ParallelJobStreamEventKeyB,
                        Val2 = GlobalPolymorphicEventTests.ParallelJobStreamEventKeyB,
                        Val3 = GlobalPolymorphicEventTests.ParallelJobStreamEventKeyB,
                    });
                }
                EventsStream.EndForEachIndex();
            }
        }
    }
    #endregion

    #region EventReaders
    [UpdateAfter(typeof(TestGlobalPolymorphicEventSystem))]
    public partial struct GlobalPolymorphicEventTests_MainThreadReaderSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public NativeHashMap<int, int> EventsCounter;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestIGlobalPolyByteArrayEventsSingleton>();
            state.RequireForUpdate<Singleton>();
        }

        public unsafe void OnUpdate(ref SystemState state)
        {
            SystemAPI.QueryBuilder().WithAll<TestIGlobalPolyByteArrayEventsSingleton>().Build().CompleteDependency();
            TestIGlobalPolyByteArrayEventsSingleton singleton = SystemAPI.GetSingleton<TestIGlobalPolyByteArrayEventsSingleton>();
            Singleton counterSingleton = SystemAPI.GetSingleton<Singleton>();

            counterSingleton.EventsCounter.Clear();
            
            NativeList<byte> eventsList = singleton.ReadEventsList;
            PolymorphicObjectNativeListIterator<PolyTestGlobalPolymorphicEvent> iterator = 
                PolymorphicObjectUtilities.GetIterator<PolyTestGlobalPolymorphicEvent>(eventsList);
            while (iterator.GetNext(out PolyTestGlobalPolymorphicEvent e, out _, out _))
            {
                e.Execute(ref counterSingleton.EventsCounter);
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(TestGlobalPolymorphicEventSystem))]
    public partial struct GlobalPolymorphicEventTests_SingleJobReaderSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public NativeHashMap<int, int> EventsCounter;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestIGlobalPolyByteArrayEventsSingleton>();
            state.RequireForUpdate<Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestIGlobalPolyByteArrayEventsSingleton singleton = SystemAPI.GetSingleton<TestIGlobalPolyByteArrayEventsSingleton>();
            Singleton counterSingleton = SystemAPI.GetSingleton<Singleton>();

            counterSingleton.EventsCounter.Clear();
            state.Dependency = new ReadJob
            {
                EventsCounter = counterSingleton.EventsCounter,
                EventsList = singleton.ReadEventsList,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public unsafe struct ReadJob : IJob
        {
            public NativeHashMap<int, int> EventsCounter;
            [ReadOnly]
            public NativeList<byte> EventsList;

            public void Execute()
            {
                PolymorphicObjectNativeListIterator<PolyTestGlobalPolymorphicEvent> iterator = 
                    PolymorphicObjectUtilities.GetIterator<PolyTestGlobalPolymorphicEvent>(EventsList);
                while (iterator.GetNext(out PolyTestGlobalPolymorphicEvent e, out _, out _))
                {
                    e.Execute(ref EventsCounter);
                }
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(TestGlobalPolymorphicEventSystem))]
    public partial struct GlobalPolymorphicEventTests_ParallelJobReaderSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public NativeHashMap<int, int> EventsCounter1;
            public NativeHashMap<int, int> EventsCounter2;
            public NativeHashMap<int, int> EventsCounter3;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TestIGlobalPolyByteArrayEventsSingleton>();
            state.RequireForUpdate<Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TestIGlobalPolyByteArrayEventsSingleton singleton = SystemAPI.GetSingleton<TestIGlobalPolyByteArrayEventsSingleton>();
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
            }.Schedule(GlobalPolymorphicEventTests.ParallelCount, 1, state.Dependency);
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
            public NativeList<byte> EventsList;

            public unsafe void Execute(int index)
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

                PolymorphicObjectNativeListIterator<PolyTestGlobalPolymorphicEvent> iterator = 
                    PolymorphicObjectUtilities.GetIterator<PolyTestGlobalPolymorphicEvent>(EventsList);
                while (iterator.GetNext(out PolyTestGlobalPolymorphicEvent e, out _, out _))
                {
                    e.Execute(ref targetCounter);
                }
            }
        }
    }
    #endregion
}
#endif