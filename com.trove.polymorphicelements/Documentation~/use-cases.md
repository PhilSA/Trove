
# Polymorphic Elements Use Cases

## When to use

Good memory access patterns are key in data-oriented programming. However, there are situations where the cost of arranging your data for great memory access patterns from frame to frame will largely outweigh the performance savings of that memory access pattern. It's in these situations that Polymorphic Structs will often be a better alternative. Examples of situations where this might happen:
* You rely on lots of structural changes to change the behaviour of entities. This gives you great data access patterns when you update these behaviours, but it is only made possible because you are frequently paying an enormous performance cost with structural changes. Overall, in your quest to reach perfect data access patterns at all costs during your behaviour updates, you've actually just made things worse by adding a very high cost outside of the behaviour updates.
* You rely on many enabled components to handle entity behaviour changes. Enabled components inflate the size of the archetype in the chunk, and adds the cost of constantly checking for enabled bits during entity iteration. This can add a lot of overhead to your jobs, and in several cases this performs much worse than handling the different behaviours with one job doing a simple switch statement. Relying on enabled components for behaviour changes will often also mean requiring many jobs to handle these behaviours, which brings us to the next point...
* You rely on scheduling tons of jobs because of all the different behaviours you need. Each job you schedule has an overhead, and there is always a point where this overhead will start to outweigh the benefits of tackling the problem with 1 job for each different type of behaviour. 
    * A situation like this will also often mean that you have tons of different archetypes in your game, because of one small behaviour difference between your entities. And when you have lots of different archetypes, you are also at risk of having poor chunk utilization because there will be plenty of chunks with very few entities in them. This means wasted memory and wasted performance.

Polymorphic Elements can therefore provide a better-performing alternative when facing any of these problems:
* Cost of structural changes
* Overhead of enabled components or change-filtering
* Overhead of many jobs
* Poor chunk utilization

Often, these problems are a consequence of one of these requirements:
* The need for ordering, where order cannot be determined by type.
* The need for conditional behaviour or frequent behaviour changes.
* The need to a wide variety of different possible behaviours.

The rest of this page describes several problems that Polymorphic Elements can solve very well.


## Events

#### Jobified events

#### EntityManager events

#### Managed events


## Delegates


## Coroutines and Tweens


## State machines


## Behaviour trees