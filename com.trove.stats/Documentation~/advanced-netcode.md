
[Home](../README.md)

# Netcode

The following sections provide tips on using this solution in a netcode context.


## If stats do not take part in prediction

If stats do not take part in prediction, only the `Stat` buffer element has to be synchronized on ghosts, and in this component, only the stat value field(s) matter. You can use the following Ghost Variant for this:

```cs
[GhostComponentVariation(typeof(Stat))]
[GhostComponent()]
public struct Stat_GhostVariant
{
    [GhostField()] // Could also be "SendData = false" if you don't use it
    public float BaseValue;
    [GhostField()]
    public float Value;

    [GhostField(SendData = false)]
    public int LastModifierIndex;
    [GhostField(SendData = false)]
    public int LastObserverIndex;
    
    [GhostField(SendData = false)]
    public byte ProduceChangeEvents;
}
```

All attribute changes and modifier calculations will happen on the server, and the server will send to final `Stat` buffer values to clients.

You may also want to `[GhostField]` your stored `StatHandle`s in components if their creation is not deterministic.

--------------------------------------

## If attributes must take part in prediction

#### Ghost variants

If attributes must take part in prediction, all stats data must be synced. You can use the following Ghost Variants for this:

```cs
[GhostComponentVariation(typeof(StatsOwner))]
[GhostComponent()]
public struct StatsOwner_GhostVariant
{
    [GhostField()]
    public uint ModifierIDCounter;
}

[GhostComponentVariation(typeof(Stat))]
[GhostComponent()]
public struct Stat_GhostVariant
{
    [GhostField()] 
    public float BaseValue;
    [GhostField()]
    public float Value;

    [GhostField()]
    public int LastModifierIndex;
    [GhostField()]
    public int LastObserverIndex;
    
    [GhostField())]
    public byte ProduceChangeEvents;
}

// Note: Replace "TStatModifier" and "TStatModifierStack" with your stat modifier & stack types
[GhostComponentVariation(typeof(StatModifier<TStatModifier, TStatModifierStack>))]
[GhostComponent()]
public struct StatModifier_GhostVariant
{
    [GhostField()]
    public uint ID;
    [GhostField()]
    public TStatModifier Modifier;
    [GhostField()]
    public int PrevElementIndexData;
}

[GhostComponentVariation(typeof(StatObserver))]
[GhostComponent()]
public partial struct StatObserver_GhostVariant
{
    [GhostField()]
    public StatHandle ObserverHandle;
    [GhostField()]
    public int PrevElementIndexData;
}
```

You may also want to `[GhostField]` your stored `StatHandle`s in components if their creation is not deterministic.
