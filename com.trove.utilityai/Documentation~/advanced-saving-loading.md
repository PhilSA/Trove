
[Advanced topics](./advanced.md)

# Saving and loading reasoner state

The following represents all the data that must be serialized/deserialized in order to properly save/load the state of reasoners:
* Everything in the `Reasoner` component.
* Everything in the `Action` buffer.
* Everything in the `Consideration` buffer.
* All the `ActionReference`s and `ConsiderationReference`s stored in your own components.
* `ConsiderationDefinition`s, but only if you create/modify them at runtime. If you just use the approach of storing them in a blob asset during baking, there's no need to serialize them.