
# Why events in DOTS?

Events in DOTS are useful when you need some action to be performed, but you are not yet in the most optimal/appropriate context in which to perform that action. They can be a key tool for unlocking performance opportunities, for implementing modular decoupled systems, or for simplifying defered actions.

For example:
* You are in a parallel bursted job, and you need to perform an action that could only be executed on the main thread in unbursted managed code.
* You are in monobehaviour code, and you need to remember to perform an action that should only be executed at a specific point later in the frame in a bursted job, after some other system has updated.
* You are iterating a certain entity query, and you need to schedule something to be executed on another entity, but only later when you are iterating that other entity archetype. Once we iterate that other entity's archetype, we can do archetype-dependent handling of the action, and we can gain very fast access to multiple component data on the entity.
* etc....

Moreover, they can also be a convenient way to communicate between systems and between entities.

