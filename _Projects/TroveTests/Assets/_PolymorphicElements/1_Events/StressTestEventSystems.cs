using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Trove.PolymorphicElements;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

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
    int TestProp { get; set; }
    void Execute(ref StressTestEventsData data);
}

[PolymorphicElement]
public struct StressTestEvent_SetPosition : IStressTestEvent
{
    public Entity Entity;
    public float3 Position;

    public int TestProp { get; set; }

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
public struct StressTestEvent_SetRotation : IStressTestEvent
{
    public Entity Entity;
    public quaternion Rotation;

    public int TestProp { get; set; }

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
public struct StressTestEvent_SetScale : IStressTestEvent
{
    public Entity Entity;
    public float Scale;

    public int TestProp { get; set; }

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
public struct StressTestEvent_SetColor : IStressTestEvent
{
    public Entity Entity;
    public float4 Color;

    public int TestProp { get; set; }

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
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
public partial struct StressTestTransformEventCreatorSystem : ISystem
{
    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EventsTest>();
        state.RequireForUpdate<StressTestEventExecutorSystem.Singleton>();
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        EventsTest singleton = SystemAPI.GetSingleton<EventsTest>();
        if (!singleton.EnableStressTestEventsTest || !state.EntityManager.Exists(singleton.MainCubeInstance))
            return;

        int jobThreads = singleton.TransformEventsJobThreads;
        int eventsPerThread = singleton.TransformEventsCount / jobThreads;

        EventStream.Writer eventStreamWriter = SystemAPI.GetSingletonRW<StressTestEventExecutorSystem.Singleton>().ValueRW.EventStreamManager.CreateEventStreamWriter(jobThreads);

        TransformEventJob job = new TransformEventJob
        {
            EventsPerThread = eventsPerThread,
            Time = (float)SystemAPI.Time.ElapsedTime,
            Singleton = singleton,
            EventStreamWriter = eventStreamWriter,
        };
        state.Dependency = job.Schedule(jobThreads, eventsPerThread, state.Dependency);
    }

    [BurstCompile]
    public partial struct TransformEventJob : IJobParallelFor
    {
        public int EventsPerThread;
        public float Time;
        public EventsTest Singleton;
        public EventStream.Writer EventStreamWriter;

        public void Execute(int index)
        {
            Random random = Random.CreateFromIndex((uint)index + (uint)(Time * 10000f));

            EventStreamWriter.BeginForEachIndex(index);
            for (int i = 0; i < EventsPerThread; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        IStressTestEventManager.AppendElement(ref EventStreamWriter, new StressTestEvent_SetPosition
                        {
                            Entity = Singleton.MainCubeInstance,
                            Position = random.NextFloat3(new float3(-3f), new float3(3f)),
                        });
                        break;
                    case 1:
                        IStressTestEventManager.AppendElement(ref EventStreamWriter, new StressTestEvent_SetRotation
                        {
                            Entity = Singleton.MainCubeInstance,
                            Rotation = random.NextQuaternionRotation(),
                        });
                        break;
                    case 2:
                        IStressTestEventManager.AppendElement(ref EventStreamWriter, new StressTestEvent_SetScale
                        {
                            Entity = Singleton.MainCubeInstance,
                            Scale = random.NextFloat(0.5f, 2f),
                        });
                        break;
                }
            }
            EventStreamWriter.EndForEachIndex();
        }
    }
}


[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
public partial struct StressTestColorEventCreatorSystem : ISystem
{
    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EventsTest>();
        state.RequireForUpdate<StressTestEventExecutorSystem.Singleton>();
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        EventsTest singleton = SystemAPI.GetSingleton<EventsTest>();
        if (!singleton.EnableStressTestEventsTest || !state.EntityManager.Exists(singleton.MainCubeInstance))
            return;

        int jobThreads = singleton.ColorEventsJobThreads;
        int eventsPerThread = singleton.ColorEventsCount / jobThreads;

        EventStream.Writer eventWriter = SystemAPI.GetSingletonRW<StressTestEventExecutorSystem.Singleton>().ValueRW.EventStreamManager.CreateEventStreamWriter(jobThreads);

        ColorEventsJob job = new ColorEventsJob
        {
            EventsPerThread = eventsPerThread,
            Time = (float)SystemAPI.Time.ElapsedTime,
            Singleton = singleton,
            EventStreamWriter = eventWriter,
        };
        state.Dependency = job.Schedule(jobThreads, 1, state.Dependency);
    }

    [BurstCompile]
    public partial struct ColorEventsJob : IJobParallelFor
    {
        public int EventsPerThread;
        public float Time;
        public EventsTest Singleton;
        public EventStream.Writer EventStreamWriter;

        const float ColorStrength = 1f;

        public void Execute(int index)
        {
            Random random = Random.CreateFromIndex((uint)index + (uint)(Time * 10000f));

            EventStreamWriter.BeginForEachIndex(index);
            for (int i = 0; i < EventsPerThread; i++)
            {
                IStressTestEventManager.AppendElement(ref EventStreamWriter, new StressTestEvent_SetColor
                {
                    Entity = Singleton.MainCubeInstance,
                    Color = new float4(random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), 1f),
                });
            }
            EventStreamWriter.EndForEachIndex();
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct StressTestEventExecutorSystem : ISystem
{
    // Store collections in the system
    private EventStreamManager _eventStreamManager;

    public struct Singleton : IComponentData
    {
        public EventStreamManager EventStreamManager;
    }

    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EventsTest>();

        // Create events manager in the system (allocates native collections)
        _eventStreamManager = new EventStreamManager(ref state);

        // Create singleton
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new Singleton
        {
            EventStreamManager = _eventStreamManager,
        });
    }

    [BurstCompile]
    void OnDestroy(ref SystemState state)
    {
        // Dispose events manager
        _eventStreamManager.Dispose();
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        EventsTest singleton = SystemAPI.GetSingleton<EventsTest>();
        if (!singleton.EnableStressTestEventsTest)
            return;

        // Iterate stream readers and schedule a job to process each one
        ref EventStreamManager eventStreamManager = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW.EventStreamManager;
        eventStreamManager.BeginEventStreamReaderIteration();
        while (eventStreamManager.NextEventStreamReader(out EventStream.Reader streamReader))
        {
            ExecuteEventsJob job = new ExecuteEventsJob
            {
                Singleton = singleton,
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
                EmissionColorLookup = SystemAPI.GetComponentLookup<URPMaterialPropertyEmissionColor>(false),

                EventStreamReader = streamReader,
            };
            state.Dependency = job.Schedule(state.Dependency);
        }

        // Dispose streams & clear
        state.Dependency = eventStreamManager.DisposeAndClearEventStreams(state.Dependency);

    }

    [BurstCompile]
    public partial struct ExecuteEventsJob : IJob
    {
        public EventsTest Singleton;
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        public ComponentLookup<URPMaterialPropertyEmissionColor> EmissionColorLookup;

        public EventStream.Reader EventStreamReader;

        public void Execute()
        {
            StressTestEventsData data = new StressTestEventsData
            {
                Singleton = Singleton,
                LocalTransformLookup = LocalTransformLookup,
                EmissionColorLookup = EmissionColorLookup,
            };

            // Iterate stream buffers
            for (int i = 0; i < EventStreamReader.ForEachCount; i++)
            {
                // Iterate events in stream readers
                EventStreamReader.BeginForEachIndex(i);
                bool success = true;
                while (success)
                {
                    IStressTestEventManager.Execute(ref EventStreamReader, ref data, out success);
                }
                EventStreamReader.EndForEachIndex();
            }
        }
    }
}