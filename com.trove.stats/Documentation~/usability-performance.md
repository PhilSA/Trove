
[Home](../README.md)

# Usability and performance characteristics

No stats solution can be the best fit for every kind of game, because different games will have different requirements, and different requirements have different optimal solutions.

However, if a general-purpose solution is to exist, it has no other choice but to take a best guess as to what its priorities should be. This solution has these main priorities:
* Make stats read performance very good: the final modified stat values are stored in dynamic buffers on the stat-owning entity. 
    * Most games will need to read stats way more often than to change them, and this is what this solution focuses on (imagine an RTS game with 5000 units reading a "MoveSpeed" and "DetectionRange" stat every frame, but both of these stats need to support modifiers/buffs in case some spell is casted on some of them).
* All stats data lives on entities: this makes it simple for saving/loading, of for using stats in a netcode context (including predicition).
* Zero constant cost if no stats are changing. Even if you have 10 million stats in the world, there will be no constant cost to pay due to either change-filtering jobs or enabled components jobs iterating on them.
* No structural changes involved in any point in stats creation, value changes, modifier application, etc...
* No managed objects anywhere, therefore it is fully job-compatible and causes no GC allocations.
* Simple and easy to use: you define stats, you setup rules (modifiers) affecting these stats, and all stats are automatically recalculated when they need to be.
* Very high customizability: you have full control over defining modifier types, operations, and the entire modifiers stack. Modifiers are not limited to simple pre-determined arithmetic operations; they can be as complex as you want. For example, you could decide to create a modifier type that multiplies a stat's value by the sine function of the median value of three other stats, multiplied by a value that decays with the quantity of modifiers of this type that are currently affecting the stat.
* Stat/modifier changes can happen instantaneously in jobs at any point in the frame.
* Advanced modifier capabilities (stats depending on other stats, modifier values depending on other stats, complex dependency networks, etc...).