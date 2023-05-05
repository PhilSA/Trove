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
[WriteGroup(typeof(LocalToWorld))]
public struct ShakeTween : IComponentData
{
    public TweenTimer Timer;

    public float Frequency;
    public float MaxAmplitude;
    public EasingType AmplitudeEasing;
    public float noiseTimer;
    public float amplitudeUpTimer;
    public float currentAmplitudeRatio;
    public float targetAmplitudeRatio;
    public FixedList32Bytes<float> randomSlopes;

    public ShakeTween(
        float duration,
        float frequency,
        float maxAmplitude,
        ref Random random,
        EasingType amplitudeEasing = EasingType.EaseInQuad,
        bool autoPlay = false)
    {
        Timer = new TweenTimer(duration, false, false, 1f, autoPlay);

        Frequency = frequency;
        MaxAmplitude = maxAmplitude;
        AmplitudeEasing = amplitudeEasing;

        noiseTimer = 0f;
        amplitudeUpTimer = 0f;
        currentAmplitudeRatio = 0f;
        targetAmplitudeRatio = 0f;
        randomSlopes = new FixedList32Bytes<float>();
        NoiseUtilities.InitRandomSlopes(ref random, ref randomSlopes);
    }

    public void Update(float deltaTime, out float3 localShakePosition)
    {
        // Handle tweening up added amplitude
        if (amplitudeUpTimer > 0f)
        {
            amplitudeUpTimer -= deltaTime;
            float amplitudeTimerRatio = 1f - (amplitudeUpTimer / (1f / Frequency)); // How close to completion the timer is
            currentAmplitudeRatio = math.lerp(currentAmplitudeRatio, targetAmplitudeRatio, amplitudeTimerRatio);
        }
        float amplitudeDecayRatio = EasingUtilities.CalculateEasing(1f - Timer.GetNormalizedTime(), AmplitudeEasing);
        currentAmplitudeRatio = math.min(currentAmplitudeRatio, amplitudeDecayRatio);
        float noiseAmplitude = MaxAmplitude * currentAmplitudeRatio;

        // Advance noise timer
        noiseTimer += deltaTime;
        float noiseIndex = noiseTimer * Frequency;

        localShakePosition = default;
        localShakePosition.x = NoiseUtilities.Perlin1D(noiseIndex, randomSlopes, 0) * noiseAmplitude;
        localShakePosition.y = NoiseUtilities.Perlin1D(noiseIndex, randomSlopes, 3) * noiseAmplitude;
        localShakePosition.z = NoiseUtilities.Perlin1D(noiseIndex, randomSlopes, 5) * noiseAmplitude;
    }

    public void AddAmplitude(float amplitude)
    {
        float currentAmplitude = currentAmplitudeRatio * MaxAmplitude;
        targetAmplitudeRatio = math.saturate((currentAmplitude + amplitude) / MaxAmplitude);
        float currentToTargetRatio = 1f - (currentAmplitudeRatio / targetAmplitudeRatio);
        amplitudeUpTimer = (1f / Frequency) * currentToTargetRatio;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[RequireMatchingQueriesForUpdate]
public partial struct ShakeTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        ShakeTweenJob job = new ShakeTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct ShakeTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref ShakeTween t, ref LocalToWorld ltw, in LocalTransform transform)
        {
            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
            if (hasChanged)
            {
                t.Update(DeltaTime, out float3 localShakePosition);
                ltw.Value = float4x4.TRS(transform.Position + math.mul(transform.Rotation, localShakePosition), transform.Rotation, transform.Scale);
            }
        }
    }
}