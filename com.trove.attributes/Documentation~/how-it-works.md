
[Home](./index.md)

# How it works

> **IMPORTANT** When using this package, you must start by navigating to the "Samples" tab of the "Trove Attributes" package to download the "User Content" (it's a necessary part of the solution, but it's installed in your project folders because it's meant to be customized).

## Summary

#### Attributes
An attribute is an `AttributeValues` struct in a user-created component. Users define their attribute types, getters, and setters in files that come with the User Content. `AttributeValues` contains a `float BaseValue` representing the balue before any modifiers are applied, and a `float Value` representing the value after all modifiers are applied. 

#### Attribute modifiers
Attribute modifiers and the modifiers stack are defined in `AttributeModifier` and `AttributeModifierStack` in the User Content. `AttributeModifier` is a single common struct type that can act as any of the various modifier types in your game, and `AttributeModifierStack` represents the accumulation of all modifier operations affecting a certain attribute when that attribute is being recalculated. 

An attribute modifier affects a specific attribute type on a specific entity, and is given a unique identifier when added (so it can be removed later).

#### Attribute owners
Any entity that wants to support having modifiers applied to its attributes must have the `AttributeOwnerAuthoring` component during baking in order to add the required dynamic buffers to it. Alternatively, these buffers can be added at runtime using `AttributeUtilities.MakeAttributeOwner`.

#### Attribute references and observers
Attribute modifiers can use the values of any other attribute in their calculations, using an `AttributeReference` field. If an attribute A has a modifier that uses an `AttributeReference` to attribute B, then attribute A will be marked as an "observer" of attribute B, and it will automaticaly be recalculated by the system whenever attribute B's value changes. 

#### Attribute changes
Attribute changes (either changing the value or adding/removing modifiers) must always be done with an `AttributeChanger`, or with `AttributeCommand`s (which are just instructions to perform a change using `AttributeChanger` later in the frame). This is required in order to make all of the "reactive" logic of the system work.


## Table of contents

* [Defining attributes](./how-it-works-defining-attributes.md)
    * [Attribute type](./how-it-works-defining-attributes.md#attribute-type)
    * [Attribute component](./how-it-works-defining-attributes.md#attribute-component)
    * [Attribute getter and setter](./how-it-works-defining-attributes.md#attribute-getter-and-setter)
    * [Attribute owner](./how-it-works-defining-attributes.md#attribute-owner)
* [Changing attributes](./how-it-works-changing-attributes.md)
* [Attribute modifiers](./how-it-works-attribute-modifiers.md)
    * [Creating, adding and removing modifiers](./how-it-works-attribute-modifiers.md#creating-adding-and-removing-modifiers)
    * [Modifier type](./how-it-works-attribute-modifiers.md#modifier-type)
    * [Applying modifiers to a stack](./how-it-works-attribute-modifiers.md#applying-modifiers-to-a-stack)
    * [Modifier data](./how-it-works-attribute-modifiers.md#modifier-data)
    * [Modifiers accessing attribute values](./how-it-works-attribute-modifiers.md#modifiers-accessing-attribute-values)
* [Attribute commands](./how-it-works-attribute-commands.md)
    * [Writing and executing commands](./how-it-works-attribute-commands.md#writing-and-executing-commands)
    * [Adding modifiers with commands](./how-it-works-attribute-commands.md#adding-modifiers-with-commands)
    * [Why use commands](./how-it-works-attribute-commands.md#why-use-commands)
    * [Commands and NativeStream](./how-it-works-attribute-commands.md#commands-and-nativestream)

