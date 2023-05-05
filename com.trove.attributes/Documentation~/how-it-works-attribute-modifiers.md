
[How it works](./how-it-works.md)

# Attribute modifiers

Attribute modifiers are uniquely-identifiable "rules" that you add to a specific attribute's final value calculation. Examples of modifiers would be:
* a modifier that adds a value of `5` to an attribute
* a modifier that multiplies the value of an attribute by `1.2`, after all modifiers of type "add" were added
* a modifier that clamps the final value of an attribute (after all other operations) to a minimum and maximum
* a modifier that sets an attribute's value to the average value of 3 other attributes before the other modifier operations are performed
* etc...

--------------------------------------

## Creating, adding and removing modifiers

Modifiers can be added to a specific attribute using `AttributeChanger.AddModifier`. This function returns a `ModifierReference`, which can later be used to remove this specific modifier using `AttributeChanger.RemoveModifier`. A `ModifierReference` consists of an `AttributeReference` that the modifier affects, and a unique `uint` ID.

Creating modifiers (before adding them) is done with the various `AttributeModifier.Create_(...)` static functions, for the built-in modifier types. You could choose to use the same approach for your own custom modifier types, if any. For example, this is how you would create and add a "AddFromAttribute" modifier to the "Strength" attribute on an entity:

```cs
public void OnUpdate(ref SystemState state)
{
    _attributeChanger.UpdateData(ref state);

    // When pressing space, adds a modifier to all units in the game that have a Strength and a Intelligence attribute.
    // That modifier makes the Strength attribute add the value of the Intelligence attribute on that unit to its own value.
    if(Input.GetKeyDown(KeyCode.Space))
    {
        foreach (var (unit, entity) in SystemAPI.Query<MyUnit>().WithEntityAccess())
        {
            // Add the modifier
            _attributeChanger.AddModifier(
                new AttributeReference(entity, (int)AttributeType.Strength), // This is the attribute we want the modifier to affect.
                AttributeModifier.Create_AddFromAttribute(new AttributeReference(entity, (int)AttributeType.Intelligence)), // This is the created modifier we want to add.
                out ModifierReference addedModifierReference); // This is the modifier reference that we could store and use to remove the modifier later.

            // Remove the modifier we just added (just as an example)
            _attributeChanger.RemoveModifier(addedModifierID);

            // This would remove all modifiers affecting any attribute type on that entity (again, just as an example)
            _attributeChanger.RemoveAllModifiers(entity);
        }
    }
}
```

Modifiers are immediately and automatically applied as soon as they are added with `AttributeChanger.AddModifier`, and they will be applied again whenever the attribute needs to be recalculated by the system. 

--------------------------------------

## Modifier type

In order to create modifiers in your project, start by adding a new element to the `ModifierType` struct from the [User Content](./howtoinstall.md):

```cs
public enum ModifierType
{
    Set,
    SetFromAttribute,

    Add,
    AddFromAttribute,

    AddMultiplier,
    AddMultiplierFromAttribute,

    Clamp,
    ClampFromAttribute,
}
```

--------------------------------------

## Applying modifiers to a stack

Then, in the `AttributeModifier.ApplyModifier` function, add a case for your `ModifierType` in the switch statement. This is where you'll implement how a modifier applies itself to a `AttributeModifierStack`, which is a temporary struct that's created to contain the accumulation of all the applied modifiers an attribute when it gets recalculated.

> Important: `AttributeModifier.ApplyModifier` gives you access to the `AttributeGetterSetter` in case you need to read the value of an attribute. However, you must be careful to **never** "Set" the value of an attribute in `AttributeModifier.ApplyModifier`, because doing so will fail to trigger the required attribute value change updates.

Defining how a `AttributeModifierStack` applies itself to an attribute's value is done in `AttributeModifierStack.CalculateFinalValue`. Here, you can customize the various steps of the modifiers stack. For example, you could define a second "Add" step that comes after the "AddMultiplier" step, etc...


--------------------------------------

## Modifier data

The `AttributeModifier` struct must be used to represent all of the different kinds modifiers in your game. This means that the fields contained in this struct must represent the common set of data used across all of your different modifier types, and therefore that the same field might mean different things depending on which type of modifier is using it. For example, `AttributeModifier.ValueA` might mean "value added" for the "Add" modifier, but it would mean "min value" for the "Clamp" modifier.

--------------------------------------

## Modifiers accessing attribute values

Some fields in `AttributeModifier` are of `AttributeReference` type. These can be used by modifiers to get the value of any other attribute as part of their calculations. For example, the "AddMultiplierFromAttribute" modifier uses the attribute assigned to `AttributeModifier.AttributeA` in order to evaluate how much value it should add to the multiplier. This mechanism is what allows one attribute to depend on another.

When your modifiers use `AttributeReference` fields in their calculations, you must remember to declare that they are "observers" of these attributes (meaning they will react to changes of that attribute). This is done in `AttributeModifier.AddObservedAttributesToList`. Here, all `AttributeReference` fields used by the current modifier type must be manually added to the `observedAttributes` list. By doing so, the system will make sure that the attribute the modifier affects (`AttributeModifier.AffectedAttributeType`) on the entity will be automatically recalculated whenever any of the observed attributes change.
