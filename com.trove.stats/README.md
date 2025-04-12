
# Trove Stats

## Installation

Refer to [Trove Readme](https://github.com/PhilSA/Trove/blob/main/README.md#installing-the-packages) for installation instructions.


## Overview

Trove Stats can be used to effortlessly setup gameplay mechanics such as RPG character/equipment stats, buffs, effects, roguelike-style modifiers, etc... 

In practice, they are "numbers" that can depend on each-other (including across different entities), react to each-other's changes, and hold stacks of "modifers" that can change their values. 

As an example, a game could define `Intelligence` and `MagicDefense` stats for player/NPC entities in the game. Then, a modifier can be added to `MagicDefence` in order to give it a bonus that scales with the `Intelligence` stat's value on the same entity. If a buff is then applied to `Intelligence` using another stat modifier, `MagicDefence` will automatically be recalculated using the final buffed `Intelligence` value for its bonus.

-----------------------------------------

## Table Of Contents

* [Example usage scenario](./Documentation~/examplescenario.md)
* [Usability and performance](./Documentation~/usability-performance.md)
* [How it works](./Documentation~/how-it-works.md)
* [Advanced topics](./Documentation~/advanced.md)
