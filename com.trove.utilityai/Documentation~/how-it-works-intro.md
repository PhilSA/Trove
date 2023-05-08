
[How it works](./how-it-works.md)

# Introduction: restaurant example

An AI entity consists of these main building blocks:
* `Reasoner`: this is a component that represents a "decision-maker" on an entity. A reasoner has actions and considerations associated to it. An AI agent could have multiple reasoners for different types of decisions, but this will be covered in a later section.
* `Action`s: this is a dynamic buffer that represents the various actions that a reasoner can choose from. Each action has its score evaluated based on its considerations.
* `Consideration`s: this is a dynamic buffer that represents the various considerations that affect an action's score. A consideration is given an input, and outputs a score based on that input.


### Restaurand worker example

Let's imagine, for example, creating a very simple restaurant worker AI. 

We would define the following `Action`s and `Consideration`s for this restaurant worker's reasoner:
* Action: `Service` (take customer orders)
    * Consideration: `CustomerLineup` (the bigger the customer lineup, the higher the score of the `Service` action will be)
* Action: `Cook` (cook the meals that customers order)
    * Consideration: `PendingOrders` (the higher the amount of pending orders, the higher the score of the `Cook` action will be)
* Action: `Clean` (clean the kitchen)
    * Consideration: `KitchenDirtiness` (the dirtier the kitchen is, the higher the score of the `Clean` action will be)
    * Consideration: `CleaningSupplies` (if no cleaning supplies are available, the score of the `Clean` action should be 0, because we can't clean)

Updating the restaurant worker's reasoner would be done in these steps:
* First, give each consideration their inputs:
    * The `Service`->`CustomerLineup` consideration is given an input value between 0 and 1 depending on quantity of customers in line. 
    * The `Cook`->`PendingOrders` consideration is given an input value between 0 and 1 depending on quantity of pending orders. 
    * The `Clean`->`KitchenDirtiness` consideration is given an input value between 0 and 1 depending on kitchen dirtiness. 
    * The `Clean`->`CleaningSupplies` consideration is given an input value of either 0 or 1 depending on if there are available cleaning supplies.
* Then, update the reasoner:
    * For each action (`Service`, `Cook`, `Clean`), calculate final score based on all of their considerations.
    * Select an action based on a rule (highest-scoring action, weighted random, random within percentage of highest-scoring, etc...).
* Finally, once we know which action was picked, the user writes the code that executes that action:
    * If selected action is `Service`, execute the code to go take customer orders.
    * If selected action is `Cook`, execute the code to go cook some food.
    * If selected action is `Clean`, execute the code to go clean the kitchen.

>The reasoner's job is therefore to decide *what* to do; not *how* to do it. The *how* is the user's responsibility. An "action" is just an identifier of a thing to do.
