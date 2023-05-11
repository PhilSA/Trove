
[Home](./how-it-works.md)

# Creating a tween entity

This section adds to what we've learned in [Creating a tween component](./how-it-works-tween-component.md) and will skip certain explanations.

There are times where it would be desirable for the tween component to not actually live on the target's entity; but on its own separate entity instead. This could be the case if you need to add some custom "OnUpdate" or "OnComplete" logic to your tween. Here we'll go over how to create such a tween.

First, let's create our tween component. We do two things differently compared to the `LocalPositionTween` we've built before:
1. There's a field for storing the target Entity that the tween affects
1. The `TweenTimer` is not present inside the component. This is because in this case, we will make the `TweenTimer` be its own component on the entity. We'll see why this is important towards the end of the section (allows for generic "OnComplete" logic for tweens)

```cs
[Serializable]
public struct LocalPositionTargetTween : IComponentData
{
    public Entity Target;
    public TweenerFloat3 Tweener;

    public LocalPositionTween(Entity target, TweenerFloat3 tweener)
    {
        Target = target;
        Tweener = tweener;
    }
}
```

Then, let's create the system + job for this type of tween. This is once again similar to the `LocalPositionTweenSystem` we've created before, but the `LocalTransform` component is gotten by component lookup instead of being gotten on the iterated entity. We also iterate on the `TweenTimer` on top of our tween component. Notice the job is also not scheduled in parallel, since we're writing to other entities:
```cs
[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct LocalPositionTargetTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        LocalPositionTargetTweenJob job = new LocalPositionTargetTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
        };
        state.Dependency = job.Schedule(state.Dependency); // single-thread
    }

    [BurstCompile]
    public partial struct LocalPositionTargetTweenJob : IJobEntity
    {
        public float DeltaTime;
        public ComponentLookup<LocalTransform> LocalTransformLookup;

        // We iterate on both the Timer and the Tween
        void Execute(ref TweenTimer timer, ref LocalPositionTargetTween t)
        {
            timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
            if (hasChanged)
            {
                // Get the transform component on the target entity rather than on self entity
                RefRW<LocalTransform> localTransformRW = LocalTransformLookup.GetRefRW(t.Target);
                if (localTransformRW.IsValid)
                {
                    t.Tweener.Update(timer.GetNormalizedTime(), hasStartedPlaying, ref localTransformRW.ValueRW.Position);
                }
            }
        }
    }
}
```

Finally, here's the code for creating and playing the tween:
```cs
ecb.AddCOmponent(newTweenEntity, new TweenTimer(1f, false, false)); // Add the timer as its own component on the entity
ecb.AddComponent(newTweenEntity, new LocalPositionTargetTween(
    myTweenedEntity, /*Target*/
    new TweenerFloat3(tester.EntityIInitialTransform.Position, math.forward(), true, EasingType.EaseInCubic)));

// (...)

ComponentLookup<LocalPositionTargetTween> localPositionTargetTweenLookup = SystemAPI.GetComponentLookup<LocalPositionTargetTween>(false);

// We get the tween component on its own entity rather than on the tweened entity
ref LocalPositionTargetTween t = ref localPositionTargetTweenLookup.GetRefRW(newTweenEntity).ValueRW;
t.Timer.Play(true);
```

-----------------------------------------------------------


At the beginning of this section, we mentioned that this approach would allow us to add some "OnUpdate" or "OnComplete" logic to this tween. Here's an example of how it could be done:
```cs
// We want to be able to add this component to a tween entity in order to make it automatically start another tween on complete
public struct StartOtherTweenOnComplete : IComponentData
{
    public Entity OtherTweenEntity; // this represents another tween-as-entity to start when the current tween completes
}

[BurstCompile]
[RequireMatchingQueriesForUpdate]
public partial struct StartOtherTweenOnCompleteSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        StartOtherTweenOnCompleteJob job = new StartOtherTweenOnCompleteJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            TweenTimerLookup = SystemAPI.GetComponentLookup<TweenTimer>(false),
        };
        state.Dependency = job.Schedule(state.Dependency); // single-thread
    }

    [BurstCompile]
    [WithChangeFilter(typeof(TweenTimer))] // Only run this for changed TweenTimers
    public partial struct StartOtherTweenOnCompleteJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer ECB;
        public ComponentLookup<TweenTimer> TweenTimerLookup;

        // We iterate only on this component type
        void Execute(Entity entity, in StartOtherTweenOnComplete c)
        {
            // Get this tween entity's timer
            if(TweenTimerLookup.TryGetComponent(entity, out TweenTimer thisTimer))
            {
                // Check if the timer has completed
                if(thisTimer.HasCompleted())
                {
                    // Start the other tween's timer, with the excess time that this current timer had
                    if(TweenTimerLookup.TryGetComponent(c.OtherTweenEntity, out TweenTimer otherTimer))
                    {
                        // Set time as the excess time of the completed timer
                        otherTimer.SetTime(thisTimer.GetExcessTime())
                        otherTimer.Play(true);

                        // Write back to component on other entity
                        TweenTimerLookup[c.OtherTweenEntity] = otherTimer;
                    }

                    // Self-destruct tween
                    ECB.DestroyEntity(entity);
                }
            }
        }
    }
}
```
