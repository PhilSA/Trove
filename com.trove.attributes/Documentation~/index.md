
# Trove Attributes

## Overview

Trove Attributes can be used to effortlessly setup gameplay mechanics such as RPG character/equipment attributes, buffs, effects, roguelike-style modifiers, etc... 

In practice, they are "numbers" that can depend on each-other (including across different entities), react to each-other's changes, and hold stacks of "modifers" that can change their values. As an example, a game could define "Intelligence" and "MagicDefense" attributes on player/NPC entitie, and have "MagicDefence" get a bonus that scales with "Intelligence". If a buff is then applied to "Intelligence" with an attribue modifier, "MagicDefence" will automatically be recalculated using the final buffed "Intelligence" value.

-----------------------------------------

## Table Of Contents

* [Example usage scenario](./examplescenario.md)
* [Usability and performance](./usability-performance.md)
* [How it works](./how-it-works.md)
* [Advanced topics](./advanced.md)
