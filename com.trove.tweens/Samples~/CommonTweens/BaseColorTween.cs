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
public struct BaseColorTween : IComponentData
{
    public TweenTimer Timer;
    public TweenerFloat4 Tweener;

    public BaseColorTween(TweenerFloat4 tweener, TweenTimer timer)
    {
        Timer = timer;
        Tweener = tweener;
    }
}

// This system is commented out because its affected component depends on the render pipeline you're using
//[BurstCompile]
//[RequireMatchingQueriesForUpdate]
//public partial struct BaseColorTweenSystem : ISystem
//{
//    [BurstCompile]
//    void OnUpdate(ref SystemState state)
//    {
//        BaseColorTweenJob job = new BaseColorTweenJob
//        {
//            DeltaTime = SystemAPI.Time.DeltaTime,
//        };
//        state.Dependency = job.ScheduleParallel(state.Dependency);
//    }

//    [BurstCompile]
//    public partial struct BaseColorTweenJob : IJobEntity
//    {
//        public float DeltaTime;

//        void Execute(ref BaseColorTween t, ref URPMaterialPropertyBaseColor matProperty)
//        {
//            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
//            if (hasChanged)
//            {
//                t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref matProperty.Value);
//            }
//        }
//    }
//}