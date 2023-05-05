
[Home](./index.md)

# Netcode

The following sections provide tips on using this solution in a netcode context.


## If attributes do not take part in prediction

If attributes do not take part in prediction, only the `AttributeValues` fields in your components have to be synchronized on ghosts, using `[GhostField]`.

All attribute changes will happen on the server, and the server will send to final `AttributeValues` to clients.

--------------------------------------

## If attributes must take part in prediction

#### Ghost variants

If attributes must take part in prediction, you must add the following Ghost Variants in a .cs file in your project, and use these variants on your ghosts:

```cs
[GhostComponentVariation(typeof(AttributesOwner))]
[GhostComponent()]
public struct AttributesOwner_GhostVariant
{
    [GhostField()]
    public uint ModifierIDCounter;
}

[GhostComponentVariation(typeof(AttributeModifier))]
[GhostComponent()]
public struct AttributeModifier_GhostVariant
{
    [GhostField()]
    public ModifierType ModifierType;
    [GhostField()]
    public float ValueA;
    [GhostField()]
    public float ValueB;
    [GhostField()]
    public AttributeReference AttributeA;
    [GhostField()]
    public AttributeReference AttributeB;
    [GhostField()]
    public uint __internal__modifierID;
    [GhostField()]
    public int __internal__affectedAttributeType;
}

[GhostComponentVariation(typeof(AttributeObserver))]
[GhostComponent()]
public struct AttributeObserver_GhostVariant
{
    [GhostField()]
    public AttributeReference ObserverAttribute;
    [GhostField()]
    public int ObservedAttributeType;
    [GhostField()]
    public uint Count;
}
```

You will need to update the `AttributeModifier_GhostVariant` whenever you make any change to the data in `AttributeModifier` (when you add/change/remove fields). The entire `AttributeModifier` must be synchronized.

#### Attribute values

The `AttributeValues` fields in your components have to be synchronized on ghosts, using `[GhostField]`.


#### Attribute owners

Your entities that have attributes must be `Predicted` or `Owner Predicted` ghosts, with the `AttributeOwnerAuthoring` component on the ghost prefab. This will ensure that the `AttributeModifier` and `AttributeObserver` buffers are synchronized and rolled-back for prediction.


#### Attribute changes

When attributes are part of prediction, attribute changes must be done in prediction systems both on client and server (using `AttributeChanger` or `AttributeCommand`s).

--------------------------------------

## If some attributes are predicted but some aren't

It is possible to have some attributes predicted and some not. Simply follow the guidance in the previous sections to set up each of those.

The only things to be mindful of are:
* Don't make non-prediction code try to add attribute modifiers to predicted entities (the added modifier will just get overwritten next time prediction runs).
* Don't make prediction code try to add attribute modifiers to non-predicted entities (the modifier will get added multiple times).
* Don't add a modifier that makes a predicted attribute depend on a non-predicted attribute (this could break prediction). 
* `AttributeChanger.UpdateData` must be given a different `AttributesSingleton` entity as parameter depending on if it's used by a prediction system or not. This is so that non-predicted modifier IDs incrementation doesn't mess with predicted modifier IDs incrementation. One way to do this is to:
    * Create two new tag component types: `PredictedAttributesSingleton` and `LocalAttributesSingleton`.
    * Make sure two entities with `AttributesSingleton` are created, and add the tags from the previous point to them (each has a different one).
    * When calling `AttributeChanger.UpdateData`, instead of calling `SystemAPI.GetSingletonEntity<AttributesSingleton>()` to get the singleton entity that is passed as parameter, use `SystemAPI.GetSingletonEntity<PredictedAttributesSingleton>()` or `SystemAPI.GetSingletonEntity<LocalAttributesSingleton>()`.
    * Change any code that previously relied on `if (SystemAPI.HasSingleton<AttributesSingleton>())` or `RequireForUpdate<AttributesSingleton>()`.