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
public struct StressTestEvent_SetPosition : IStressTestEvent
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
public struct StressTestEvent_SetRotation : IStressTestEvent
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
public struct StressTestEvent_SetScale : IStressTestEvent
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
public struct StressTestEvent_SetColor : IStressTestEvent
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

        TransformEventJob job = new TransformEventJob
        {
            EventsCount = singleton.TransformEventsCount,
            Time = (float)SystemAPI.Time.ElapsedTime,
            Singleton = singleton,

            EventList = SystemAPI.GetSingletonRW<StressTestEventExecutorSystem.Singleton>().ValueRW.EventList,
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct TransformEventJob : IJob
    {
        public int EventsCount;
        public float Time;
        public EventsTest Singleton;

        public NativeList<byte> EventList;

        public void Execute()
        {
            Random random = Random.CreateFromIndex((uint)(Time * 10000f));

            for (int i = 0; i < EventsCount; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        IStressTestEventManager.AddElement(ref EventList, new StressTestEvent_SetPosition
                        {
                            Entity = Singleton.MainCubeInstance,
                            Position = random.NextFloat3(new float3(-3f), new float3(3f)),
                        });
                        break;
                    case 1:
                        IStressTestEventManager.AddElement(ref EventList, new StressTestEvent_SetRotation
                        {
                            Entity = Singleton.MainCubeInstance,
                            Rotation = random.NextQuaternionRotation(),
                        });
                        break;
                    case 2:
                        IStressTestEventManager.AddElement(ref EventList, new StressTestEvent_SetScale
                        {
                            Entity = Singleton.MainCubeInstance,
                            Scale = random.NextFloat(0.5f, 2f),
                        });
                        break;
                }
            }
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

        ColorEventsJob job = new ColorEventsJob
        {
            EventsCount = singleton.ColorEventsCount,
            Time = (float)SystemAPI.Time.ElapsedTime,
            Singleton = singleton,

            EventList = SystemAPI.GetSingletonRW<StressTestEventExecutorSystem.Singleton>().ValueRW.EventList,
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct ColorEventsJob : IJob
    {
        public int EventsCount;
        public float Time;
        public EventsTest Singleton;

        public NativeList<byte> EventList;

        const float ColorStrength = 1f;

        public void Execute()
        {
            Random random = Random.CreateFromIndex((uint)(Time * 10000f));

            for (int i = 0; i < EventsCount; i++)
            {
                IStressTestEventManager.AddElement(ref EventList, new StressTestEvent_SetColor
                {
                    Entity = Singleton.MainCubeInstance,
                    Color = new float4(random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), 1f),
                });
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct StressTestEventExecutorSystem : ISystem
{
    private NativeList<byte> _internalEventList;

    public struct Singleton : IComponentData
    {
        public NativeList<byte> EventList;
    }

    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EventsTest>();

        // Create singleton
        _internalEventList = new NativeList<byte>(100000, Allocator.Persistent);
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new Singleton
        {
            EventList = _internalEventList,
        });
    }

    [BurstCompile]
    void OnDestroy(ref SystemState state)
    {
        if (_internalEventList.IsCreated)
        {
            _internalEventList.Dispose();
        }
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        EventsTest singleton = SystemAPI.GetSingleton<EventsTest>();
        if (!singleton.EnableStressTestEventsTest)
            return;

        ExecuteEventsJob job = new ExecuteEventsJob
        {
            Singleton = singleton,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            EmissionColorLookup = SystemAPI.GetComponentLookup<URPMaterialPropertyEmissionColor>(false),

            EventList = SystemAPI.GetSingletonRW<Singleton>().ValueRW.EventList,
        };
        state.Dependency = job.Schedule(state.Dependency);

    }

    [BurstCompile]
    public partial struct ExecuteEventsJob : IJob
    {
        public EventsTest Singleton;
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        public ComponentLookup<URPMaterialPropertyEmissionColor> EmissionColorLookup;

        public NativeList<byte> EventList;

        public void Execute()
        {
            StressTestEventsData data = new StressTestEventsData
            {
                Singleton = Singleton,
                LocalTransformLookup = LocalTransformLookup,
                EmissionColorLookup = EmissionColorLookup,
            };

            int eventsCounter = 0;

            // Iterate and execute events
            int elementStartByteIndex = 0;
            bool success = EventList.Length > 0;
            while (success)
            {
                IStressTestEventManager.Execute(ref EventList, elementStartByteIndex, out elementStartByteIndex, ref data, out success);
                if (success)
                {
                    eventsCounter++;
                }
            }

           Log.Debug($"Executed {eventsCounter} events");

            // Clear events
            EventList.Clear();
        }
    }
}