
# Trove Polymorphic Structs

Trove Polymorphic Structs provides a codegen tool for polymorphic behaviour in burstable unmanaged code. Based on several "child" structs in your project implementing the same interface, a "parent" union struct can be generated. The "parent" struct has all the methods of the interface, can be constructed from a "child" struct, and will automatically call the "child" struct implementation of that method.

They are essentially a way to assign different possible behaviours to an entity without requiring a different entity archetype/query for each behaviour. Internally, the execution of polymorphic behaviour is handled with a simple switch statement over an enum representing the type of behaviour.

---------------------------------------------------

## Table Of Contents

* [When to use](./when-to-use.md)
* [Tutorial](./tutorial.md)

