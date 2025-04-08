
[Home](../README.md)

# How it works - Advanced topics

> Note: some example code and scenes are available in the Trove test project:
> * [Stats Folder](https://github.com/PhilSA/Trove/tree/main/_Projects/TroveTests/Assets/_Tests/Stats)
> * [Unit Tests](https://github.com/PhilSA/Trove/tree/main/com.trove.stats/Tests/Runtime)


## Table of Contents
* [Netcode](#netcode)
* [Saving and loading](#saving-and-loading)
* [Stat values as Ints](#stat-values-as-ints)
* [Dynamic stat modifiers](#dynamic-stat-modifiers)
* [Self-removing modifiers](#self-removing-modifiers)
* [Complex modifiers stack](#complex-modifiers-stack)
* [On-demand stats update](#stats-that-are-only-evaluared-on-demand)


---

## Netcode

See [Netcode](./advanced-netcode.md)


## Saving and loading

In order to save/load the state of stats and their modifiers, all fields of the following components and buffers must be serialized/deserialized:
* The `StatsOwner` component
* The `Stat` buffer
* The `StatModifier<T,S>` buffer
* The `StatObserver` buffer

Additionally, the `StatHandle` and `StatModifierHandle` fields stored in your components may also need to be serialized/deserialized if they are not added deterministically.

Keep in mind that the stats of one entity may depend on the stats of other entities, so if you want to save such entities, you must also save all the entities whose stats they depend on. `StatsUtilities` provides utilities for this:
* `StatsUtilities.GetOtherDependantStatEntitiesOfEntity` will return all stat entities that depend on the target entity.
* `StatsUtilities.GetStatEntitiesThatEntityDependsOn` will return all stat entities that this entity depends on.


## Stat values as Ints

`StatsUtilities` includes the `AsInt` and `AsFloat` extension methods for `float`s and `int`s. This allows you to "store" an `int` value in a `float` value's memory range. This is different from casting between `int` and `float`. Casting `10.1f` to an `int` will result in a value of `10`. `AsInt` and `AsFloat` will instead just replace the underlying byte data so that an int can be losslessly stored in a `float` field, meaning using `AsInt()` on a value of `10.1f` will give a completely different and hard-to-guess value (not `10`).

If you want a certain stat to be treated as an `int` instead of a `float`:
* Whenever you change the stat value, use `StatsAccessor.TrySetStatBaseValueInt`, `StatsAccessor.TryAddStatBaseValueInt`, `StatsAccessor.TryMultiplyStatBaseValueInt` instead of the regular non-"Int" versions.
* Whenever you add a modifier to an int stat, make sure both the modifier type and the modifier stack know they should deal with the stat value as an `int`. For example, for a modifier that adds a value to a stat that should be treated as an `int`, the modifiers stack should hold an `int AddInt` field, and the modifier should add to the stack's `AddInt` field. Then, in the stack's `Apply()` we'd convert the stat's `baseValue` to an `int` using `int intValue = statBaseValue.AsInt()`, add the `AddInt` value to it, and finally set the `float` stat value to `statValue = intValue.AsFloat();`.
* Adding float-based modifiers to an int stat, or int-based modifiers to a float stat will lead to highly unpredictable results. Make sure it's always all float or all int for the same stat.
    * To reduce risks of mistakes in your game, you may wish to treat all stats and modifier values as ints, and then have a project-wide (or per-stat) divider value that you use to get decimal numbers.


## Dynamic stat modifiers

Once added, a stat modifier cannot be directly changed unless it changes itself in its own `Apply()` code. If you wanted to change a modifier's values, you'd have to first remove the modifier using its `StatModifierHandle`, and then add back a new version of the modifier.

Thankfully however, this package provides a different way for modifier values to change dynamically, without having to do this expensive remove/add modifier operation every time. 

Let's work with the simple example of a weapon damage modifier that adds a damage bonus that increases as our hero's health ratio lowers. Instead of removing, changing, and re-adding the modifier to the weapon's "Damage" stat every frame with an updated damage bonus calculated based on the current hero health ratio, we would make the modifier depend on 2 stats when adding it the first time: the "CurrentHealth" stat of our hero entity, and the "MaxHealth" stat of our hero entity. If these health values were not already stats, they should be converted to stats. Alternatively, we could even choose to create a "HealthRatio" stat that has a modifier that makes it depend on the "CurrentHealth" and "MaxHealth" stats, and the modifier sets the stat value to "CurrentHealth/MaxHealth". When either the "CurrentHealth" or "MaxHealth" stats are changed, it would trigger an update of the "HealthRatio" stat, which would trigger an update of the weapon damage stat. The damage modifier will use these health stat values in its `Apply()` logic to calculate the bonus, and we will now have a weapon damage modifier that automatically scales with our hero's health ratio.

Basically, any value of a modifier that may change after adding the modifier can be turned into another stat that the modifier observes. Changing these values after adding the modifier just means changing the value of those observed stats.

Stats that a modifier depends on must be stored as `StatHandle`s in the modifier struct, and must be added to a list in the modifier's `AddObservedStatsToList()`. You can refer to the `SampleStatModifier` from the "Starter Content" sample for an example of this.


## Self-removing modifiers

Modifier trigger events can be used for self-removing modifiers. For example, if you need modifiers to self-remove whenever they detect that a stat they depend on no longer exists:
* In the modifier's `Apply()`, if `statsReader.TryGetStat` returns false, 
    * set `shouldProduceModifierTriggerEvent` to true. This will create a trigger event for this modifier.
    * set a `MustRemove` field in the modifier struct to true.
* In the system that processes your modifier trigger events,
    * In the event data, if `modifierEvent.Modifier.MustRemove == true;`, remove the modifier using `StatsAccessor.TryRemoveStatModifier(event.ModifierHandle, ref statsWorldData);`


## Complex modifiers stack

It's possible to add and set global data in your stats modifier stack, by using `StatsWorldData.SetStatModifiersStack()`. You can use this to create more complex modifier effects.

For example, let's say you want a stackable health modifier that is capped to 5 stacks, and whose modifier bonuses decay based on a curve and the number of stacks. We could handle this like this:
* Use `StatsWorldData.SetStatModifiersStack()` on game start to set some evaluation curve data in the modifiers stack. It could be a procedural curve function, a blob curve, etc...
* Also add a `int myModifierStackCount;` field to your modifiers stack.
* In your modifier `Apply()` function, if the stack's `myModifierStackCount` is `< 5`:
    * increment `myModifierStackCount`.
    * get the evaluation curve from the stack, and evaluate the curve at the current `myModifierStackCount` value.
    * add this evaluated value to the stack's `Add` field.

`StatsWorldData.SetStatModifiersStack()` is also a good way to create modifiers that depend on elapsed time. You can store the elapsed time in the stack and update the stack at the start of every frame.

`StatsWorldData.SetStatModifiersStack()` can also be use to add native collections or any kind of blob reference to the stack, for whatever purposes you may find.


## On-demand stats update

In certain cases, it can be useful -for performance reasons- to only update stats on-demand (meaning they don't automatically react to stat changes of dependent stats). This can be the case if a stat A depends on a stat B, and B changes every frame, but A only needs to be read once in a while. Instead of having stat B's update trigger an update to stat A every frame, we can choose to only update stat A when we need to read it.

In order to do this, the modifiers we add to the stat must NOT register their observed stats in the modifier's `AddObservedStatsToList`. This will prevent reactive stat changes for the modifier's affected stat. Then when we do want to access the up-to-date value of this stat, we simply call `StatsAccessor.TryUpdateStat` or `StatsAccessor.UpdateStatRef` before reading the value.