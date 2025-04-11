![](./trove_header.png)

Trove is a collection of packages for Unity DOTS.

All of these packages care about:
* Performance
* Customizability
* Netcode compatibility
* Keeping things as simple and not over-engineered as possible 
* Having as little impact on your architecture as possible
* Making you do things manually so you are fully aware of everything that happens

----------------------------

## Installing the packages

The packages are hosted on [OpenUPM](https://openupm.com/). The OpenUPM page of each Trove package is linked below. Follow installation instructions on OpenUPM.

----------------------------

## Package list

Package statuses:
* ✅: Slightly experimental
* ⚠️: Experimental
* ⛔: Too experimental

| **Name** | **Description** | **Documentation** | **Status** |
| :--- | :--- | :--- | :--- |
| [Common](https://openupm.com/packages/com.trove.common/) | Trove Common provides various tools and utilities that may be used by other packages or projects. | [Documentation](./com.trove.common/README.md) | ⚠️ |
| [Event Systems](https://openupm.com/packages/com.trove.eventsystems/) | Trove Event Systems provides various types of event systems to facilitate deferred logic. | [Documentation](./com.trove.eventsystems/README.md) | ⚠️ |
| [Polymorphic Structs](https://openupm.com/packages/com.trove.polymorphicstructs/) | Trove Polymorphic Structs provides a codegen tool for polymorphic behaviour in burstable unmanaged code. They can be used to solve a variety of problems that are not so obvious to solve in ECS, such as: ordered events where order can't be determined by type, state machines without structural changes or large jobs overhead, etc... | [Documentation](./com.trove.polymorphicstructs/README.md) | ⚠️ |
| [State Machines](https://openupm.com/packages/com.trove.statemachines/) | Trove State Machines provides templates for state machines in DOTS | [Documentation](./com.trove.statemachines/README.md) | ⚠️ |
| [Stats](https://openupm.com/packages/com.trove.stats/) | Trove Stats allows you to define identifiable "numbers" (stats) on entities, and add/remove "rules" (modifiers) that affect the evaluation of these numbers' values. This tool can be used to effortlessly setup gameplay mechanics such as RPG character/equipment stats, buffs, effects, roguelike-style stackable powerups and synergies, etc... | [Documentation](./com.trove.stats/README.md) | ⚠️ |
| [Utility AI](https://openupm.com/packages/com.trove.utilityai/) | Trove Utility AI provides a flexible and efficient decision-making system for ECS. | [Documentation](./com.trove.utilityai/README.md) | ⚠️ |
| [Tweens](https://openupm.com/packages/com.trove.tweens/) | Trove Tweens is a simple tweening tool that allows you to create your own highly-efficient custom tweens. | [Documentation](./com.trove.tweens/README.md) | ⚠️ |
