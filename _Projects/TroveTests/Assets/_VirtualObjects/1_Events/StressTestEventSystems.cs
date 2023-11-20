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
[UpdateBefore(typeof(StressTestEventExecutorSystem))]
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
        public NativeList<IStressTestEventUnionElement> UnionEventList;

        public void Execute()
        {
            Random random = Random.CreateFromIndex((uint)(Time * 10000f));

            for (int i = 0; i < EventsCount; i++)
            {
                if (Singleton.UseUnionEvents)
                {
                    switch (i % 3)
                    {
                        case 0:
                            UnionEventList.Add(new StressTestEvent_SetPosition
                            {
                                Entity = Singleton.MainCubeInstance,
                                Position = random.NextFloat3(new float3(-3f), new float3(3f)),
                            });
                            break;
                        case 1:
                            UnionEventList.Add(new StressTestEvent_SetRotation
                            {
                                Entity = Singleton.MainCubeInstance,
                                Rotation = random.NextQuaternionRotation(),
                            });
                            break;
                        case 2:
                            UnionEventList.Add(new StressTestEvent_SetScale
                            {
                                Entity = Singleton.MainCubeInstance,
                                Scale = random.NextFloat(0.5f, 2f),
                            });
                            break;
                    }
                }
                else
                {
                    switch (i % 3)
                    {
                        case 0:
                            PolymorphicElementsUtility.AddElement(ref EventList, new StressTestEvent_SetPosition
                            {
                                Entity = Singleton.MainCubeInstance,
                                Position = random.NextFloat3(new float3(-3f), new float3(3f)),
                            });
                            break;
                        case 1:
                            PolymorphicElementsUtility.AddElement(ref EventList, new StressTestEvent_SetRotation
                            {
                                Entity = Singleton.MainCubeInstance,
                                Rotation = random.NextQuaternionRotation(),
                            });
                            break;
                        case 2:
                            PolymorphicElementsUtility.AddElement(ref EventList, new StressTestEvent_SetScale
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
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(StressTestEventExecutorSystem))]
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
        public NativeList<IStressTestEventUnionElement> UnionEventList;

        const float ColorStrength = 1f;

        public void Execute()
        {
            Random random = Random.CreateFromIndex((uint)(Time * 10000f));

            for (int i = 0; i < EventsCount; i++)
            {
                if (Singleton.UseUnionEvents)
                {
                    UnionEventList.Add(new StressTestEvent_SetColor
                    {
                        Entity = Singleton.MainCubeInstance,
                        Color = new float4(random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), 1f),
                    });
                }
                else
                {
                    PolymorphicElementsUtility.AddElement(ref EventList, new StressTestEvent_SetColor
                    {
                        Entity = Singleton.MainCubeInstance,
                        Color = new float4(random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), random.NextFloat(0f, ColorStrength), 1f),
                    });
                }
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct StressTestEventExecutorSystem : ISystem
{
    private NativeList<byte> _internalEventList;
    private NativeList<IStressTestEventUnionElement> _internalUnionEventList;

    public struct Singleton : IComponentData
    {
        public NativeList<byte> EventList;
        public NativeList<IStressTestEventUnionElement> UnionEventList;
    }

    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EventsTest>();

        // Create singleton
        _internalEventList = new NativeList<byte>(1000000, Allocator.Persistent);
        _internalUnionEventList = new NativeList<IStressTestEventUnionElement>(100000, Allocator.Persistent);
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new Singleton
        {
            EventList = _internalEventList,
            UnionEventList = _internalUnionEventList,
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

        if (singleton.UseUnionEvents)
        {
            ExecuteUnionEventsJob job = new ExecuteUnionEventsJob
            {
                Singleton = singleton,
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
                EmissionColorLookup = SystemAPI.GetComponentLookup<URPMaterialPropertyEmissionColor>(false),

                EventList = SystemAPI.GetSingletonRW<Singleton>().ValueRW.UnionEventList,
            };
            state.Dependency = job.Schedule(state.Dependency);
        }
        else
        {
            ExecuteEventsJob job = new ExecuteEventsJob
            {
                Singleton = singleton,
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
                EmissionColorLookup = SystemAPI.GetComponentLookup<URPMaterialPropertyEmissionColor>(false),

                EventList = SystemAPI.GetSingletonRW<Singleton>().ValueRW.EventList,
            };
            state.Dependency = job.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ExecuteUnionEventsJob : IJob
    {
        public EventsTest Singleton;
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        public ComponentLookup<URPMaterialPropertyEmissionColor> EmissionColorLookup;

        public NativeList<IStressTestEventUnionElement> EventList;

        public unsafe void Execute()
        {
            StressTestEventsData data = new StressTestEventsData
            {
                Singleton = Singleton,
                LocalTransformLookup = LocalTransformLookup,
                EmissionColorLookup = EmissionColorLookup,
            };

            int eventsCounter = 0;

            // Iterate and execute events
            for (int i = 0; i < EventList.Length; i++)
            {
                EventList[i].Execute(ref data);
                eventsCounter++;
            }

            //Log.Debug($"Executed {eventsCounter} events");

            // Clear events
            EventList.Clear();
        }
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
            int readByteIndex = 0;
            while (readByteIndex < EventList.Length - 1)
            {
                IStressTestEventManager.Execute(PolymorphicElementsUtility.GetPtrOfByteIndex(EventList, readByteIndex), out int readSize, ref data);
                readByteIndex += readSize;
                eventsCounter++;
            }

            //Log.Debug($"Executed {eventsCounter} events");

            // Clear events
            EventList.Clear();
        }
    }
}