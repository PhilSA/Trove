
# Why events in DOTS?

Events in DOTS are useful when you need some action to be performed, but you are not yet in the most optimal/appropriate context in which to perform that action. They can be a key tool for unlocking performance opportunities, for implementing modular decoupled systems, or for simplifying defered actions.


## Events as a way to perform an action at the correct time in the frame
Imagine a projectile system that applies damage to targets, and updates in the `FixedStepSimulationSystemGroup` because it relies on physics. In this game, damage handling must rely on buffable stat values that are updated by a system that updates at a certain point later in the frame, in the regular `SimulationSystemGroup`. With events, we can have our damage system create damage "events" in the fixed step group, but these damage events would only be processed later in the frame, after the stats update system.


## Events as a way to enable bursted code and prevent sync points
Imagine you have some ECS system that needs to update UI values. Since UI elements are managed, this cannot happen in bursted code. A naive approach would be to disable burst for this ECS system's update, and make it create a main thread sync point, so that it can update UI elements. However with an events system, this system could write UI update events in a burst job, and these events would be processed at a later point in a non-burst main thread context, where we already had an existing sync point. Events allow us to avoid a sync point and to preserve bursted code here.


## Events as a way to enable multithreading
Imagine a projectile system that applies damage to hit targets. Since multiple projectiles could hit the same entity at the same time, the apllication of damage must be single-threaded. Therefore, the job that handles projectile hit detection with raycasts and applying damage would, at first, have to be single-threaded. However, an event system allows us to parallelize this projectiles job. Projectiles would handle hit detection in parallel, and would write "damage events" in parallel. Later, a single-threaded job would process these damage events and apply damage. With this approach, we were able to parallelize the part of the work that could be parallelized, and only handle the part of the would that couldn't be parallelized on a single thread.


## Events as a way to reduce component lookups
Imagine a system that applies damage to targets. In this game, damage-handling is complex and relies on the data of many components and buffers on the affected entity: `Health`, `Team`, `Defense`, `Equipment`, `DynamicBuffer<Buff>`, etc... A system that wishes to apply damage would therefore need 5 component/buffer lookups on the affected entity in order to apply that damage. With an events system however, we could first create a "damage event" that is stored in a `DynamicBuffer<DamageEvent>` on the affected entity, and then have a separate system that iterates entities with all the damage-related components, and apply damage there. Since we now handle damage in entity iteration, we have very fast access to all the other components and buffers required for damage handling. Our damage-creating system now only has to pay the price of one component lookup (for writing the damage event to the damage events buffer) instead of 5 lookups.


## Events as a way to do archetype-dependent handling of some action
Imagine a system that applies damage to targets. In this game, damage is handled differently depending on the archetype of the damaged entity. A naive approach would be to make the damaging job look for the presence of various components on the affected entity via lookups, and do different things based on the results. However with an events system, we can first create a "damage event" that is stored in a `DynamicBuffer<DamageEvent>` on the affected entity, and then have several damage-handling jobs (one for each damageable archetype) that process this damage in different ways depending on the archetype. For example, a "unit" archetype would process damage depending on its team, resistances, buffs, etc.... while a "destructible" archetype would simply trigger some destroy logic when its health reaches 0.
