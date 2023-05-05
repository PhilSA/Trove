using Unity.Entities;
using Unity.Mathematics;
using System;
using Trove.Tweens;
using Unity.Collections;
using Trove;
using Color = UnityEngine.Color;
using Random = Unity.Mathematics.Random;
using Unity.Transforms;
using Unity.Burst;

[Serializable]
public struct NonUniformScaleTween : IComponentData
{
    public TweenTimer Timer;
    public TweenerFloat3 Tweener;

    public NonUniformScaleTween(TweenerFloat3 tweener, TweenTimer timer)
    {
        Timer = timer;
        Tweener = tweener;
    }
}

[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct NonUniformScaleTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        NonUniformScaleTweenJob job = new NonUniformScaleTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct NonUniformScaleTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref NonUniformScaleTween t, ref PostTransformMatrix scale)
        {
            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
            if (hasChanged)
            {
                float3 nonUnitofmScale = scale.Value.Scale();
                t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref nonUnitofmScale);
                scale.Value = float4x4.Scale(nonUnitofmScale);
            }
        }
    }
}