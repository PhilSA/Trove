
# Usage Patterns

**Table of Contents**
* [Polymorphic structs as a way to minimize buffer lookups](#polymorphic-structs-as-a-way-to-minimize-buffer-lookups)
* [Polymorphic structs as a way to handle type-independent ordering](#polymorphic-structs-as-a-way-to-handle-type-independent-ordering)
* [Entity and Blob fields restriction workaround](#entity-and-blob-fields-restriction-workaround)


## Polymorphic structs as a way to minimize buffer lookups

Consider a use case where you need to access 3 dynamic buffers on an entity via `BufferLookup`s. In a case like this, Polymorphic structs can offer a way to reduce this to 1 lookup (if it makes sense for the use case).

For example, let's say an entity must support 3 different types of events (stored in dynamic buffers on that entity and processed by a system): `DynamicBuffer<EventA>`, `DynamicBuffer<EventB>`, `DynamicBuffer<EventC>`. If something needs to add all 3 types of events on that entity, it will need to do 3 different buffer lookups in order to get all the event buffers. With Polymorphic structs, `EventA`, `EventB`, `EventC` could be turned into one single polymorphic `Event` type, stored in only one dynamic buffer. With this change, only one buffer lookup would be required for adding any amount of different types of events.


## Polymorphic structs as a way to handle type-independent ordering

Imagine you need to implement an ordered events system, where events of many different types must be executed in the exact order they were added: `EventTypeA`, then `EventTypeB`, then `EventTypeA`, `EventTypeC`, then `EventTypeB`, etc.... Polymorphic structs can solve this problem easily and efficiently. Simply turn all your events into a polymorphic struct type, have a single buffer of those events, and add the polymorphic events in order.


## Entity and Blob fields restriction workaround

If not using the [Merged Fields](./poly-struct-types.md/#merged-fields-struct) type of polymorphic structs, Entities and blob fields are not allowed in polymorphic structs. This is due to how ECS has special treatment for certain types of fields, and the union struct approach is incompatible with this special treatment. There are, however, alternatives to storing Entities in polymorphic structs.

#### Encompassing struct

The simplest alternative is to simply create a regular struct that encompasses the generated polymorphic struct:
```cs
public struct MyEncompassingStruct // This is the regular "encompassing struct"
{
    public Entity EntityA;
    public BlobString BlobStringA;

    public PolyMyStruct PolyStruct; // this is our generated polymorphic struct
}
```

Then, our polymorphic struct has a polymorphic method that takes the Entities/Blobs as parameters:
```cs
public void DoSomething(Entity entityA, in BlobString blobStringA, int val)
{
    // TODO: polymorphic method that does something with entities and blobs
}
```

So from our encompassing struct, we can invoke our polymorphic method like this:
```cs
myEncompassingStruct.PolyStruct.DoSomething(myEncompassingStruct.EntityA, in myEncompassingStruct.BlobStringA, val);
```


#### External storage

Another alternative is to store Entities/Blobs in a DynamicBuffer alongside wherever your polymorphic struct is stored. You could, for example, have a buffer of several Entities, and your polymorphic structs simply store an index to an entity in that buffer. The buffer is then passed as parameter to the polymorphic functions, so the entities can be gotten from there.