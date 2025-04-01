
[How it works](./how-it-works.md)

# Changing Attributes

You'll notice that you cannot write to `AttributeValues` fields; you can only read from them. This is because **all** changes to attribute values and **all** adding/removing of modifiers have to be done via an `AttributeChanger` struct. Making changes to attributes via an `AttributeChanger` ensures that all of the "reactive" logic of attributes will be triggered successfully.

`AttributeChanger` contains functions such as:
* `SetBaseValue`: sets the base value of an attribute
* `AddBaseValue`: adds to the base value of an attribute
* `AddModifier`: add (and apply) a modifier to an attribute
* `RemoveModifier`: remove (and unapply) a modifier from an attribute
* `RecalculateAttributeAndAllObservers`: Triggers a recalculation of the attribute, and all attributes that depend on it ("observers"). This can help you save on performance when you want to make several changes to an attribute without triggering auto-recalculation, and then only recalculate manually once at the end of all the changes.
* etc...

In order to use an `AttributeChanger`, your can set it up like this in a system and/or a job:
```cs
using AttributeChanger = Trove.Attributes.AttributeChanger<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;

[BurstCompile]
public partial struct MyAttributeChangerSystem : ISystem
{
    private AttributeChanger _attributeChanger;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _attributeChanger = new AttributeChanger(ref state);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _attributeChanger.UpdateData(ref state);

        // Here, you are ready to use the AttributeChanger (on the main thread, or in a single-threaded job)
        new MyAttributeChangerJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            AttributeChanger = _attributeChanger,
        }.Schedule();
    }
}

[BurstCompile]
public partial struct MyAttributeChangerJob : IJobEntity
{
    public float DeltaTime;
    public AttributeChanger AttributeChanger;

    void Execute(Entity entity, in StrengthChanger strengthChanger)
    {
        // This would change the base value of the 'Strength' attribute on this entity every frame
        AttributeChanger.AddBaseValue(new AttributeReference(entity, (int)AttributeType.Strength), strengthChanger.ChangeRate * DeltaTime);
    }
}
```

Important points to notice are:
* the `using AttributeChanger = (...)` statement at the top makes things more readable by removing the need to define all generic types when using `AttributeChanger`
* the call to `_attributeChanger.UpdateData` before using it in the system's `OnUpdate`
* Like many other `AttributeChanger` functions, `AddBaseValue` takes an `AttributeReference` as parameter. An `AttributeReference` is the combination of an `Entity` and an `AttributeType`, and it's essentially a way of identifying an specific attribute on a specific entity.
