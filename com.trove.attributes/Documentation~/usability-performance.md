
[Home](./index.md)

# Usability and performance characteristics

No attributes solution can be the best fit for every kind of game, because different games will have different requirements, and different requirements have different optimal solutions.

This solution has these main priorities:
* Make attributes read performance as good as it can be (or almost): the final modified attribute values are just `float`s in unmanaged components of your choice. They don't need to be gotten from buffers/arrays/maps/lookups/etc..., which means they can get read with great data access patterns. Most games will need to read attributes way more often than to change them, and this is what this solution focuses on (imagine an RTS game with 5000 units reading a "MoveSpeed" and "DetectionRange" attribute every frame, but both of these attributes need to support modifiers/buffs).
* Zero constant cost if no attributes are changing. Even if you have 10 million attributes in the world, there will be no constant cost to pay due to either change-filtering jobs or enabled components jobs iterating on them.
* No structural changes
* No managed objects anywhere, therefore it is fully job-compatible and causes no GC allocations.
* Simple and easy to use: you define attributes, you setup rules (modifiers) affecting these attributes, and all attributes are automatically recalculated when they need to be.
* Very high customizability: you have full control over defining modifier types, operations, and the entire modifiers stack.
* Attribute/modifier changes can happen instantaneously in jobs at any point in the frame.
* Advanced modifier capabilities (attributes depending on other attributes, modifier values depending on other attributes, complex dependency networks, etc...).
* Good fit for Netcode rollback & prediction.

In order to prioritize these, this solution makes some sacrifices when it comes to attribute/modifier change performance. This solution is therefore ideal for cases where there may be a large quantity of attributes in the world, and many of them must be read every frame, but high-volume attribute changes don't happen too often. However, attribute changes are not so costly that you can't have a certain portion of attributes changing every frame either. They have the cost of a few component/buffer lookups per changed attribute.
