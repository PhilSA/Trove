using Trove;
using Trove.Audio.FMOD;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct AudioTests : IComponentData
{
    public Entity ListenerPrefab;
    public float2 ListenerSpeed;
    
    public Entity EmitterPrefab;
    public int EmitterSpawnCount;
    public float EmitterSpacing;
    public float2 EmitterSpeed;

    public bool DidInitialize;
}

public struct AudioTestsSpeed : IComponentData
{
    public float2 Speed;
    public float3 StartPos;
}

partial struct AudioTestsSystem : ISystem
{
    public bool DidThing;
    public float DidThingCounter;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AudioTests>();
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref AudioTests audioTests = ref SystemAPI.GetSingletonRW<AudioTests>().ValueRW;

        // Spawn
        if (!audioTests.DidInitialize)
        {
            MathUtilities.GetGrid3DParams(audioTests.EmitterSpawnCount, audioTests.EmitterSpacing, 
                out float3 gridExtents, out int elementResolution);

            if (audioTests.ListenerPrefab != Entity.Null)
            {
                Entity listenerEntity = state.EntityManager.Instantiate(audioTests.ListenerPrefab);
                state.EntityManager.SetComponentData(listenerEntity, LocalTransform.FromPosition(float3.zero));
                state.EntityManager.AddComponentData(listenerEntity, new AudioTestsSpeed
                {
                    Speed = audioTests.ListenerSpeed,
                    StartPos = float3.zero,
                });
            }

            if (audioTests.EmitterPrefab != Entity.Null)
            {
                for (int i = 0; i < audioTests.EmitterSpawnCount; i++)
                {
                    Entity emitterEntity = state.EntityManager.Instantiate(audioTests.EmitterPrefab);
                    float3 pos = MathUtilities.GetGrid3DPosition(i, audioTests.EmitterSpacing, elementResolution) - gridExtents;
                    state.EntityManager.SetComponentData(emitterEntity, LocalTransform.FromPosition(pos));
                    state.EntityManager.AddComponentData(emitterEntity, new AudioTestsSpeed
                    {
                        Speed = audioTests.EmitterSpeed,
                        StartPos = pos,
                    });
                }
            }

            audioTests.DidInitialize = true;
        }
        
        // Speed
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        foreach (var (localTransform, speed) in 
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<AudioTestsSpeed>>())
        {
            localTransform.ValueRW.Position = speed.ValueRO.StartPos + 
                                              (math.sin(elapsedTime * speed.ValueRO.Speed.x) * speed.ValueRO.Speed.y);
        }
        
        // Disable
        // DidThingCounter += SystemAPI.Time.DeltaTime;
        // if (DidThingCounter > 3f)
        // {
        //     if (DidThing)
        //     {
        //         EntityQuery emittersQuery = SystemAPI.QueryBuilder().WithAll<FMODEventEmitter, Disabled>().Build();
        //         DidThing = false;
        //         Debug.Log($"Removed disabled on {emittersQuery.CalculateEntityCount()}");
        //         state.EntityManager.RemoveComponent<Disabled>(emittersQuery);
        //     }
        //     else
        //     {
        //         EntityQuery emittersQuery = SystemAPI.QueryBuilder().WithAll<FMODEventEmitter>().Build();
        //         DidThing = true;
        //         Debug.Log($"Added disabled on {emittersQuery.CalculateEntityCount()}");
        //         state.EntityManager.AddComponent<Disabled>(emittersQuery);
        //     }
        //     DidThingCounter = 0f;
        // }
    }
}
