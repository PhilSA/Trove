
[Advanced topics](./advanced.md)

# Combining different AI strategies

Trove Utility AI lends itself very well to being combined with other AI strategies, if needed. Because the user is fully in charge of manually updating each reasoner, they can easily do things like:
1. Have an AI state machine for an agent, and one of the states updates a Trove Utility AI reasoner to make additional decisions when updated.
1. Have a Trove Utility AI reasoner select an action, and some actions run an AI state machine when selected.
1. Same as point 1. and 2., but with behaviour trees instead of state machines.
1. Same as point 1. and 2., but with action planners instead of state machines.
1. etc...