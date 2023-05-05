
# Trove Polymorphic Structs

Trove Polymorphic Structs provides a codegen tool for polymorphic behaviour in burstable unmanaged code. Based on several "child" structs in your project implementing the same interface, a "parent" union struct can be generated. The "parent" struct has all the methods of the interface, can be constructed from a "child" struct, and will automatically call the "child" struct implementation of that method.

These can play a key role in unlocking new performance & usability opportunities in some situations. They can allow you to avoid:
* Structural changes / sync points
* Component Lookups
* Scheduling a very large amount of different jobs
* Bad chunk utilization (due to each entity having one small archetype difference compared to others)

The drawback of polymorphic structs is that they have the added cost of a switch statement and casting between struct types when calling any of their polymorphic functions. They can also have a bigger size than what they should have, if there's a big discrepency in data size between the various "child" struct types. It's up to you to consider the pros and cons and decide if this is a good fit for your problem.

Common examples of where these can be useful:
* State machines without structural changes
* Ordered events
* Certain types of AI systems
* etc...

---------------------------------------------------

## Table Of Contents

* [Tutorial](./tutorial.md)

