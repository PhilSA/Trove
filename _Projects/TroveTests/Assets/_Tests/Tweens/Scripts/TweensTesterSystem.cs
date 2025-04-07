using System;
using System.Collections;
using System.Collections.Generic;
using Trove.Tweens;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Serializable]
public struct PositionToScaleSequenceTween : IComponentData
{
    public sbyte State;

    public unsafe void Play(bool reset, ref LocalPositionTween positionTween, ref NonUniformScaleTween scaleTween)
    {
    }

    public unsafe void SetCourse(bool forward, ref LocalPositionTween positionTween, ref NonUniformScaleTween scaleTween)
    {
        TweenUtilities.SetSequenceCourse(forward, ref State, ref positionTween.Timer, ref scaleTween.Timer);
    }
}

[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct PositionToScaleSequenceTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        PositionToScaleSequenceTweenJob job = new PositionToScaleSequenceTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public unsafe partial struct PositionToScaleSequenceTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref PositionToScaleSequenceTween t, ref LocalPositionTween positionTween, ref NonUniformScaleTween scaleTween)
        {
            TweenUtilities.UpdateSequence(ref t.State, ref positionTween.Timer, ref scaleTween.Timer);
        }
    }
}

[BurstCompile]
public partial struct TweensTesterSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        state.EntityManager.CompleteAllTrackedJobs();

        // Initialize
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (tweenTesterRW, entity) in SystemAPI.Query<RefRW<TweensTester>>().WithEntityAccess())
        {
            ref TweensTester tester = ref tweenTesterRW.ValueRW;
            if (!tester.IsInitialized)
            {
                // Initial transforms
                tester.EntityAInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityA);
                tester.EntityBInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityB);
                tester.EntityCInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityC);
                tester.EntityDInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityD);
                tester.EntityEInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityE);
                tester.EntityFInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityF);
                tester.EntityGInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityG);
                tester.EntityHInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityH);
                tester.EntityIInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityI);
                tester.EntityJInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityJ);
                tester.EntityKInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityK);
                tester.EntityLInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityL);
                tester.EntityMInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityM);
                tester.EntityNInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityN);
                tester.EntityOInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityO);
                tester.EntityPInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityP);
                tester.EntityQInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityQ);
                tester.EntityRInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityR);
                tester.EntitySInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityS);
                tester.EntityTInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityT);
                tester.EntityUInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityU);
                tester.EntityVInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityV);
                tester.EntityWInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityW);
                tester.EntityXInitialTransform = SystemAPI.GetComponent<LocalTransform>(tester.EntityX);

                // Setup tweens
                {
                    Random tmpRandom = Random.CreateFromIndex(0);

                    // A
                    ecb.AddComponent(tester.EntityA, new ShakeTween(1f, 8f, 1.5f, ref tmpRandom, EasingType.EaseInQuad));

                    // B
                    ecb.AddComponent(tester.EntityB, new FlashTween(1f, 7f, Color.red * 10f, EasingType.Linear, EasingType.EaseInCirc));

                    // C
                    ecb.AddComponent(tester.EntityC, new ShakeTween(1f, 8f, 1f, ref tmpRandom, EasingType.EaseInQuad));
                    ecb.AddComponent(tester.EntityC, new FlashTween(1f, 7f, Color.green * 10f, EasingType.Linear, EasingType.EaseInCirc));

                    // D
                    ecb.AddComponent(tester.EntityD, new LocalPositionTween(
                        new TweenerFloat3(tester.EntityDInitialTransform.Position, math.forward(), true, EasingType.EaseInElastic),
                        new TweenTimer(1f, false, false)));

                    // E
                    ecb.AddComponent(tester.EntityE, new LocalPositionTween(
                        new TweenerFloat3(tester.EntityEInitialTransform.Position, math.forward(), true, EasingType.EaseInCubic), 
                        new TweenTimer(1f, false, false)));
                    ecb.AddComponent(tester.EntityE, new LocalRotationTween(
                        new TweenerQuaternion(quaternion.identity, quaternion.Euler(1f, 1f, 1f), true, EasingType.EaseInCubic), 
                        new TweenTimer(1f, false, false)));
                    ecb.AddComponent(tester.EntityE, new LocalScaleTween(
                        new TweenerFloat(1f, 2f, false, EasingType.EaseInCubic), 
                        new TweenTimer(1f, false, false)));
                    ecb.AddComponent(tester.EntityE, new BaseColorTween(
                        new TweenerFloat4(Color.blue.ToFloat4(), false, EasingType.EaseInCubic), new 
                        TweenTimer(1f, false, true)));

                    // F
                    ecb.AddComponent(tester.EntityF, new LocalPositionTween(
                        new TweenerFloat3(tester.EntityFInitialTransform.Position, math.forward(), true, EasingType.EaseInCubic), 
                        new TweenTimer(1f, false, false)));

                    // G
                    ecb.AddComponent(tester.EntityG, new LocalPositionTween(
                        new TweenerFloat3(tester.EntityGInitialTransform.Position, math.forward(), true, EasingType.EaseInCubic), 
                        new TweenTimer(1f, false, false)));
                    ecb.AddComponent(tester.EntityG, new NonUniformScaleTween(
                        new TweenerFloat3(new float3(1f, 1f, 1f), new float3(2f, 1f, 0.5f), false, EasingType.EaseInBounce), 
                        new TweenTimer(1f, false, false)));

                    // H
                    ecb.AddComponent(tester.EntityH, new LocalPositionTween(
                        new TweenerFloat3(tester.EntityHInitialTransform.Position, math.forward(), true, EasingType.EaseInCubic), 
                        new TweenTimer(0.5f, false, false)));
                    ecb.AddComponent(tester.EntityH, new NonUniformScaleTween(
                        new TweenerFloat3(new float3(1f, 1f, 1f), new float3(2f, 1f, 0.5f), false, EasingType.EaseInBounce), 
                        new TweenTimer(0.5f, false, false)));
                    ecb.AddComponent(tester.EntityH, new PositionToScaleSequenceTween());

                    // I
                    ecb.AddComponent(entity, new LocalPositionTargetTween(
                        tester.EntityI,
                        new TweenerFloat3(tester.EntityIInitialTransform.Position, math.forward(), true, EasingType.EaseInCubic),
                        new TweenTimer(1f, false, false)));

                    // J
                    DynamicBuffer<LocalPositionBufferTween> jPosTweensBuffer = ecb.AddBuffer<LocalPositionBufferTween>(tester.EntityJ);
                    jPosTweensBuffer.Add(new LocalPositionBufferTween(
                        new TweenerFloat(tester.EntityJInitialTransform.Position.x, 1f, true, EasingType.EaseInCubic),
                        new TweenTimer(1f, false, false),
                        LocalPositionBufferTween.Type.X));
                    jPosTweensBuffer.Add(new LocalPositionBufferTween(
                        new TweenerFloat(tester.EntityJInitialTransform.Position.y, 1f, true, EasingType.EaseInCubic),
                        new TweenTimer(1f, false, false),
                        LocalPositionBufferTween.Type.Y));
                    jPosTweensBuffer.Add(new LocalPositionBufferTween(
                        new TweenerFloat(tester.EntityJInitialTransform.Position.z, 1f, true, EasingType.EaseInBounce),
                        new TweenTimer(1f, false, false),
                        LocalPositionBufferTween.Type.Z));

                }

                // Stress test many tween entities
                {
                    Random tmpRandom = Random.CreateFromIndex(0);

                    for (int i = 0; i < tweenTesterRW.ValueRW.StressTestTweens; i++)
                    {
                        Entity newTweenEntity = ecb.CreateEntity();
                        ecb.AddComponent(newTweenEntity, new ShakeTween(1f, 8f, 1.5f, ref tmpRandom, EasingType.EaseInQuad));
                        ecb.AddComponent(newTweenEntity, new LocalTransform { Position = default, Rotation = quaternion.identity, Scale = 1f });
                        ecb.AddComponent(newTweenEntity, new LocalToWorld { Value = float4x4.identity });

                        // TODO: option to play them
                    }
                }

                tester.IsInitialized = true;
            }
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        ComponentLookup<ShakeTween> shakeTweenLookup = SystemAPI.GetComponentLookup<ShakeTween>(false);
        ComponentLookup<FlashTween> flashTweenLookup = SystemAPI.GetComponentLookup<FlashTween>(false);
        ComponentLookup<LocalPositionTween> localPositionTweenLookup = SystemAPI.GetComponentLookup<LocalPositionTween>(false);
        ComponentLookup<LocalPositionTargetTween> localPositionTargetTweenLookup = SystemAPI.GetComponentLookup<LocalPositionTargetTween>(false);
        BufferLookup<LocalPositionBufferTween> localPositionBufferTweenLookup = SystemAPI.GetBufferLookup<LocalPositionBufferTween>(false);
        ComponentLookup<LocalRotationTween> localRotationTweenLookup = SystemAPI.GetComponentLookup<LocalRotationTween>(false);
        ComponentLookup<LocalScaleTween> localScaleTweenLookup = SystemAPI.GetComponentLookup<LocalScaleTween>(false);
        ComponentLookup<NonUniformScaleTween> nonUniformScaleTweenLookup = SystemAPI.GetComponentLookup<NonUniformScaleTween>(false);
        ComponentLookup<BaseColorTween> baseColorTweenLookup = SystemAPI.GetComponentLookup<BaseColorTween>(false);
        ComponentLookup<EmissionColorTween> emissionColorTweenLookup = SystemAPI.GetComponentLookup<EmissionColorTween>(false);
        ComponentLookup<PositionToScaleSequenceTween> positionToScaleComboTweenLookup = SystemAPI.GetComponentLookup<PositionToScaleSequenceTween>(false);

        // Play tweens (press)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            foreach (var (tweenTesterRW, entity) in SystemAPI.Query<RefRW<TweensTester>>().WithEntityAccess())
            {
                ref TweensTester tester = ref tweenTesterRW.ValueRW;

                // A
                {
                    ref ShakeTween t = ref shakeTweenLookup.GetRefRW(tester.EntityA).ValueRW;
                    t.AddAmplitude(100f);
                    t.Timer.Play(true);
                }

                // B
                {
                    ref FlashTween t = ref flashTweenLookup.GetRefRW(tester.EntityB).ValueRW;
                    t.Timer.Play(true);
                }

                // C
                {
                    ref ShakeTween st = ref shakeTweenLookup.GetRefRW(tester.EntityC).ValueRW;
                    ref FlashTween ft = ref flashTweenLookup.GetRefRW(tester.EntityC).ValueRW;
                    st.AddAmplitude(10f);
                    st.Timer.Play(true);
                    ft.Timer.Play(true);
                }

                // D
                {
                    ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(tester.EntityD).ValueRW;
                    t.Timer.Play(true);
                }

                // E
                {
                    ref LocalPositionTween p = ref localPositionTweenLookup.GetRefRW(tester.EntityE).ValueRW;
                    ref LocalRotationTween r = ref localRotationTweenLookup.GetRefRW(tester.EntityE).ValueRW;
                    ref LocalScaleTween s = ref localScaleTweenLookup.GetRefRW(tester.EntityE).ValueRW;
                    ref BaseColorTween c = ref baseColorTweenLookup.GetRefRW(tester.EntityE).ValueRW;
                    p.Timer.Play(true);
                    r.Timer.Play(true);
                    s.Timer.Play(true);
                    c.Timer.Play(true);
                }

                // F
                {
                    ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(tester.EntityF).ValueRW;
                    t.Timer.SetCourse(true);
                    t.Timer.Play(false);
                }

                // G
                {
                    ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(tester.EntityG).ValueRW;
                    ref NonUniformScaleTween n = ref nonUniformScaleTweenLookup.GetRefRW(tester.EntityG).ValueRW;
                    t.Timer.SetCourse(true);
                    n.Timer.SetCourse(true);
                    t.Timer.Play(false);
                    n.Timer.Play(false);
                }

                // H
                {
                    ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(tester.EntityH).ValueRW;
                    ref NonUniformScaleTween n = ref nonUniformScaleTweenLookup.GetRefRW(tester.EntityH).ValueRW;
                    ref PositionToScaleSequenceTween c = ref positionToScaleComboTweenLookup.GetRefRW(tester.EntityH).ValueRW;
                    TweenUtilities.SetSequenceCourse(true, ref c.State, ref t.Timer, ref n.Timer);
                    TweenUtilities.PlaySequence(false, ref c.State, ref t.Timer, ref n.Timer);
                }

                // I
                {
                    ref LocalPositionTargetTween t = ref localPositionTargetTweenLookup.GetRefRW(entity).ValueRW;
                    t.Timer.Play(true);
                }

                // J
                {
                    if (localPositionBufferTweenLookup.TryGetBuffer(tester.EntityJ, out DynamicBuffer<LocalPositionBufferTween> tweensBuffer))
                    {
                        for (int i = 0; i < tweensBuffer.Length; i++)
                        {
                            var t = tweensBuffer[i];
                            t.Timer.Play(true);
                            tweensBuffer[i] = t;
                        }
                    }
                }
            }
        }

        // Play tweens (unpress)
        if (Input.GetKeyUp(KeyCode.Space))
        {
            foreach (var (tweenTesterRW, entity) in SystemAPI.Query<RefRW<TweensTester>>().WithEntityAccess())
            {
                ref TweensTester tester = ref tweenTesterRW.ValueRW;

                // F
                {
                    ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(tester.EntityF).ValueRW;
                    t.Timer.SetCourse(false);
                    t.Timer.Play(false);
                }

                // G
                {
                    ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(tester.EntityG).ValueRW;
                    ref NonUniformScaleTween n = ref nonUniformScaleTweenLookup.GetRefRW(tester.EntityG).ValueRW;
                    t.Timer.SetCourse(false);
                    n.Timer.SetCourse(false);
                    t.Timer.Play(false);
                    n.Timer.Play(false);
                }

                // H
                {
                    ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(tester.EntityH).ValueRW;
                    ref NonUniformScaleTween n = ref nonUniformScaleTweenLookup.GetRefRW(tester.EntityH).ValueRW;
                    ref PositionToScaleSequenceTween c = ref positionToScaleComboTweenLookup.GetRefRW(tester.EntityH).ValueRW;
                    TweenUtilities.SetSequenceCourse(false, ref c.State, ref t.Timer, ref n.Timer);
                    TweenUtilities.PlaySequence(false, ref c.State, ref t.Timer, ref n.Timer);

                }
            }
        }
    }
}
