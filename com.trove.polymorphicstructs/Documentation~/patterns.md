
# Usage Patterns

## Entity and Blob fields

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


#### Accompanying buffer

Another alternative is to store Entities/Blobs in a DynamicBuffer alongside wherever your polymorphic struct is stored. You could, for example, have a buffer of several Entities, and your polymorphic structs simply store an index to an entity in that buffer. The buffer is then passed as parameter to the polymorphic functions, so the entities can be gotten from there.