using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System;

[BurstCompile]
public partial struct EndFrameSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        state.EntityManager.CompleteAllTrackedJobs();
    }
}