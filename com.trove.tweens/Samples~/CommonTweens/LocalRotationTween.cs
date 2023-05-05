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
public struct LocalRotationTween : IComponentData
{
    public TweenTimer Timer;
    public TweenerQuaternion Tweener;

    public LocalRotationTween(TweenerQuaternion tweener, TweenTimer timer)
    {
        Timer = timer;
        Tweener = tweener;
    }
}

[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct LocalRotationTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        LocalRotationTweenJob job = new LocalRotationTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct LocalRotationTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref LocalRotationTween t, ref LocalTransform localTransform)
        {
            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
            if (hasChanged)
            {
                t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref localTransform.Rotation);
            }
        }
    }
}