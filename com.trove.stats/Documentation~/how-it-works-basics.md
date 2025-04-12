
[Home](../README.md)

# How it works - Basics

## Table of Contents
* [Starter Content](#starter-content)
* [Concepts](#concepts)
* [Creating stats](#creating-stats)
* [Using the StatsAccessor](#using-the-statsaccessor)
* [Reading stats](#reading-stats)
* [Changing stat values](#changing-stat-values)
* [Adding stat modifiers](#adding-stat-modifiers)
* [Removing stat modifiers](#removing-stat-modifiers)
* [Stat events](#stat-events)
* [Destroying stat entities](#destroying-stat-entities)

---


## Starter Content
In the "Samples" tab of the "Trove Stats" package in the Package Manager window, you can download the "Starter Content" sample. This includes basic customizable setup code for your stats, and it is the recommended starting point for starting with this package. Here's an overview of the files:
* `SampleStats`: A component holding example `StatHandle`s (a `StatHandle` holds a reference to a specific stat on a specific entity).
* `SampleStatsAuthoring`: An authoring component for `SampleStats`.
* `SampleStatModifier`: An example of a stats modifier and a modifiers stack, that can be extended and customized.
* `SampleStatsWorldSystem`: A system that creates the `StatsWorldData` singleton, used for most stats operations.
* `SampleStatEventsSystem`: An example of how to process stat events.
* `StatDestructionSystems`: An example of how to process stat entity destruction (see [Destroying stat entities](./how-it-works-basics.md#destroying-stat-entities)).


## Concepts

#### Stats
A `Stat` is a `DynamicBuffer` element containing a `BaseValue`, a `Value`, and some other internal data. `BaseValue` represents the value before any modifiers are applied, and a `Value` represents the value after all modifiers are applied. 

When created, stats are given a unique `StatHandle`, so they can be referenced later.

#### Stat Modifiers and Observers
Stat modifiers and the modifiers stack are defined in user code. The Trove Stats "Starter Content" sample includes a starter version of stat modifiers and modifiers stack. Stat modifiers are implemented as a single struct that can act as any of the various modifier types in your game, and the stat modifiers stack represents the accumulation of all modifier operations affecting a certain stat when that stat is being recalculated. 

A stat modifier affects a specific stat on a specific entity, and is given a unique `StatModifierHandle` when added (so it can be removed later).

A stat modifier can also be an "observer" of other stats, meaning that when that stat changes, the modifier will know that it has to re-apply itself to the target stat. For example if a game wants to apply a bonus to the Strength stat based on the Dexterity stat: it would add a modifier to the Strength stat, and that modifier would be an observer of the Dexterity stat. Now when Dexterity changes, the modifier will tell the Strength stat that is has to recalculate its value and re-apply all of its modifiers (including the one that gives a bonus based on Dexterity).

#### Stats Access
**IMPORTANT: In this package, you should never directly change the data in the `Stat`, `StatModifier`, `StatObserver` buffers directly**. The data stored here must be handled by the various tools that the Stats package provides, and any change that doesn't go through the stats APIs could break things.
* `StatsAccessor` is the main tool you must use for all stat changes: stat creation, stat value change, stat modifier addition/removal. Note that it cannot be used in parallel, as stat changes can affect multiple entities.
* `StatsUtilities` can be used when you only need to read (not write) stats.


## Creating stats

The `SampleStatsAuthoring` (from the "Starter Content") demonstrates how to bake stats to an entity. First, creates a `StatsBaker` by using `StatsUtilities.BakeStatsComponents`. And then, create stats by calling `statsBaker.CreateStat` and storing the resulting `StatHandles` in your own components.

In order to make an entity support stats at runtime, you can use `StatsUtilities.AddStatsComponents`

In order to create stats at runtime, you can use `StatsAccessor.TryCreateStat`. (See [Using the StatsAccessor](#using-the-statsaccessor))


## Using the StatsAccessor

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


## Reading stats

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


## Changing stat values

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


## Adding stat modifiers

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

Adding modifiers this way will automatically and instantly recompute any other stat values that depend on this stat.

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


## Removing stat modifiers

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


## Stat events

> Note: You can take a look at the `SampleStatEventsSystem` in the "Starter Content" sample, for an example of how to setting up a system to process stat events.

Two types of stat events are supported:
* Stat change events occur whenever a stat's value has changed. These events contain the `StatHandle` of the changed stat, the previous values, and the current values.
* Modifier trigger events occur whenever a stat modifier has been applied or re-applied to a stat. These events contain the `StatModifierHandle` of the triggered modifier, as well as the data of the modifier struct at the moment of triggering the event. 

For stat change events, you must make sure the stat itself supports change events. You can configure this either during stat creation (a bool parameter for the `CreateStat` and `TryCreateStat` methods handles this), or by using `StatsAccessor.TrySetStatProduceChangeEvents`. 

For modifier trigger events, stat modifiers are responsible for setting the value of the `out bool shouldProduceModifierTriggerEvent` parameter in their `Apply` function. 

Events can be accessed through the `StatsWorldData`, which is stored in `StatsWorldSingleton`.


## Destroying stat entities

> Note: a pre-made version of what's described in this section is available as part of the "Starter Content" sample, in the "Stats Destruction" folder.

Special consideration must be taken when destroying stat entities, because of the fact that other stat entities may depend on them. When destroying these entities, any stat on other entities that depend on stats of the destroyed entity should be updated, so they can decide what to do with the fact that they now depend on a nonexistent stat. This won't happen automatically.

Because of this, it is recommended that you setup an entity destruction pipeline in your project, that gives you a chance to perform some logic on entities pending destruction before they are actually destroyed. Let's look at how this is implemented in the "Starter Content" sample's stats destruction systems:
* A `DestroyEntity` enableable component is added to entites that can be destroyed by the destroy pipeline. This component starts off disabled. A `DestroyEntityAuthoring` component is available to add it during baking.
* Towards the end of the frame, a `EntityDestructionSystemGroup` contains the `EntityDestructionSystem`, which handles destroying all entities with an enabled `DestroyEntity` component.
* A `StatsPreDestructionSystem` updates in `EntityDestructionSystemGroup` before `EntityDestructionSystem`. For each stats entities with an enabled `DestroyEntity` component, it will add all the dependent stat handles of that entity's stats to a list. This list represents all the stat handles that must be updated after the entity is actually destroyed.
* A `StatsPostDestructionSystem` updates in `EntityDestructionSystemGroup` after `EntityDestructionSystem`. For each stat handle in the list of stat handles to update, it will call `StatsAccessor.TryUpdateStat` to update the stat.

The dependent stats updated after the destruction of stat entities will discover in their modifier `Apply()` that their observed stat(s) no longer exist. They will then be free to decide what to do with this (self-remove, use last known value, use fallback value, etc...)

> Note: if you do not do what is suggested in this section, stats depending on stats of the destroyed entity will simply not automatically update upon the entity's destruction. They will keep the last known value of the destroyed stat. However, the next time their update is triggered for any reason, they will realize they now depend on a destroyed stat. They may choose to just keep the last known value at that moment. This could potentially be the desired logic, but may also not be in some cases.

