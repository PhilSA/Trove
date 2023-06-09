
# Trove Attributes

## Overview

Trove Attributes can be used to effortlessly setup gameplay mechanics such as RPG character/equipment attributes, buffs, effects, roguelike-style modifiers, etc... 

In practice, they are "numbers" that can depend on each-other (including across different entities), react to each-other's changes, and hold stacks of "modifers" that can change their values. 

As an example, a game could define `Intelligence` and `MagicDefense` attributes for player/NPC entities in the game. Then, a modifier can be added to `MagicDefence` in order to give it a bonus that scales with the `Intelligence` attribute's value on the same entity. If a buff is then applied to `Intelligence` using another attribute modifier, `MagicDefence` will automatically be recalculated using the final buffed `Intelligence` value for its bonus.

-----------------------------------------

## Table Of Contents

* [Example usage scenario](./examplescenario.md)
* [Usability and performance](./usability-performance.md)
* [How it works](./how-it-works.md)
* [Advanced topics](./advanced.md)
