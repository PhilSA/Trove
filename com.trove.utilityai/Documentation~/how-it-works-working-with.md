
[How it works](./how-it-works.md)

# Working with reasoners, actions and considerations

## Updating a reasoner

### Setting consideration inputs

Before computing the scores of all of our actions and considerations, we must first give our considerations their inputs. We can do so like this:

```cs
// Give our "CustomerLineup" its input.
// With a customer lineup of 0, we'll give it an input of 0f.
// With a customer lineup of 2, we'll give it an input of 0.5f.
// With a customer lineup of 4 and beyond, we'll give it an input of 1f.
float normalizedCustomerLineupCount = math.clamp(customersInLine / 4f, 0f, 1f);
ReasonerUtilities.SetConsiderationInput(ref worker.CustomerLineupRef, normalizedCustomerLineupCount, in reasoner, considerationsBuffer, considerationInputsBuffer);
```

It's up to you to make sure your consideration inputs are up to date before you trigger a reasoner update

### Computing scores

After all our considerations have been given their input, it's time to compute scores like this:

```cs
// Create an action selector (this one will select whichever action scored highest)
ActionSelectors.HighestScoring actionSelector = new ActionSelectors.HighestScoring();

// Update scores, and select an action to take using the action selector
if (ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out Action selectedAction))
{
    // We've successfully selected the highest-scoring action ("selectedAction")
    // Based on the selected action, we would determine what to do here
    switch ((RestaurantWorkerAIAction)selectedAction.Type)
    {
        case RestaurantWorkerAIAction.Service:
            // Handle customer service (...)
            break;
        case RestaurantWorkerAIAction.Cook:
            // Handle cooking (...)
            break;
        case RestaurantWorkerAIAction.Clean:
            // Handle cleaning (...)
            break;
    }
}
```

Scores are calculated like this for each action:
* If the action is disabled or has no enabled considerations, its score is always `0f`
* If not, its score starts at `1f`
* Then, all enabled considerations multiply the action's score with their own score:
    * If consideration1 has a score of `0.2f`, the action's score becomes `1f * 0.2f = 0.2f`
    * If consideration2 has a score of `1f`, the action's score becomes `0.2f * 1f = 0.2f`
    * If consideration3 has a score of `0.5f`, the action's score becomes `0.2f * 0.5f = 0.1f`
    * etc...
* A "score compensation" is applied in case there is a difference in the amount of enabled considerations between different actions. You can read more about score compensation [Here](./how-it-works-score-compensation.md). 


### Action selectors

There are various different types of action selectors you can choose from:
* `HighestScoring`: selects the highest-scoring action
* `RandomWithinToleranceOfHighestScoring`: selects a random action whose score is within a certain % of the highest-scoring action
* `WeightedRandom`: selects an action based on a weighted random of their scores (higher scores are more likely to be picked)
* `None`: selects no action. This can be used when you just want to look at the scores of all actions and do something based on that (you're saving the performance cost of selecting an action)

You can also implement your own custom selectors, using the existing ones as inspiration. Simply create a new struct type that implements `IActionSelector`, construct it, and pass it to `ReasonerUtilities.UpdateScoresAndSelectAction` as parameter.

-------------------------------------------------

## Enabling and disabling actions and considerations

You can enable and disable actions and considerations like this:
```cs
ReasonerUtilities.SetActionEnabled(ref myActionRef, isEnabled, ref reasoner, actionsBuffer);

ReasonerUtilities.SetConsiderationEnabled(ref myConsiderationRef, isEnabled, ref reasoner, considerationsBuffer);
```

Enabling and disabling actions and considerations can be handy when you just want to temporarily prevent actions/considerations from being considered. It can also help with performances in certain situations where actions/considerations would be removed and added at a very high frequency (see [The cost of removing actions and considerations from an agent](./performance.md#the-cost-of-removing-actions-and-considerations-from-an-agent)).

-------------------------------------------------

## Setting new action and consideration data

You can set new data on existing actions and considerations like this:

```cs
ReasonerUtilities.SetActionData(ref myActionRef, new ActionDefinition((int)RestaurantWorkerAIAction.Cook), isEnabled, ref reasoner, actionsBuffer);

ReasonerUtilities.SetConsiderationData(ref myConsiderationRef, myConsiderationDef, isEnabled, input, ref reasoner, considerationsBuffer);
```

You can also set an action's score multiplier (applies a multiplier to the calculated score) like this:

```cs
ReasonerUtilities.SetActionScoreMultiplier(ref myActionRef, scoreMultiplier, in reasoner, actionsBuffer);
```

-------------------------------------------------

## Multiple actions of the same type

It is possible to add several actions of the same type to a reasoner. For example, a "target selection" reasoner would have one action per target, and all actions would be of type "Target". Each would have considerations such as distance, importance, etc....

In cases like these, we need a way to differentiate these actions when we run our reasoner update and a `out Action selectedAction` is outputted. Simply checking the `selectedAction.Type` will not help us, because all actions are of the same type. Instead, we can use the `selectedAction.Index`.

When adding an action to a reasoner, we have an opportunity to pass an `Index` value to this action, via the `ActionDefinition` constructor (look for `myActionIndex`):
```cs
ReasonerUtilities.AddAction(new ActionDefinition((int)TargetSelectorAction.Target, myActionIndex), true, ref reasoner, actionsBuffer, out restaurantWorker.ServiceRef);
```

If we have 5 targets to choose from, we'd add 5 actions of type `TargetSelectorAction.Target` to our reasoner, but each would have a different `Index` value. This way, you can store a dynamic buffer of data about each of your targets (like a buffer of target entities), and the action corresponding to each of these targets is given an `Index` based on the index of these target datas in your buffer. When an action is selected, you'll know that if the `selectedAction.Index` is 2 for example, then that means the selected target is the one at index 2 in your target datas buffer.

-------------------------------------------------

## Multiple reasoners

It is possible for one AI agent to require multiple reasoners. In these cases, you can have a main entity for your AI, and create additional entities for each reasoner this AI needs. The AI would therefore access reasoner components and buffers on those via component lookups.

For example, imagine we start with the restaurant scenario from the [Introduction](./how-it-works-intro.md) section. Now imagine that instead of "Cooking" being a simple straightforward action where the worker cooks whatever order came first, it would be an action where the worker needs to prioritize orders in order of which ones are fastest to prepare (or there might also be other considerations as well). In that case, one reasoner would first select an action between "Service", "Cook", "Clean", but then if "Cook" was selected, a second reasoner would be used to select which order we should be cooking first. In this second reasoner, each order would be a different action to choose from. Since reasoners are manually updated by the user, the user would simply be in charge of calling an update for the second reasoner only when the first reasoner chose the "Cook" action.

Note that in the example above, you may aslo choose to handle everything with a single reasoner (each order to choose from would just be another action in the main reasoner). The best approach depends on your design and your specific scenario, but splitting things up into multiple reasoners can help limiting the performance cost of AI updates sometimes.

Another use case for multiple reasoners for the same agent would be to have multiple "layers" of behaviour that can happen simultaneously. In a shooter game for example, you might have AI bots that should be able to move while shooting. In this case, you could choose to have one reasoner that makes "movement" decisions, and another reasoner that makes "combat" decisions. Both would be running at the same time. So the "movement" reasoner could choose to run towards a medkit on the floor, while the "combat" reasoner would choose which enemy to shoot at while we're running towards the medkit. The considerations of the "combat" reasoner could be influenced by the action chosen by the "movement" reasoner, or vice-versa.
