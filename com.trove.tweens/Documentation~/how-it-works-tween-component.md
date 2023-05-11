
[Home](./how-it-works.md)

# Creating a tween component

As an example of how to use this tool, we will build a local position tween component that can be added to entities that need their local positions tweened.

First, let's create our tween component that will live on the tweened entity. The two main building blocks of this component are the `TweenTimer`, which manages the lifecycle of the tween, and the `TweenerFloat3`, which knows how to tween a position:
```cs
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
```

Then, we need to write the system + job that handles this tween's logic. See comments for details:
```cs
[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))] // Important to update before transforms
[RequireMatchingQueriesForUpdate]
public partial struct LocalPositionTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        // Schedule the parallel job
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
            // First, update the tween timer based on deltaTime
            t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);

            // Then, only if the timer's time has changed (it wouldn't change if the tween was completed or paused)....
            if (hasChanged)
            {
                // .... update the tweener using the timer's normalized time. This is what will change the local transform's position
                t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref localTransform.Position);
            }
        }
    }
}
```

Finally, let's write the code that would add this tween component to an entity. The following code could be added to a system's `OnUpdate` or to a job's update:
```cs
// Add the tween component to the entity.
// The tween won't play by itself immediately, because we're setting "autoPlay" to false on the TweenTimer
ecb.AddComponent(myTweenedEntity, new LocalPositionTween(
    new TweenerFloat3(new float3(1f, 1f, 1f) /*the position to tween to*/, true /*whether or not that target position is relative to initial*/, EasingType.EaseInElastic),
    new TweenTimer(1f /*duration*/, false /*isLoop*/, false /*isRewind*/, 1f /*speed*/, false /*autoPlay*/)));

// (...)

// Get a component lookup for our tween type, so we can access it from other entities
ComponentLookup<LocalPositionTween> localPositionTweenLookup = SystemAPI.GetComponentLookup<LocalPositionTween>(false);

// Manually call "Play" on the TweenTimer, when we're ready to play
// Note: we're getting the component by "ref", because this will change the state of the TweenTimer and we must therefore write changes to the component
ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(myTweenedEntity).ValueRW;
t.Timer.Play(true /*whether the tween should be reset from the start when playing*/);
```