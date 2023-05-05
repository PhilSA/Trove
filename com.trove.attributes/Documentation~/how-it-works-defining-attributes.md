
[How it works](./how-it-works.md)


# Defining attributes

## Attribute type

In order to add a new type of attribute to your game, start by adding add an element to the already-existing `AttributeType` enum from the [User Content](./howtoinstall.md). This will be an identifier for the new attribute type. For example:

```cs
public enum AttributeType
{
    Strength,
    Dexterity,
    Intelligence,
}
```

--------------------------------------

## Attribute component

Then, create components with `AttributeValues` fields for storing these attribute values. `AttributeValues` is where your attribute's "value" and "base value" are stored. The "base value" is the value before any modifiers were applied, and the "value" is the final value after all modifiers were applied to the attribute. 

For example:

```cs
public struct Strength : IComponentData
{
    public AttributeValues Values;
}

public struct Dexterity : IComponentData
{
    public AttributeValues Values;
}

public struct Strength : IComponentData
{
    public AttributeValues Values;
}
```

If you wanted to, you could also choose to store multiple attributes in the same component, as well as any other data. Like this:

```cs
public struct Character : IComponentData
{
    public AttributeValues Strength;
    public AttributeValues Dexterity;
    public AttributeValues Intelligence;

    public int Level;
    public float3 Velocity;
}
```

--------------------------------------

## Attribute getter and setter

Then, in the already-exiting `AttributeGetterSetter` struct from the [User Content](./howtoinstall.md), implement the code that knows how to get and set your attribute's value on an entity, based on the `AttributeType` :
* In the `AttributeGetterSetter`, store component lookups for all of your attribute types,
* In `AttributeGetterSetter.OnSystemCreate`, create those lookups based on a system's `SystemState`
* In `AttributeGetterSetter.OnSystemUpdate`, update those lookups based on a system's `SystemState`
* In `AttributeGetterSetter.GetAttributeValues`, get (read) the `AttributeValues` from your attribute component
* In `AttributeGetterSetter.SetAttributeValues`, set (write) an `AttributeValues` in your attribute component
s
(Refer to the existing example `AttributeGetterSetter` implementation from the [User Content](./howtoinstall.md) for more details on how these should be implemented)

--------------------------------------

## Attribute owner

Finally, an entity needs a few more dynamic buffers in order to be able to have modifiers added to its attributes. You can use the `AttributeOwnerAuthoring` to handle baking for these entities, or you could also use `AttributeUtilities.MakeAttributeOwner` at runtime. This will add dynamic buffers for `AttributeModifier`s and `AttributeObserver`s to the entity, but you will likely never have to directly deal with these buffers in code.

