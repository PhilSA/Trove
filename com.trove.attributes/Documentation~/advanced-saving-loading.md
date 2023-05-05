
[Home](./index.md)

# Saving and loading attributes state

In order to save/load the state of attributes and their modifiers, the following component fields must be serialized/deserialized on an entity that has attributes:
* The `AttributeValues` in your own components
* `AttributesOwner.ModifierIDCounter`
* `AttributeModifier.ModifierType`
* `AttributeModifier.__internal__modifierID`
* `AttributeModifier.__internal__affectedAttributeType`
* All other `AttributeModifier` fields that represent the custom user data used by modifiers (`ValueA`, `ValueA`, `AttributeA`, `AttributeB`, etc...)
* `AttributeObserver.ObserverAttribute`
* `AttributeObserver.ObservedAttributeType`
* `AttributeObserver.Count`


## Saving only certain attributes on certain entities

In order to save/load the state of an AttributeA on an EntityA, you must make sure not to forget to save all the other attributes it depends on as well:
* Save AttributeA's `AttributeValues` in your own component
* Save EntityA's `AttributesOwner.ModifierIDCounter`
* For each `modifier` in EntityA's `AttributeModifier` buffer:
    * if `modifier.AffectedAttributeType` is the type of AttributeA
        * Save `modifier.ModifierType`
        * Save `modifier.__internal__modifierID`
        * Save `modifier.__internal__affectedAttributeType`
        * Get all the attributes that your attribute depends on:
            * call `modifier.AddObservedAttributesToList` on this `AttributeModifier`. This will will a list with the `AttributeReference`s this modifier depends on
            * make sure all `AttributeReference`s returned by that last step are also saved. Repeat this list of steps for those attributes, but add these extra steps as well:
                * For each `observer` in the referenced attribute entity's `AttributeObserver` buffer:
                    * if `observer.ObservedAttributeType` is the type of AttributeA
                        * Save `observer.ObserverAttribute`
                        * Save `observer.ObservedAttributeType`
                        * Save `observer.Count`

        