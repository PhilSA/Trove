
[How it works](./how-it-works.md)

# Attribute commands

`AttributeCommand`s are used to schedule an attribute change to be applied at a later point in the frame, using an `AttributeChanger`. Here is how to use them.

## Writing and executing commands

In order to write commands for later execution, first you must get an entity that has a `DynamicBuffer<AttributeCommandElement>`. Then use an `EntityCommandBuffer` to append to the `AttributeCommandElement` buffer on that entity. For example:

```cs
using AttributeCommand = Trove.Attributes.AttributeCommand<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;

[BurstCompile]
public partial struct MyAttributeCommandJob : IJobEntity
{
    public Entity CommandsEntity;
    public float DeltaTime;
    public EntityCommandBuffer ECB;

    void Execute(Entity entity, in StrengthChanger strengthChanger)
    {
        ECB.AppendToBuffer<AttributeCommandElement>(CommandsEntity, AttributeCommand.Create_AddBaseValue(new AttributeReference(entity, (int)AttributeType.Strength), strengthChanger.ChangeRate * DeltaTime));
    }
}
```

>Notice that the `AttributeCommand` that we create is implicitly casted to the `AttributeCommandElement` type, which is just the buffer element type containing the `AttributeCommand`. 

While nothing is stopping you from adding `AttributeCommandElement`s to multiple entities, keep in mind that if the order of commands matters for your use case, you should add them all to the same entity (a singleton entity for all commands would be good for this). This ensures that they will get processed in the exact order they were created.

In order to execute those commands, you must either wait for the `ProcessAttributeChangerCommandsSystem` to update, or create a system that will update the `ProcessAttributeChangerCommandsSystem` manually at a different point in the frame, after the ECB used to write commands has played back. The `ProcessAttributeChangerCommandsSystem` schedules a single-threaded job that makes an `AttributeChanger` execute all written commands one by one.

--------------------------------------

## Adding modifiers with commands

There is one particularity to be aware of when adding modifiers with an `AttributeCommand`; it will not return the modifier reference immediately. The modifier reference will only be created when the commands will be processed. In order to get those modifier references, you can pass a `Entity modifierReferenceNotificationTarget` and a `notificationID` to the "AddModifier" command. The `modifierReferenceNotificationTarget` entity is an entity with a `DynamicBuffer<ModifierReferenceNorification>` already added on it, which will receive the added modifier reference once the command gets processed. It is your responsibility to make sure that this target entity already has the buffer component. As for the `notificationID`, this is a custom ID you can assign to the "AddModifier" command in order to identify the modifier reference notifications that get added to the `DynamicBuffer<ModifierReferenceNorification>`.

--------------------------------------

## Why use commands

There are several reasons why you'd want to use `AttributeCommand`s:
* If you are in a job that instantiates a prefab via ECB, and you want to add a modifier to that new prefab instance, you wouldn't be able to use `AttributeChanger` to do so. This is because `AttributeChanger` needs the entity to be fully created (not just an ECB entity) in order to work properly. Using attribute commands allows you to perform attribute changes on entities that have been freshly instantiated by an ECB.
* You might want to perform attribute changes while you are in a parallel job, either for performance or convenience reasons. `AttributeChanger` cannot be used in parallel jobs, but there's nothing preventing the `AttributeCommandElement`s to be created in parallel. Therefore, in parallel jobs, you can schedule attribute changes using `AttributeCommandElement`s created with a parallel ECB.
* `AttributeCommand`s can allow you to create authoring tools for attribute modifiers in your game. The `ProcessAttributeChangerCommandsSystem` will execute the commands on all `DynamicBuffer<AttributeCommandElement>` it finds in the world, so if you create a `Baker` that adds `AttributeCommand`s to a buffer on an entity during baking, these commands will be automatically processed as soon as the `ProcessAttributeChangerCommandsSystem` updates. With this, you could define and setup "initial modifiers" on attributes in the inspector, at authoring time. 
    * Note that if you do this, you may want to add a `RemoveAttributeCommands` component to that baked entity as well. This will make it so that the `ProcessAttributeChangerCommandsSystem` will remove the `DynamicBuffer<AttributeCommand>` from the entity once it has processed the commands.

--------------------------------------

## Commands and NativeStream

Previously, we've seen how commands can be created with an ECB. However, while this is a *safe* way to create commands, it isn't the most efficient way.

The most efficient way to create and process them, especially from parallel jobs, would be:
* In a parallel job, write `AttributeCommand`s to a `NativeStream`
* In another single-threaded job that updates after this one, read `AttributeCommand`s from that `NativeStream` and call the `AttributeCommand.Process()` function on them (you will need to pass an `AttributeChanger` as parameter)

However, when using this approach, you have to be very careful not to use any ECB entities in your commands. These temporary entities will not be remapped once they've been created, since `NativeStream`s do not support entity remapping.