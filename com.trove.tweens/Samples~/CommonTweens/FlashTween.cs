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
public struct FlashTween : IComponentData
{
    public TweenTimer Timer;

    private float FlashPeriod;
    private float4 InitialColor;
    private float4 FlashColor;
    private EasingType DecayEasing;
    private EasingType FlashEasing;

    public FlashTween(
        float duration,
        float flashCount,
        Color flashColor,
        EasingType decayEasing = EasingType.Linear,
        EasingType flashColorEasing = EasingType.EaseInCirc)
    {
        Timer = new TweenTimer(duration, false, false);

        FlashPeriod = duration / flashCount;
        InitialColor = float4.zero;
        FlashColor = flashColor.ToFloat4();
        DecayEasing = decayEasing;
        FlashEasing = flashColorEasing;
    }

    public void Update(ref float4 emissiveColor)
    {
        float intensityScale = EasingUtilities.CalculateEasing(1f - Timer.GetNormalizedTime(), DecayEasing);

        float flashNormalizedTime = (Timer.GetTime() % FlashPeriod) / FlashPeriod;
        flashNormalizedTime *= 2f;
        if (flashNormalizedTime > 1f)
        {
            flashNormalizedTime = 1f - (flashNormalizedTime - 1f);
        }
        float flashValue = EasingUtilities.CalculateEasing(flashNormalizedTime, FlashEasing);

        float4 currentPeakFlashColor = math.lerp(InitialColor, FlashColor, intensityScale);
        emissiveColor = math.lerp(InitialColor, currentPeakFlashColor, flashValue);
    }
}

// This system is commented out because its affected component depends on the render pipeline you're using
//[BurstCompile]
//[RequireMatchingQueriesForUpdate]
//public partial struct FlashTweenSystem : ISystem
//{
//    [BurstCompile]
//    void OnUpdate(ref SystemState state)
//    {
//        FlashTweenJob job = new FlashTweenJob
//        {
//            DeltaTime = SystemAPI.Time.DeltaTime,
//        };
//        state.Dependency = job.ScheduleParallel(state.Dependency);
//    }

//    [BurstCompile]
//    public partial struct FlashTweenJob : IJobEntity
//    {
//        public float DeltaTime;

//        void Execute(ref FlashTween t, ref URPMaterialPropertyEmissionColor emissiveColor)
//        {
//            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
//            if (hasChanged)
//            {
//                t.Update(ref emissiveColor.Value);
//            }
//        }
//    }
//}
