
[Home](./how-it-works.md)

# Creating a tween buffer element

This section adds to what we've learned in [Creating a tween component](./how-it-works-tween-component.md) and will skip certain explanations.

You may decide that storing tweens as buffer elements would be preferable for your use case, because you may have multiple similar tweens of the same type affecting the same entity. In this example, we'll create a tween that can tween the individual components of a color for URP materials. 

First, let's create our tween buffer element:
```cs
[Serializable]
public struct BaseColorTween : IBufferElementData
{
    public enum ColorComponentType
    {
        R,
        G,
        B,
        A,
    }

    public TweenTimer Timer; 
    public TweenerFloat Tweener;
    public ColorComponentType Type;

    public LocalPositionTween(TweenerFloat tweener, TweenTimer timer, ColorComponentType type)
    {
        Timer = timer;
        Tweener = tweener;
        Type = type;
    }
}
```

Then, let's create the system + job for this tween:
```cs
[BurstCompile]
[RequireMatchingQueriesForUpdate]
public partial struct BaseColorTweenSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        BaseColorTweenJob job = new BaseColorTweenJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct BaseColorTweenJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(ref DynamicBuffer<BaseColorTween> tweenBuffer, ref URPMaterialPropertyBaseColor baseColor)
        {
            // For each tween element in buffer....
            for (int i = 0; i < tweenBuffer.Length; i++)
            {
                BaseColorTween t = tweenBuffer[i];

                // Update timer...
                t.Timer.Update(DeltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);

                // If timer time has changed....
                if (hasChanged)
                {
                    // Tween based on the color component type that this tween affects
                    switch (t.Type)
                    {
                        case BaseColorTween.ColorComponentType.R:
                            t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref baseColor.Value.x);
                            break;
                        case BaseColorTween.ColorComponentType.G:
                            t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref baseColor.Value.y);
                            break;
                        case BaseColorTween.ColorComponentType.B:
                            t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref baseColor.Value.z);
                            break;
                        case BaseColorTween.ColorComponentType.A:
                            t.Tweener.Update(t.Timer.GetNormalizedTime(), hasStartedPlaying, ref baseColor.Value.a);
                            break;
                    }
                }

                // Don't forget to write back our modified values to the buffer. The timer's state could have changed even if its tweened time didn't change
                tweenBuffer[i] = t;
            }
        }
    }
}
```

We can now create those tweens from a system or job:
```cs
// Add the buffer to the entity
DynamicBuffer<BaseColorTween> tweensBuffer = ecb.AddBuffer<BaseColorTween>(myTweenedEntity);

// Add an element for each color component
tweensBuffer.Add(new BaseColorTween(
    new TweenerFloat(0f /*initial*/, 1f /*target*/, false /*isRelative*/, EasingType.EaseInBounce),
    new TweenTimer(1f /*duration*/, false /*isLoop*/, false /*isRewind*/),
    BaseColorTween.ColorComponentType.R));
tweensBuffer.Add(new BaseColorTween(
    new TweenerFloat(0f /*initial*/, 1f /*target*/, false /*isRelative*/, EasingType.EaseInBounce),
    new TweenTimer(1f /*duration*/, false /*isLoop*/, false /*isRewind*/),
    BaseColorTween.ColorComponentType.G));
tweensBuffer.Add(new BaseColorTween(
    new TweenerFloat(0f /*initial*/, 1f /*target*/, false /*isRelative*/, EasingType.EaseInBounce),
    new TweenTimer(1f /*duration*/, false /*isLoop*/, false /*isRewind*/),
    BaseColorTween.ColorComponentType.B));
tweensBuffer.Add(new BaseColorTween(
    new TweenerFloat(0f /*initial*/, 1f /*target*/, false /*isRelative*/, EasingType.EaseInBounce),
    new TweenTimer(1f /*duration*/, false /*isLoop*/, false /*isRewind*/),
    BaseColorTween.ColorComponentType.A));

// (...)

BufferLookup<BaseColorTween> baseColorTweenBufferLookup = SystemAPI.GetBufferLookup<BaseColorTween>(false);

// Get the buffer on the target entity
if (baseColorTweenBufferLookup.TryGetBuffer(myTweenedEntity, out DynamicBuffer<BaseColorTween> tweensBuffer))
{
    // Iterate the tweens in buffer
    for (int i = 0; i < tweensBuffer.Length; i++)
    {
        var t = tweensBuffer[i];

        // Play the tweens
        t.Timer.Play(true);

        // Don't forget to write back to buffer, since Playing modifies the state of the TweenTimer
        tweensBuffer[i] = t;
    }
}
```