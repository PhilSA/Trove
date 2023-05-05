
[Home](./how-it-works.md)

# Tweening tools

Here are the various kinds of tools Trove Tweens provides. 

## Tween timer

The `TweenTimer` is a struct that manages the time-related state and lifecycle of a tween. It doesn't know about which values are being tweened, but it is meant to be used in conjunction with tweening code that understands what to tween.

It contains functions and properties such as:
* `Play`: start playing the timer.
* `Pause`: pause the timer.
* `Stop`: stop (reset) the timer.
* `Update`: advance the timer's time and state by a certain amount of time.
* `GetNormalizedTime`: get the normalized time of the timer, relatively to its duration.
* `SetNormalizedTime`: set the normalized time of the timer, relatively to its duration.
* `HasCompleted`: whether or not the tween has completed its duration.
* `IsPlaying`: whether or not the tween is currently playing.
* `GetExcessTime`: store how much excess time this tween had after completion.
* `SetCourse`: determines if the timer should advance time forward, or in reverse.

When creating a `TweenTimer` with its constructor, you can set the following parameters:
* `duration`: how long will the timer last before completion.
* `isLoop`: does this timer loop upon completion? 
* `isRewind`: does this timer rewind (reverse its timer) upon reaching the end of its duration. 
* `speed`: the speed multiplier used to update time.
* `autoPlay`: whether or not the tween starts playing immediately upon creation.

The typical usage for a `TweenTimer` is to store it in a component, write a job that updates its time using `tweenTimer.Update`, access the `tweenTimer.GetNormalizedTime`, use that normalized time as parameter for a `EasingUtilities` function, and then apply the tween to a value using this easing result.


## Tweeners

Tweeners are built-in structs that can (optionally) be used with `TweenTimer`s in order to tween values. There are different tweeners for different types of values:
* `TweenerFloat`
* `TweenerFloat2`
* `TweenerFloat3`
* `TweenerFloat4`
* `TweenerQuaternion`

These built-in Tweeners take the following parameters for their constructors:
* `initial`: the initial tweened value (what to tween the value from). This parameter is optional. If not specified, the tween will use the value the object had when the tween started as its `initial` value.
* `target`: the target of the tweened value (what to tween the value to).
* `targetIsRelative`: whether or not the `target` is relative to the `initial` value (as opposed to being absolute).
* `easing`: the type of easing function to use for this tween.


## Built-in tweens

There are a few pre-built tweens available for common scenarios. These are available as a download in the package's "Samples" tab in the package manager:
* `LocalPositionTween`
* `LocalRotationTween`
* `LocalScaleTween`
* `NonUniformScaleTween`
* `BaseColorTween`: This one does not come with a built-in system + job, as it will depend on the shaders and render pipeline you're using. However, a commented-out example system for this is available in the package (search for "BaseColorTweenSystem").
* `EmissionColorTween`: This one does not come with a built-in system + job, as it will depend on the shaders and render pipeline you're using. However, a commented-out example system for this is available in the package (search for "EmissionColorTweenSystem").
* `ShakeTween`: shakes the target's position. When playing this tween, rememeber to call `shakeTween.AddAmplitude()` on top of calling `shakeTween.Timer.Play(true)`. This type of tween has a `maxAmplitude` defined, but shake amplitude can be added to it gradually and additively.
* `FlashTween`: "flashes" a target color. This one does not come with a built-in system + job, as it will depend on the shaders and render pipeline you're using. However, a commented-out example system for this is available in the package (search for "FlashTweenSystem").


## EasingUtilities

The `EasingUtilities` static class is part of Trove Common, but is mainly used for tweening. It provides various easing functions that return a value based on a normalized time. You can read more about these here: https://easings.net/ 
