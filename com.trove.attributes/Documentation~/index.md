
# Trove Attributes

## Overview

Trove Attributes can be used to effortlessly setup gameplay mechanics such as RPG character/equipment attributes, buffs, effects, roguelike-style modifiers, etc... 

Attributes represent "numbers" that are:
* **Associated with a type and an entity**: these attributes live on an entity, and they are associated with a type. Each entity can have 0 or 1 attribute of any given type. For example: you could define an attribute of type `Dexterity`, and query if a certain entity has an attribute of type `Dexterity`.
* **Referencible in a generic way**: you can get and store a reference to a specific attribute instance (an attribute of a specific type on a specific entity), and you can get/set its value in a generic way if needed (without having to know in advance what type of component it's part of).
* **Modifiable with trackable & combineable modifiers**: you can add modifiers of various types to these attributes. The modifiers are individually-identifiable so that they can be removed later.
* **Inter-dependent and Reactive**: one attribute can depend on the final modified value(s) of other attributes. Changing one attribute automatically triggers a recalculation of all attributes that depend on it (directly or indirectly).

If there are "numbers" in your game that would need any or all of the above characteristics, it could be worth turning them into "attributes".

-----------------------------------------

## Table Of Contents

* [Example usage scenario](./examplescenario.md)
* [Usability and performance](./usability-performance.md)
* [How it works](./how-it-works.md)
* [Advanced topics](./advanced.md)