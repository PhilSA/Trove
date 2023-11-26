using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Trove.PolymorphicElements;
using Unity.Jobs;
using Unity.Logging;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;

#region Event Definitions
public struct StressTestEventsData
{
    public EventsTest Singleton;
    public ComponentLookup<LocalTransform> LocalTransformLookup;
    public ComponentLookup<URPMaterialPropertyEmissionColor> EmissionColorLookup;
}

[PolymorphicElementsGroup]
public interface IStressTestEvent 
{
    void Execute(ref StressTestEventsData data);
}

[PolymorphicElement]
public partial struct StressTestEvent_SetPosition : IStressTestEvent
{
    public Entity Entity;
    public float3 Position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute(ref StressTestEventsData data)
    {
        RefRW<LocalTransform> transformRef = data.LocalTransformLookup.GetRefRW(Entity);
        if(transformRef.IsValid)
        {
            transformRef.ValueRW.Position = Position;
        }
    }
}

[PolymorphicElement]
public partial struct StressTestEvent_SetRotation : IStressTestEvent
{
    public Entity Entity;
    public quaternion Rotation;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute(ref StressTestEventsData data)
    {
        RefRW<LocalTransform> transformRef = data.LocalTransformLookup.GetRefRW(Entity);
        if (transformRef.IsValid)
        {
            transformRef.ValueRW.Rotation = Rotation;
        }
    }
}

[PolymorphicElement]
public partial struct StressTestEvent_SetScale : IStressTestEvent
{
    public Entity Entity;
    public float Scale;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute(ref StressTestEventsData data)
    {
        RefRW<LocalTransform> transformRef = data.LocalTransformLookup.GetRefRW(Entity);
        if (transformRef.IsValid)
        {
            transformRef.ValueRW.Scale = Scale;
        }
    }
}

[PolymorphicElement]
public partial struct StressTestEvent_SetColor : IStressTestEvent
{
    public Entity Entity;
    public float4 Color;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] 
    public void Execute(ref StressTestEventsData data)
    {
        RefRW<URPMaterialPropertyEmissionColor> colorRef = data.EmissionColorLookup.GetRefRW(Entity);
        if (colorRef.IsValid)
        {
            colorRef.ValueRW.Value = Color;
        }
    }
}
#endregion

[BurstCompile]
public partial struct StressTestEventSetupSystem : ISystem
{
    private bool HasInitialized;

    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EventsTest>();
    }

    [BurstCompile]
    void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        ref EventsTest singleton = ref SystemAPI.GetSingletonRW<EventsTest>().ValueRW;
        if (!singleton.EnableStressTestEventsTest)
            return;

        if(!HasInitialized)
        {
            Entity cubeEntity = state.EntityManager.Instantiate(singleton.CubePrefab);
            singleton.MainCubeInstance = cubeEntity;

            HasInitialized = true;
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(MyEventSystem))]
public partial struct StressTestTransformEventCreatorSystem : ISystem
{
    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EventsTest>();
        state.RequireForUpdate<MyEventSystem.Singleton>();
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        EventsTest singleton = SystemAPI.GetSingleton<EventsTest>();
        if (!singleton.EnableStressTestEventsTest || !state.EntityManager.Exists(singleton.MainCubeInstance))
            return;

