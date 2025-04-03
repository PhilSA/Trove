using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections;
using System;
using Trove;
using Trove.Tweens;
using UnityEngine;

[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct LocalPositionTargetTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        LocalPositionTargetTweenJob job = new LocalPositionTargetTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct LocalPositionTargetTweenJob : IJobEntity
    {
        public float DeltaTime;
        public ComponentLookup<LocalTransform> LocalTransformLookup;

        void Execute(ref LocalPositionTargetTween t)
        {
            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
            if (hasChanged)
            {
                RefRW<LocalTransform> localTransformRW = LocalTransformLookup.GetRefRW(t.Target);
                if (localTransformRW.IsValid)
                {
                    t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref localTransformRW.ValueRW.Position);
                }
            }
        }
    }
}

[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct LocalPositionBufferTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        LocalPositionBufferTweenJob job = new LocalPositionBufferTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct LocalPositionBufferTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref DynamicBuffer<LocalPositionBufferTween> tBuffer, ref LocalTransform localTransform)
        {
            for (int i = 0; i < tBuffer.Length; i++)
            {
                LocalPositionBufferTween t = tBuffer[i];
                t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
                if (hasChanged)
                {
                    switch (t.PosType)
                    {
                        case LocalPositionBufferTween.Type.X:
                            t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref localTransform.Position.x);
                            break;
                        case LocalPositionBufferTween.Type.Y:
                            t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref localTransform.Position.y);
                            break;
                        case LocalPositionBufferTween.Type.Z:
                            t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref localTransform.Position.z);
                            break;
                    }
                }
                tBuffer[i] = t;
            }
        }
    }
}



