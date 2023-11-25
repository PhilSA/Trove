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

        TransformEventJob job = new TransformEventJob
        {
            EventsCount = singleton.TransformEventsCount,
            Time = (float)SystemAPI.Time.ElapsedTime,
            Singleton = singleton,

            EventWriter = SystemAPI.GetSingletonRW<MyEventSystem.Singleton>().ValueRW.EventBuffersManager.CreateEventWriterParallel(1, ref state),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct TransformEventJob : IJob
    {
        public int EventsCount;
        public float Time;
        public EventsTest Singleton;

        public EventWriterParallel EventWriter;

        public void Execute()
        {
            Random random = Random.CreateFromIndex((uint)(Time * 10000f));

            EventWriter.BeginForEachIndex(0);
            for (int i = 0; i < EventsCount; i++)
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

        ColorEventsJob job = new ColorEventsJob
        {
            EventsCount = singleton.ColorEventsCount,
            Time = (float)SystemAPI.Time.ElapsedTime,
            Singleton = singleton,

            EventWriter = SystemAPI.GetSingletonRW<MyEventSystem.Singleton>().ValueRW.EventBuffersManager.CreateEventWriterParallel(1, ref state),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct ColorEventsJob : IJob
    {
        public int EventsCount;
        public float Time;
        public EventsTest Singleton;

        public EventWriterParallel EventWriter;

        const float ColorStrength = 1f;

        public void Execute()
        {
            Random random = Random.CreateFromIndex((uint)(Time * 10000f));

            EventWriter.BeginForEachIndex(0);
            for (int i = 0; i < EventsCount; i++)
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