        if (singleton.UseParallelEvents)
        {
            int eventsPerThread = singleton.TransformEventsCount / singleton.ParallelThreadCount;
            int eventsSurplus = singleton.TransformEventsCount % singleton.ParallelThreadCount;

            TransformEventParallelJob job = new TransformEventParallelJob
            {
                EventsCount = eventsPerThread,
                EventsSurplus = eventsSurplus,
                Time = (float)SystemAPI.Time.ElapsedTime,
                Singleton = singleton,

                EventWriter = SystemAPI.GetSingleton<MyEventSystem.Singleton>().EventBuffersManager.CreateEventWriterParallel(singleton.ParallelThreadCount, ref state),
            };
            state.Dependency = job.Schedule(singleton.ParallelThreadCount, 1, state.Dependency);
        }
        else
        {
            TransformEventSingleJob job = new TransformEventSingleJob
            {
                EventsCount = singleton.TransformEventsCount,
                Time = (float)SystemAPI.Time.ElapsedTime,
                Singleton = singleton,

                EventWriter = SystemAPI.GetSingleton<MyEventSystem.Singleton>().EventBuffersManager.CreateEventWriterSingle(singleton.TransformEventsCount * 30, ref state),
            };
            state.Dependency = job.Schedule(state.Dependency);


            //JobHandle initDep = state.Dependency;

            //TransformEventSingleJob job1 = new TransformEventSingleJob
            //{
            //    EventsCount = singleton.TransformEventsCount,
            //    Time = (float)SystemAPI.Time.ElapsedTime,
            //    Singleton = singleton,

            //    EventWriter = SystemAPI.GetSingleton<MyEventSystem.Singleton>().EventBuffersManager.CreateEventWriterSingle(singleton.TransformEventsCount * 30, ref state),
            //};
            //JobHandle d1 = job1.Schedule(initDep);

            //TransformEventSingleJob job2 = new TransformEventSingleJob
            //{
            //    EventsCount = singleton.TransformEventsCount,
            //    Time = (float)SystemAPI.Time.ElapsedTime,
            //    Singleton = singleton,

            //    EventWriter = SystemAPI.GetSingleton<MyEventSystem.Singleton>().EventBuffersManager.CreateEventWriterSingle(singleton.TransformEventsCount * 30, ref state),
            //};
            //JobHandle d2 = job2.Schedule(initDep);

            //TransformEventSingleJob job3 = new TransformEventSingleJob
            //{
            //    EventsCount = singleton.TransformEventsCount,
            //    Time = (float)SystemAPI.Time.ElapsedTime,
            //    Singleton = singleton,

            //    EventWriter = SystemAPI.GetSingleton<MyEventSystem.Singleton>().EventBuffersManager.CreateEventWriterSingle(singleton.TransformEventsCount * 30, ref state),
            //};
            //JobHandle d3 = job3.Schedule(initDep);

            //state.Dependency = JobHandle.CombineDependencies(state.Dependency, d1);
            //state.Dependency = JobHandle.CombineDependencies(state.Dependency, d2);
            //state.Dependency = JobHandle.CombineDependencies(state.Dependency, d3);
        }
    }

    [BurstCompile]
    public partial struct TransformEventSingleJob : IJob
    {
        public int EventsCount;
        public float Time;
        public EventsTest Singleton;

        public EventWriterSingle EventWriter;

        public void Execute()
        {
            Random random = Random.CreateFromIndex((uint)(Time * 10000f));

            for (int i = 0; i < EventsCount; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        PolymorphicElementsUtility.AddElement(ref EventWriter, new StressTestEvent_SetPosition
                        {
                            Entity = Singleton.MainCubeInstance,
                            Position = random.NextFloat3(new float3(-3f), new float3(3f)),
                        });
                        break;
                    case 1:
                        PolymorphicElementsUtility.AddElement(ref EventWriter, new StressTestEvent_SetRotation
                        {
                            Entity = Singleton.MainCubeInstance,
                            Rotation = random.NextQuaternionRotation(),
                        });
                        break;
                    case 2:
                        PolymorphicElementsUtility.AddElement(ref EventWriter, new StressTestEvent_SetScale
                        {
                            Entity = Singleton.MainCubeInstance,
                            Scale = random.NextFloat(0.5f, 2f),
                        });
                        break;
                }
            }
        }
    }

    [BurstCompile]
    public partial struct TransformEventParallelJob : IJobParallelFor
    {
        public int EventsCount;
        public int EventsSurplus;
        public float Time;
        public EventsTest Singleton;

        public EventWriterParallel EventWriter;

        public void Execute(int index)
        {
            Random random = Random.CreateFromIndex((uint)(index + (Time * 10000f)));

            int realEventsCount = EventsCount;
            if (index == 0)
            {
                realEventsCount += EventsSurplus;
            }

            EventWriter.BeginForEachIndex(index);
            for (int i = 0; i < realEventsCount; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        PolymorphicElementsUtility.AddStreamElement(ref EventWriter, new StressTestEvent_SetPosition
                        {
                            Entity = Singleton.MainCubeInstance,
                            Position = random.NextFloat3(new float3(-3f), new float3(3f)),
                        });
                        break;
                    case 1:
                        PolymorphicElementsUtility.AddStreamElement(ref EventWriter, new StressTestEvent_SetRotation
                        {
                            Entity = Singleton.MainCubeInstance,
                            Rotation = random.NextQuaternionRotation(),
                        });
                        break;
                    case 2:
                        PolymorphicElementsUtility.AddStreamElement(ref EventWriter, new StressTestEvent_SetScale
                        {
                            Entity = Singleton.MainCubeInstance,
                            Scale = random.NextFloat(0.5f, 2f),
                        });
                        break;
                }
            }
            EventWriter.EndForEachIndex();
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MyEventSystem))]
public partial struct StressTestColorEventCreatorSystem : ISystem
{
    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EventsTest>();
        state.RequireForUpdate<MyEventSystem.Singleton>();
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        EventsTest singleton = SystemAPI.GetSingleton<EventsTest>();
        if (!singleton.EnableStressTestEventsTest || !state.EntityManager.Exists(singleton.MainCubeInstance))
            return;

