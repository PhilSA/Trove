
[Advanced topics](./advanced.md)

# Netcode

## Non-predicted (server-only) reasoners

A majority of games will never need AIs to be part of prediction, since AI will run only on the server, and it's only the *outcome* of the AI decisions that have to be synchronized (ex: if an AI's outcome is to move a character, you only have to sync the transform component).


## Predicted reasoners

However, if you're in a situation where you want Trove Utility AI to parttake in netcode prediction, you can add the following ghost variants to your project:

```cs
[GhostComponentVariation(typeof(Reasoner))]
[GhostComponent()]
public struct Reasoner_GhostVariant
{
    [GhostField()]
    public int __internal__actionsVersion;
    [GhostField()]
    public int __internal__considerationsVersion;
    [GhostField()]
    public int __internal__actionIDCounter;
    [GhostField()]
    public int __internal__considerationIDCounter;
    [GhostField()]
    public int __internal__highestActionConsiderationsCount;
    [GhostField()]
    public byte __internal__mustRecomputeHighestActionConsiderationsCount;
}

[GhostComponentVariation(typeof(Action))]
[GhostComponent()]
public struct Action_GhostVariant
{
    [GhostField()]
    public int Type;
    [GhostField()]
    public int IndexInType;
    [GhostField()]
    public float ScoreMultiplier;

    [GhostField()]
    public byte __internal__flags;
    [GhostField()]
    public int __internal__id;
    [GhostField()]
    public float __internal__latestScoreWithoutMultiplier;
}

[GhostComponentVariation(typeof(Consideration))]
[GhostComponent()]
public struct Consideration_GhostVariant
{
    [GhostField()]
    public ParametricCurve Curve;

    [GhostField()]
    public byte __internal__flags;
    [GhostField()]
    public int __internal__id;
    [GhostField()]
    public int __internal__actionIndex;
    [GhostField()]
    public float __internal__input;
}
```

The ghosts that have reasoners would be predicted, and the code that updates/changes reasoners/actions/considerations in any way would be part of the prediction group.

You'll also have to make sure all the `ActionReference`s and `ConsiderationReference`s in your own components are synced as well.