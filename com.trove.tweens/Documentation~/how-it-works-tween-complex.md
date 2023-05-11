
[Home](./how-it-works.md)

# Creating a complex tween

This section adds to what we've learned in [Creating a tween component](./how-it-works-tween-component.md) and will skip certain explanations.

There are times where it could be more efficient to tween multiple things at the same time, rather than having one tween component type per thing to tween. Let's go over the implemetnation of a tween that tweens both the position and the color of an entity.

First, the component. Notice that there is a single `TweenTimer`, but two different tweeners:
```cs
[Serializable]
public struct PositionAndColorTween : IComponentData
{
    public TweenTimer Timer; 
    public TweenerFloat3 PositionTweener;
    public TweenerFloat4 ColorTweener;

    public LocalPositionTween(TweenerFloat3 positionTweener, TweenerFloat4 colorTweener, TweenTimer timer)
    {
        Timer = timer;
        PositionTweener = positionTweener;
        ColorTweener = colorTweener;
    }
}
```


Then, the system + job:
```cs
[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))] // Important to update before transforms
[RequireMatchingQueriesForUpdate]
public partial struct PositionAndColorTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        // Schedule the parallel job
        PositionAndColorTweenJob job = new PositionAndColorTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct PositionAndColorTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref PositionAndColorTween t, ref LocalTransform localTransform, ref URPMaterialPropertyBaseColor baseColor)
        {
            // First, update the tween timer based on deltaTime
            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);

            // Then, only if the timer's time has changed (it wouldn't change if the tween was completed or paused)....
            if (hasChanged)
            {
                float normTime = t.Timer.GetNormalizedTime();
                // .... update the position tweener
                t.PositionTweener.Update(normTime, hasStartedPlaying, ref localTransform.Position);
                // .... update the color tweener
                t.ColorTweener.Update(normTime, hasStartedPlaying, ref baseColor.Value);
            }
        }
    }
}
```

Finally, creating and playing the tween:
```cs
ecb.AddComponent(myTweenedEntity, new PositionAndColorTween(
    new TweenerFloat3(new float3(1f, 1f, 1f) /*the position to tween to*/, true /*whether or not that target position is relative to initial*/, EasingType.EaseInElastic),
    new TweenerFloat4(new float4(0.3f, 0.8f, 0.5f, 1f) /*the color to tween to*/, false /*whether or not that target position is relative to initial*/, EasingType.EaseInElastic),
    new TweenTimer(1f /*duration*/, false /*isLoop*/, false /*isRewind*/, 1f /*speed*/, false /*autoPlay*/)));

// (...)

ComponentLookup<PositionAndColorTween> positionAndColorTweenLookup = SystemAPI.GetComponentLookup<PositionAndColorTween>(false);

ref PositionAndColorTween t = ref positionAndColorTweenLookup.GetRefRW(myTweenedEntity).ValueRW;
t.Timer.Play(true);
```