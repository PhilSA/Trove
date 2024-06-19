
[Home](../README.md)


# Performance

Trove Utility AI uses an implementation strategy where all actions and all considerations of a reasoner live in dynamic buffers on the same entity. This strategy was chosen for several reasons:
* No component lookups are needed at any point in the AI update, from setting inputs on considerations, to calculating action scores, to selecting actions. Actions/considerations are accessed directly with their index in buffers (with some [exceptions](./performance.md#the-cost-of-addingremoving-actionsconsiderations-from-an-reasoner)).
* No structural changes are involved when adding/removing actions and considerations. This can be very desirable when, for example, implementing a "target selection" AI for units in an RTS game, which might end up adding/removing 100s or 1000s of actions/considerations per second.
* No "one job per type of action/consideration" required, which means our job scheduling and update costs don't grow with the quantity of different action/consideration types in our game.
* No sync points involved when working with the AI, which makes your code simpler.
* Reasoners can have all their actions and considerations pre-baked, because these live in dynamic buffers. This reduces the cost of instantiating AI agents. This wouldn't be the case if this solution relied on native collections.

While a single reasoner cannot be updated by multiple threads, each reasoner can update on different threads. Although, it would be possible to split one AI's decision-making process into multiple reasoners.

----------------------------------------------------------

## Spreading reasoner updates over multiple frames

It is typical for reasoners to not update every frame, even in games that have relatively few reasoner.

This solution provides you with the freedom to do this, because it puts you in charge of manually calling `ReasonerUtilities.UpdateScoresAndSelectAction` whenever you need to update an AI's decision-making process. It doesn't update reasoners in a built-in system by default.

Therefore, you are free (and encouraged) to come up with a strategy to evenly spread out your AI updates across frames.

----------------------------------------------------------

## The cost of adding/removing actions/considerations from an reasoner

Trove Utility AI uses a caching and versioning system for action/consideration indexes in buffers. A `ConsiderationReference`, for example, will store the entity, index, ID and version of a given consideration. Getting a condideration via a `ConsiderationReference` therefore means getting the element in the considerations buffer at the cached index and on the cached entity.

When a consideration is added or removed, the `Reasoner` will bump its version number, which will let existing `ConsiderationReference`s know that the next time they want to find their associated consideration, they must find it by searching for an ID in the buffer instead. Once they have found it again, however, ther cache the new index and version so that they won't have to "find" it by ID next time. 

Actions use the same sort of mechanism, with `ActionReference`s. Also keep in mind that removing an action removes all of its associated considerations as well, which invalidates cached data for considerations.

What this means is that accessing an action or consideration on an entity is very fast when actions/considerations aren't changing, but will be about slower on the first time a "reference" is resolved after actions/considerations have been added/removed. For example, on a reasoner with 10 actions of 5 considerations each (for 50 considerations total), setting consideration inputs and updating the action scores will be roughly 15% heavier when performed after a consideration was added/removed. The next time the update is performed, however, performance will be back to normal.

>**Takeaway**: If for any reason you are adding or removing actions or considerations very frequently, then your AI updates will cost more than they should (because action/consideration cached indexes will constantly get invalidated, and actions/considerations will have to be found by ID in the buffers instead of accessed by index directly). If you are in this situation, consider "recycling" your actions/considerations by disabling/enabling them instead of removing/adding them. 

```cs
// Disable actions and considerations (disabling them prevents invalidating cached indexes)
ReasonerUtilities.SetActionEnabled(ref myActionRef, false /*isEnabled*/, ref reasoner, actionsBuffer);
ReasonerUtilities.SetConsiderationEnabled(ref myConsiderationRef, false /*isEnabled*/, ref reasoner, considerationsBuffer);

// Set new data on actions and considerations, and re-enable them (this is a way to "recycle" disabled actions/considerations)
// However, you cannot assign a consideration to a different action when setting its data and re-enabling it (this would change consideration indexes)
ReasonerUtilities.SetActionData(ref myActionRef, new ActionDefinition((int)ActionType.A), true /*isEnabled*/, ref reasoner, actionsBuffer);
ReasonerUtilities.SetConsiderationData(ref myConsiderationRef, myConsiderationDef, true /*isEnabled*/, 0f, ref reasoner, considerationsBuffer);
```

Keep in mind, however, that disabling actions/considerations does not reduce the cost of the AI update by a large factor. Most of the update cost of the AI is buffer access, and we still need to access each action/consideration in order to check if they are diabled.