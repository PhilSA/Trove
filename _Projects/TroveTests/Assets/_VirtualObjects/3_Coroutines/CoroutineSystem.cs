using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System;
using Unity.Rendering;
using Unity.Core;
using Trove.PolymorphicElements;

[BurstCompile]
public partial struct CoroutineSystem : ISystem
{
    private bool HasInitialized;

    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CoroutineTests>();

    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        CoroutineTests singleton = SystemAPI.GetSingleton<CoroutineTests>();

        if (!HasInitialized)
        {
            const float spacing = 2f;
            int resolution = (int)math.ceil(math.sqrt(singleton.RoutinesCount));

            for (int i = 0; i < singleton.RoutinesCount; i++)
            {
                Entity cube = state.EntityManager.Instantiate(singleton.CubePrefab);

                // Transform
                int row = i / resolution;
                int column = i % resolution;
                state.EntityManager.SetComponentData(cube, LocalTransform.FromPosition(new float3(column * spacing, row * spacing, 0f)));

                // Build a test coroutine
                {
                    // Basics
                    Entity coroutineEntity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData(coroutineEntity, new Coroutine());
                    state.EntityManager.AddBuffer<CoroutineState>(coroutineEntity).Reinterpret<byte>();
                    state.EntityManager.AddBuffer<CoroutineMetaData>(coroutineEntity).Reinterpret<PolymorphicElementMetaData>();

                    DynamicBuffer<byte> coroutineStatesBuffer = state.EntityManager.GetBuffer<CoroutineState>(coroutineEntity).Reinterpret<byte>();
                    DynamicBuffer<PolymorphicElementMetaData> coroutineMetaDatasBuffer = state.EntityManager.GetBuffer<CoroutineMetaData>(coroutineEntity).Reinterpret<PolymorphicElementMetaData>();

                    // Add sequence of states
                    PolymorphicElementsUtility.AddElementGetMetaData(ref coroutineStatesBuffer, new Coroutine_MoveTo
                    {
                        Entity = cube,
                        Target = math.up() * 5f,
                        Speed = 5f,
                    }, out PolymorphicElementMetaData metaData);
                    coroutineMetaDatasBuffer.Add(metaData);
                    PolymorphicElementsUtility.AddElementGetMetaData(ref coroutineStatesBuffer, new Coroutine_Wait
                    {
                        Time = 2f,
                    }, out metaData);
                    coroutineMetaDatasBuffer.Add(metaData);
                    PolymorphicElementsUtility.AddElementGetMetaData(ref coroutineStatesBuffer, new Coroutine_SetColor
                    {
                        Entity = cube,
                        Target = new float4(10f, 0f, 0f, 1f),
                    }, out metaData);
                    coroutineMetaDatasBuffer.Add(metaData);
                    PolymorphicElementsUtility.AddElementGetMetaData(ref coroutineStatesBuffer, new Coroutine_MoveTo
                    {
                        Entity = cube,
                        Target = math.up() * 5f + math.right() * 6f,
                        Speed = 2f,
                    }, out metaData);
                    coroutineMetaDatasBuffer.Add(metaData);
                }
            }

            HasInitialized = true;
        }

        CoroutineSystemJob job = new CoroutineSystemJob
        {
            Time = SystemAPI.Time,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            EmissionColorLookup = SystemAPI.GetComponentLookup<URPMaterialPropertyEmissionColor>(false),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct CoroutineSystemJob : IJobEntity
    {
        public TimeData Time;
        public EntityCommandBuffer ECB;
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        public ComponentLookup<URPMaterialPropertyEmissionColor> EmissionColorLookup;

        void Execute(Entity entity, RefRW<Coroutine> coroutine, ref DynamicBuffer<CoroutineState> coroutineStateBuffer, ref DynamicBuffer<CoroutineMetaData> metaDataBuffer)
        {
            CoroutineUpdateData data = new CoroutineUpdateData
            {
                Time = Time,
                ECB = ECB,
                Coroutine = coroutine,
                LocalTransformLookup = LocalTransformLookup,
                EmissionColorLookup = EmissionColorLookup,
            };

            bool mustTriggerBegin = false;
            if (coroutine.ValueRW.Next)
            {
                coroutine.ValueRW.Next = false;
                coroutine.ValueRW.CurrentStateIndex++;
                mustTriggerBegin = true;
            }

            DynamicBuffer<byte> coroutineStateBytesBuffer = coroutineStateBuffer.Reinterpret<byte>();

            if (coroutine.ValueRW.CurrentStateIndex >= 0 && coroutine.ValueRW.CurrentStateIndex < metaDataBuffer.Length)
            {
                int currentStateByteStartIndex = metaDataBuffer[coroutine.ValueRW.CurrentStateIndex].Value.StartByteIndex;
                if (mustTriggerBegin)
                {
                    ICoroutineStateManager.Begin(coroutineStateBytesBuffer, currentStateByteStartIndex, out _, out _, ref data);
                }
                ICoroutineStateManager.Update(coroutineStateBytesBuffer, currentStateByteStartIndex, out _, out _, ref data);
            }
            else
            {
                // Self destruct when reached the end
                ECB.DestroyEntity(entity);
            }
        }
    }
}