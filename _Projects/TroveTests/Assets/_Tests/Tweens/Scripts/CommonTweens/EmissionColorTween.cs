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
using Unity.Rendering;

[Serializable]
public struct EmissionColorTween : IComponentData
{
    public TweenTimer Timer;
    public TweenerFloat4 Tweener;

    public EmissionColorTween(TweenerFloat4 tweener, TweenTimer timer)
    {
        Timer = timer;
        Tweener = tweener;
    }
}

// This system is commented out because its affected component depends on the render pipeline you're using
[BurstCompile]
[RequireMatchingQueriesForUpdate]
public partial struct EmissionColorTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        EmissionColorTweenJob job = new EmissionColorTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct EmissionColorTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref EmissionColorTween t, ref URPMaterialPropertyEmissionColor matProperty)
        {
            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
            if (hasChanged)
            {
                t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref matProperty.Value);
            }
        }
    }
}