        if (singleton.UseParallelEvents)
        {
            int eventsPerThread = singleton.ColorEventsCount / singleton.ParallelThreadCount;
            int eventsSurplus = singleton.ColorEventsCount % singleton.ParallelThreadCount;

            ColorEventsParallelJob job = new ColorEventsParallelJob
            {
                EventsCount = eventsPerThread,
                EventsSurplus = eventsSurplus,
                Time = (float)SystemAPI.Time.ElapsedTime,
                Singleton = singleton,

                EventWriter = SystemAPI.GetSingleton<MyEventSystem.Singleton>().EventBuffersManager.CreateEventWriterParallel(singleton.ParallelThreadCount, ref state),
            };
            state.Dependency = job.Schedule(singleton.ParallelThreadCount, 1, state.Dependency);
        }
        else 
        {
            ColorEventsSingleJob job = new ColorEventsSingleJob
            {
                EventsCount = singleton.ColorEventsCount,
                Time = (float)SystemAPI.Time.ElapsedTime,
                Singleton = singleton,

                EventWriter = SystemAPI.GetSingleton<MyEventSystem.Singleton>().EventBuffersManager.CreateEventWriterSingle(singleton.ColorEventsCount * 30, ref state),
            };
            state.Dependency = job.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ColorEventsSingleJob : IJob
    {
        public int EventsCount;
        public float Time;
        public EventsTest Singleton;

        public EventWriterSingle EventWriter;

        const float ColorStrength = 1f;

        public void Execute()
        {
            Random random = Random.CreateFromIndex((uint)(Time * 10000f));

            for (int i = 0; i < EventsCount; i++)
            {
                PolymorphicElementsUtility.AddElement(ref EventWriter, new StressTestEvent_SetColor
                {
                    Entity = Singleton.MainCubeInstance,
                    Color = new float4(random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), 1f),
                });
            }
        }
    }

    [BurstCompile]
    public partial struct ColorEventsParallelJob : IJobParallelFor
    {
        public int EventsCount;
        public int EventsSurplus;
        public float Time;
        public EventsTest Singleton;

        public EventWriterParallel EventWriter;

        const float ColorStrength = 1f;

        public void Execute(int index)
        {
            Random random = Random.CreateFromIndex((uint)(index + (Time * 10000f)));

            int realEventsCount = EventsCount;
            if (index == 0)
            {
                realEventsCount += EventsSurplus;
            }

            EventWriter.BeginForEachIndex(index);
            for (int i = 0; i < realEventsCount; i++)
            {
                PolymorphicElementsUtility.AddStreamElement(ref EventWriter, new StressTestEvent_SetColor
                {
                    Entity = Singleton.MainCubeInstance,
                    Color = new float4(random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), 1f),
                });
            }
            EventWriter.EndForEachIndex();
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct MyEventSystem : ISystem
{
    public struct Singleton : IComponentData
    {
        public EventBuffersManager EventBuffersManager;
    }

    private EventBuffersManagerData _eventBuffersManagerData;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Singleton>();
        state.RequireForUpdate<EventsTest>();
        _eventBuffersManagerData = new EventBuffersManagerData(ref state);
        state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), new Singleton { EventBuffersManager = new EventBuffersManager(ref _eventBuffersManagerData) });
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _eventBuffersManagerData.DisposeAll();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _eventBuffersManagerData.BeforeEventsProcessed(ref state);

        EventsTest eventTestSingleton = SystemAPI.GetSingleton<EventsTest>();
        ExecuteEventsJob job = new ExecuteEventsJob
        {
            EventTestSingleton = eventTestSingleton,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            EmissionColorLookup = SystemAPI.GetComponentLookup<URPMaterialPropertyEmissionColor>(false),
            EventBuffersManagerData = _eventBuffersManagerData,
        };
        state.Dependency = job.Schedule(state.Dependency);

        _eventBuffersManagerData.AfterEventsProcessed(ref state);
    }

    [BurstCompile]
    public struct ExecuteEventsJob : IJob
    {
        public EventsTest EventTestSingleton;
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        public ComponentLookup<URPMaterialPropertyEmissionColor> EmissionColorLookup;
        public EventBuffersManagerData EventBuffersManagerData;

        public void Execute()
        {
            // Build Data
            StressTestEventsData data = new StressTestEventsData
            {
                Singleton = EventTestSingleton,
                LocalTransformLookup = LocalTransformLookup,
                EmissionColorLookup = EmissionColorLookup,
            };

            int executedCount = 0;

            // Execute
            {
                // Lists
                for (int i = 0; i < EventBuffersManagerData.EventLists.Length; i++)
                {
                    UnsafeList<byte> list = EventBuffersManagerData.EventLists[i];
                    int readByteIndex = 0;
                    bool hasFinished = false;
                    while (!hasFinished)
                    {
                        IStressTestEventManager.Execute(ref list, readByteIndex, out int readSize, out hasFinished, ref data);
                        readByteIndex += readSize;
                        executedCount++;
                    }
                }

                // Streams
                for (int i = 0; i < EventBuffersManagerData.EventStreams.Length; i++)
                {
                    UnsafeStream.Reader streamReader = EventBuffersManagerData.EventStreams[i].AsReader();
                    for (int j = 0; j < streamReader.ForEachCount; j++)
                    {
                        streamReader.BeginForEachIndex(j);
                        bool hasFinished = false;
                        while (!hasFinished)
                        {
                            IStressTestEventManager.Execute(ref streamReader, out hasFinished, ref data);
                            executedCount++;
                        }
                        streamReader.EndForEachIndex();
                    }
                }
            }

            //Log.Debug($"Executed events; {executedCount}");
        }
    }
}