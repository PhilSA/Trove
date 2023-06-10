
[How it works](./how-it-works.md)

# Destroying attribute owners

Special considerations are required when destroying any entity that has attributes. Imagine this scenario:
* `EntityA` has `AttributeA` with a value of `5`.
* `EntityB` has `AttributeB` with a value of `10`.
* `EntityB` gets a modifier that adds the value of `EntityA`'s `AttributeA` to `AttributeB`. The value of `EntityB`'s `AttributeB` is now `15`.
* `EntityA` is destroyed.

By default, when `EntityA` is destroyed, there is nothing that tells `EntityB` that is must now recalculate the value of its `AttributeB` because it depends on another attribute that no longer exists (`AttributeA`).

The recommended way to solve this is to always call `AttributeUtilities.NotifyAttributesOwnerDestruction` before destroying any entity that has attributes. Here's a code example:

```cs

// In a job...
{
    // Create an entity to hold attribute commands
    Entity commandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out DynamicBuffer<AttributeCommand> attributeCommands);

    // Calling this will add attribute commands that will recalculate all attributes observing any attribute on this entity.
    // Here the "observersBuffer" is the buffer of AttributeObserver on the entity we are about to destroy.
    AttributeUtilities.NotifyAttributesOwnerDestruction(ref observersBuffer, ref attributeCommands);

    // Destroy the attributes owner entity
    ecb.DestroyEntity(entity);
}
```

In this example, here's what will happen later in the frame:
* The ecb will play back, creating the attribute commands entity and destroying the attributes owner entity
* The `ProcessAttributeChangerCommandsSystem` will update, meaning all of our commands for recalculating attributes that observed the destroyed entity will be processed
* All attributes are now up-to-date


## Why not cleanup components?

An alternative approach to attribute destruction notifications might look like this:
* Attribute-owning entities store all attribute observers in a cleanup component
* A job iterating on these cleanup components and `WithNone<AttributesOwner>` takes care of calling a recalculation of all observers.

The main thing preventing this approach from being possible is the fact that cleanup components cannot be baked. Imagine this scenario:
* In a job, we want to instantiate an attributes-owning entity via ECB, and add modifiers to it immediately.
* Since cleanup components cannot be baked, there is nothing guaranteeing that the attribute observers cleanup buffer is already present on the entity. But adding modifiers would require this buffer to be present.
* ...therefore, users would have to remember to always add an observers cleanup buffer to the attribute entities they instantiate. It wouldn't be much better for usability than the recommended approach above, because users would still have a manual step that they must always remember to do. 
* Trying to automate the adding of this buffer with a system that adds it if not present would complicate usability of the API, because then users would have to remember that attribute-owning entities are only valid for changes past a certain point in the frame, and modifiers cannot be directly added to freshly-spawned entities.