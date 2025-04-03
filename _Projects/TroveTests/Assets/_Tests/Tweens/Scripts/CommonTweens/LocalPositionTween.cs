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
public struct LocalPositionTween : IComponentData
{
    public TweenTimer Timer;
    public TweenerFloat3 Tweener;

    public LocalPositionTween(TweenerFloat3 tweener, TweenTimer timer)
    {
        Timer = timer;
        Tweener = tweener;
    }
}

[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct LocalPositionTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        LocalPositionTweenJob job = new LocalPositionTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct LocalPositionTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref LocalPositionTween t, ref LocalTransform localTransform)
        {
            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
            if (hasChanged)
            {
                t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref localTransform.Position);
            }
        }
    }
}