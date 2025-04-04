
[Home](../README.md)

# How it works

> Note: some example code and scenes are available in the Trove test project:
> * [Stats Folder](https://github.com/PhilSA/Trove/tree/main/_Projects/TroveTests/Assets/_Tests/Stats)
> * [Unit Tests](https://github.com/PhilSA/Trove/tree/main/com.trove.stats/Tests/Runtime)


## Table of Contents
* [Overview](#overview)
    * [Stats](#stats)
    * [Stat Modifiers](#stat-modifiers)
    * [Stats Access](#stats-access)
* [Tutorial](#tutorial)
    * [Creating stats](#creating-stats)
    * [Using the StatsAccessor](#using-the-statsaccessor)
    * [Reading stats](#reading-stats)
    * [Changing stat values](#changing-stat-values)
    * [Adding stat modifiers](#adding-stat-modifiers)
    * [Removing stat modifiers](#removing-stat-modifiers)
    * [Stat events](#stat-events)
    * [Destroying stat entities](#destroying-stat-entities)
* [Advanced topics](#advanced-topics)
    * [Netcode](#netcode)
    * [Saving and loading](#saving-and-loading)
    * [Stat values as Ints](#stat-values-as-ints)
    * [Dynamic stat modifiers](#dynamic-stat-modifiers)
    * [Self-removing modifiers](#self-removing-modifiers)
    * [Complex modifiers stack](#complex-modifiers-stack)
    * [Stats that are only evaluared on-demand](#stats-that-are-only-evaluared-on-demand)


---


## Overview

#### Stats
A `Stat` is a `DynamicBuffer` element containing a `BaseValue`, a `Value`, and some other internal data. `BaseValue` represents the value before any modifiers are applied, and a `Value` represents the value after all modifiers are applied. 

When created, stats are given a unique `StatHandle`, so they can be referenced later.

#### Stat Modifiers
Stat modifiers and the modifiers stack are defined in user code. The Trove Stats "Starter Content" sample includes a starter version of stat modifiers and modifiers stack. Stat modifiers are implemented as a single struct that can act as any of the various modifier types in your game, and the stat modifiers stack represents the accumulation of all modifier operations affecting a certain stat when that stat is being recalculated. 

A stat modifier affects a specific stat on a specific entity, and is given a unique `StatModifierHandle` when added (so it can be removed later).

A stat modifier can also be an "observer" of other stats, meaning that when that stat changes, the modifier will know that it has to re-apply itself to the target stat. For example if a game wants to apply a bonus to the Strength stat based on the Dexterity stat: it would add a modifier to the Strength stat, and that modifier would be an observer of the Dexterity stat. Now when Dexterity changes, the modifier will tell the Strength stat that is has to recalculate its value and re-apply all of its modifiers (including the one that gives a bonus based on Dexterity).

#### Stats Access
In this package, you should never directly change the data in the `Stat`, `StatModifier`, `StatObserver` buffers directly. The data stored here must be handled by the various tools that the Stats package provides.
* `StatsAccessor` is the main tool you must use for all stat changes: stat creation, stat value change, stat modifier addition/removal. Note that it cannot be used in parallel, as stat changes can affect multiple entities.
* `StatsUtilities` can be used when you only need to read (not write) stats.


## Tutorial

> To get started, you should first navigate to the "Samples" tab of the "Trove Stats" package in the Package Manager window to download the "Starter Content". This includes basic customizable setup code for your stats.


#### Creating stats

The `SampleStatsAuthoring` (from the "Starter Content") demonstrates how to bake stats to an entity. First, creates a `StatsBaker` by using `StatsUtilities.BakeStatsComponents`. And then, create stats by calling `statsBaker.CreateStat` and storing the resulting `StatHandles` in your own components.

In order to make an entity support stats at runtime, you can use `StatsUtilities.AddStatsComponents`

In order to create stats at runtime, you can use `StatsAccessor.TryCreateStat`. (See [Using the StatsAccessor](#using-the-statsaccessor))


#### Using the StatsAccessor

Any system that needs to write to stats data needs a `StatsAccessor`. Here is an example of how to setup a `StatsAccessor` for a system:

```cs
public partial struct ExampleStatsSystem : ISystem
{
    private StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> _statsAccessor;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StatsWorldSingleton>();

        _statsAccessor = new StatsAccessor<SampleStatModifier, SampleStatModifier.Stack>(ref state, true, true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StatsWorldSingleton statsWorldSingleton = SystemAPI.GetSingleton<StatsWorldSingleton>();
        _statsAccessor.Update(ref state);
        
        // Now you are ready to use the StatsAccessor for stat read/write operations. 
        // A StatsAccessor can be passed on to jobs, along with the `StatsWorldSingleton.StatsWorldData`.
    }
```

`StatsAccessor` often needs to be passed a `StatsWorldData` for its methods that write to stats data. The `StatsWorldData` is stored in the `StatsWorldSingleton`.


#### Reading stats

There are several ways you can read stat values:

If you are iterating entity data and want to read stats on that entity, you can do so with `StatsUtilities`:
```cs
[BurstCompile]
public partial struct ExampleStatsReadJob : IJobEntity
{
    public void Execute(in SampleStats stats, in DynamicBuffer<Stat> statsBuffer)
    {
        // If you know for sure the stat handle is valid...
        StatsUtilities.GetStat(stats.StrengthHandle, in statsBuffer, out float value, out float baseValue);

        // If you don't know for sure the stat handle is valid...
        if(StatsUtilities.TryGetStat(stats.StrengthHandle, in statsBuffer, out float value, out float baseValue))
        {

        }
    }
}
```

If you need to read stats data that could possibly be on other entities, you can also use `StatsUtilities`:
```cs
[BurstCompile]
public partial struct ExampleStatsReadJob : IJobEntity
{
    [ReadOnly]
    public BufferLookup<Stat> StatsBufferLookup;

    public void Execute(in ComponentStoringOtherStatHandles comp)
    {
        if(StatsUtilities.TryGetStat(comp.StrengthHandle, in StatsBufferLookup, out float value, out float baseValue))
        {

        }
    }
}
```

If you are already using a `StatsAccessor`, you can use it to read stats as well:
```cs
[BurstCompile]
public partial struct ExampleStatsReadJob : IJobEntity
{
    public StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> StatsAccessor;
    public StatsWorldData<SampleStatModifier, SampleStatModifier.Stack> StatsWorldData;
    
    public void Execute(Entity entity, in SampleStats stats)
    {
        StatsAccessor.TryGetStat(stats.StrengthHandle, out float value, out float baseValue);
    }
}
```


#### Changing stat values

Changing stat values is done through the `StatsAccessor`. Note that it cannot be done in parallel because it may end up affecting stats of other entities:
```cs
[BurstCompile]
public partial struct ExampleStatsChangeJob : IJobEntity
{
    public StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> StatsAccessor;
    public StatsWorldData<SampleStatModifier, SampleStatModifier.Stack> StatsWorldData;
    
    public void Execute(Entity entity, in SampleStats stats)
    {
        StatsAccessor.TrySetStatBaseValue(stats.StrengthHandle, 5f, ref StatsWorldData);
        StatsAccessor.TryAddStatBaseValue(stats.StrengthHandle, 5f, ref StatsWorldData);
        StatsAccessor.TryMultiplyStatBaseValue(stats.StrengthHandle, 5f, ref StatsWorldData);
    }
}
```

Changing stat values this way will automatically and instantly recompute any other stat values that depends on this stat.


#### Adding stat modifiers

Adding modifiers is done through the `StatsAccessor`. Note that it cannot be done in parallel because it may end up affecting stats of other entities:
```cs
[BurstCompile]
public partial struct ExampleAddModifiersJob : IJobEntity
{
    public StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> StatsAccessor;
    public StatsWorldData<SampleStatModifier, SampleStatModifier.Stack> StatsWorldData;
    
    public void Execute(Entity entity, in SampleStats stats, ref DynamicBuffer<AddedStatModifier> addedModifiers)
    {
        if(StatsAccessor.TryAddStatModifier(stats.StrengthHandle,
            new SampleStatModifier
            {
                ModifierType = SampleStatModifier.Type.AddFromStat,
                StatHandleA = stats.DexterityHandle,
            },
            out StatModifierHandle modifierHandle,
            ref StatsWorldData))
        {
            // Here we store the added modifier handle in a buffer on the entity, so we can remove it later
            addedModifiers.Add(new AddedStatModifier { Value = modifierHandle });
        }
    }
}
```

Adding modifiers this way will automatically and instantly recompute any other stat values that depends on this stat.

-----

Note that you can also add stat modifiers during baking, but only if the affected stat and all the observed stats of the modifier are all on the same entity:
```cs

class Baker : Baker<ExampleStatsAuthoring>
{
    public override void Bake(ExampleStatsAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        
        StatsUtilities.BakeStatsComponents(this, entity, out StatsBaker<SampleStatModifier, SampleStatModifier.Stack> statsBaker);
        statsBaker.CreateStat(authoring.StatA, true, out testStatOwner.StatA);
        statsBaker.CreateStat(authoring.StatB, true, out testStatOwner.StatB);

        statsBaker.TryAddStatModifier(testStatOwner.StatA, new SampleStatModifier
        {
            ModifierType = TestStatModifier.Type.AddFromStat,
            StatHandleA = testStatOwner.StatB,
        }, out StatModifierHandle modifierHandle1);
    }
}
```


#### Removing stat modifiers

Removing modifiers is done through the `StatsAccessor`. Note that it cannot be done in parallel because it may end up affecting stats of other entities:
```cs
[BurstCompile]
public partial struct ExampleRemoveModifiersJob : IJobEntity
{
    public StatsAccessor<SampleStatModifier, SampleStatModifier.Stack> StatsAccessor;
    public StatsWorldData<SampleStatModifier, SampleStatModifier.Stack> StatsWorldData;
    
    public void Execute(Entity entity, in SampleStats stats, in DynamicBuffer<AddedStatModifier> addedModifiers)
    {
        // Here we remove the modifier we added earlier, by using the modifier handle we stored
        if(StatsAccessor.TryRemoveStatModifier(addedModifiers[0], ref statsWorldStorage))
        {
            addedModifiers.RemoveAtSwapBack(0);
        }
    }
}
```

Removing modifiers this way will automatically and instantly recompute any other stat values that depends on this stat.


#### Stat events

> Note: You can take a look at the `SampleStatEventsSystem` in the "Starter Content" sample, for an example of how to setting up a system to process stat events.

Two types of stat events are supported:
* Stat change events occur whenever a stat's value has changed. These events contain the `StatHandle` of the changed stat, the previous values, and the current values.
* Modifier trigger events occur whenever a stat modifier has been applied or re-applied to a stat. These events contain the `StatModifierHandle` of the triggered modifier, as well as the data of the modifier struct at the moment of triggering the event. 

For stat change events, you must make sure the stat itself supports change events. You can configure this either during stat creation (a bool parameter for the `CreateStat` and `TryCreateStat` methods handles this), or by using `StatsAccessor.TrySetStatProduceChangeEvents`. 

For modifier trigger events, stat modifiers are responsible for setting the value of the `out bool shouldProduceModifierTriggerEvent` parameter in their `Apply` function. 

Events can be accessed through the `StatsWorldData`, which is stored in `StatsWorldSingleton`.


#### Destroying stat entities

> Note: a pre-made version of what's described in this section is available as part of the "Starter Content" sample, in the "Stats Destruction" folder.

Special consideration must be taken when destroying stat entities, because of the fact that other stat entities may depend on them. When destroying these entities, any stat on other entities that depend on stats of the destroyed entity should be updated, so they can decide what to do with the fact that they now depend on a nonexistent stat. This won't happen automatically.

Because of this, it is recommended that you setup an entity destruction pipeline in your project, that gives you a chance to perform some logic on entities pending destruction before they are actually destroyed. Let's look at how this is implemented in the "Starter Content" sample's stats destruction systems:
* A `DestroyEntity` enableable component is added to entites that can be destroyed by the destroy pipeline. This component starts off disabled. A `DestroyEntityAuthoring` component is available to add it during baking.
* Towards the end of the frame, a `EntityDestructionSystemGroup` contains the `EntityDestructionSystem`, which handles destroying all entities with an enabled `DestroyEntity` component.
* A `StatsPreDestructionSystem` updates in `EntityDestructionSystemGroup` before `EntityDestructionSystem`. For each stats entities with an enabled `DestroyEntity` component, it will add all the dependent stat handles of that entity's stats to a list. This list represents all the stat handles that must be updated after the entity is actually destroyed.
* A `StatsPostDestructionSystem` updates in `EntityDestructionSystemGroup` after `EntityDestructionSystem`. For each stat handle in the list of stat handles to update, it will call `StatsAccessor.TryUpdateStat` to update the stat.

The dependent stats updated after the destruction of stat entities will discover in their modifier `Apply()` that their observed stat(s) no longer exist. They will then be free to decide what to do with this (self-remove, use last known value, use fallback value, etc...)

> Note: if you do not do what is suggested in this section, stats depending on stats of the destroyed entity will simply not automatically update upon the entity's destruction. They will keep the last known value of the destroyed stat. However, the next time their update is triggered for any reason, they will realize they now depend on a destroyed stat. They may choose to just keep the last known value at that moment. This could potentially be the desired logic, but may also not be in some cases.


## Advanced topics

#### Netcode

See [Netcode](./advanced-netcode.md)


#### Saving and loading

In order to save/load the state of stats and their modifiers, all fields of the following components and buffers must be serialized/deserialized:
* The `StatsOwner` component
* The `Stat` buffer
* The `StatModifier<T,S>` buffer
* The `StatObserver` buffer

Additionally, the `StatHandle` and `StatModifierHandle` fields stored in your components may also need to be serialized/deserialized if they are not added deterministically.

Keep in mind that the stats of one entity may depend on the stats of other entities, so if you want to save such entities, you must also save all the entities whose stats they depend on. `StatsUtilities` provides utilities for this:
* `StatsUtilities.GetOtherDependantStatEntitiesOfEntity` will return all stat entities that depend on the target entity.
* `StatsUtilities.GetStatEntitiesThatEntityDependsOn` will return all stat entities that this entity depends on.


#### Stat values as Ints

`StatsUtilities` includes the `AsInt` and `AsFloat` extension methods for `float`s and `int`s. This allows you to "store" an `int` value in a `float` value's memory range. This is different from casting between `int` and `float`. Casting `10.1f` to an `int` will result in a value of `10`. `AsInt` and `AsFloat` will instead just replace the underlying byte data so that an int can be losslessly stored in a `float` field, meaning using `AsInt()` on a value of `10.1f` will give a completely different and hard-to-guess value (not `10`).

If you want a certain stat to be treated as an `int` instead of a `float`:
* Whenever you change the stat value, use `StatsAccessor.TrySetStatBaseValueInt`, `StatsAccessor.TryAddStatBaseValueInt`, `StatsAccessor.TryMultiplyStatBaseValueInt` instead of the regular non-"Int" versions.
* Whenever you add a modifier to an int stat, make sure both the modifier type and the modifier stack know they should deal with the stat value as an `int`. For example, for a modifier that adds a value to a stat that should be treated as an `int`, the modifiers stack should hold an `int AddInt` field, and the modifier should add to the stack's `AddInt` field. Then, in the stack's `Apply()` we'd convert the stat's `baseValue` to an `int` using `int intValue = statBaseValue.AsInt()`, add the `AddInt` value to it, and finally set the `float` stat value to `statValue = intValue.AsFloat();`.
* Adding float-based modifiers to an int stat, or int-based modifiers to a float stat will lead to highly unpredictable results. Make sure it's always all float or all int for the same stat.
    * To reduce risks of mistakes in your game, you may wish to treat all stats and modifier values as ints, and then have a project-wide (or per-stat) divider value that you use to get decimal numbers.


#### Dynamic stat modifiers

Once added, a stat modifier cannot be directly changed unless it changes itself in its own `Apply()` code. If you wanted to change a modifier's values, you'd have to first remove the modifier using its `StatModifierHandle`, and then add back a new version of the modifier.

Thankfully however, this package provides a different way for modifier values to change dynamically, without having to do this expensive remove/add modifier operation every time. 

Let's work with the simple example of a weapon damage modifier that adds a damage bonus that increases as our hero's health ratio lowers. Instead of removing, changing, and re-adding the modifier to the weapon's "Damage" stat every frame with an updated damage bonus calculated based on the current hero health ratio, we would make the modifier depend on 2 stats when adding it the first time: the "CurrentHealth" stat of our hero entity, and the "MaxHealth" stat of our hero entity. If these health values were not already stats, they should be converted to stats. Alternatively, we could even choose to create a "HealthRatio" stat that has a modifier that makes it depend on the "CurrentHealth" and "MaxHealth" stats, and the modifier sets the stat value to "CurrentHealth/MaxHealth". When either the "CurrentHealth" or "MaxHealth" stats are changed, it would trigger an update of the "HealthRatio" stat, which would trigger an update of the weapon damage stat. The damage modifier will use these health stat values in its `Apply()` logic to calculate the bonus, and we will now have a weapon damage modifier that automatically scales with our hero's health ratio.

Basically, any value of a modifier that may change after adding the modifier can be turned into another stat that the modifier observes. Changing these values after adding the modifier just means changing the value of those observed stats.

Stats that a modifier depends on must be stored as `StatHandle`s in the modifier struct, and must be added to a list in the modifier's `AddObservedStatsToList()`. You can refer to the `SampleStatModifier` from the "Starter Content" sample for an example of this.


#### Self-removing modifiers

Modifier trigger events can be used for self-removing modifiers. For example, if you need modifiers to self-remove whenever they detect that a stat they depend on no longer exists:
* In the modifier's `Apply()`, if `statsReader.TryGetStat` returns false, 
    * set `shouldProduceModifierTriggerEvent` to true. This will create a trigger event for this modifier.
    * set a `MustRemove` field in the modifier struct to true.
* In the system that processes your modifier trigger events,
    * In the event data, if `modifierEvent.Modifier.MustRemove == true;`, remove the modifier using `StatsAccessor.TryRemoveStatModifier(event.ModifierHandle, ref statsWorldData);`


#### Complex modifiers stack

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


#### Stats that are only evaluared on-demand

In certain cases, it can be useful for performance reasons to have certain stats that only recompute on-demand (meaning they don't automatically react to stat changes of dependent stats). This can be the case if a stat that needs to be read very rarely depends on another stat that changes very frequently.

In order to do this, the modifiers we add to the stat must NOT register their observed stats in the modifier's `AddObservedStatsToList`. This will prevent reactive stat changes for the modifier's affected stat. Then when we do want to access the up-to-date value of this stat, we simply call `StatsAccessor.TryUpdateStat` before reading the value.

Be careful with this approach though, because if other stats depend on this on-demand stat, these other stats will also only be recomputed when the on-demand stat is manually updated. 