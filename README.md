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

The packages are hosted on [OpenUPM](https://openupm.com/). You can install them like this:
* Open `Edit/Project Settings/Package Manager`.
* Add a new Scoped Registry
    * Name: `package.openupm.com`
    * URL: `https://package.openupm.com`
    * Scope: `com.trove`
* Click **Save** or **Apply**.
* Open `Window/Package Manager`.
* Click `+` and select `Add package by name...`.
* Enter package name (and optionally version, if want a specific one), then click `Add`.

----------------------------

## Package list

| **Name** |  **Package** | **Description** | **Documentation** |
| :--- | :--- | :--- | :--- |
| **Common** | [com.trove.common](https://openupm.com/packages/com.trove.common/) | Trove Common provides various tools and utilities that may be used by other Trove packages, or by any project in general. | [Documentation](./com.trove.common/README.md) |
| **Event Systems** | [com.trove.eventsystems](https://openupm.com/packages/com.trove.eventsystems/) | Trove Event Systems provides various types of event systems to facilitate deferred logic. | [Documentation](./com.trove.eventsystems/README.md) |
| **Polymorphic Structs** | [com.trove.polymorphicstructs](https://openupm.com/packages/com.trove.polymorphicstructs/) | Trove Polymorphic Structs provides a codegen tool for polymorphic behaviour in burstable unmanaged code. They can be used to solve a variety of problems that are not so obvious to solve in ECS, such as: ordered events where order can't be determined by type, state machines without structural changes or large jobs overhead, etc... | [Documentation](./com.trove.polymorphicstructs/README.md) |
| **State Machines** | [com.trove.statemachines](https://openupm.com/packages/com.trove.statemachines/) | Trove State Machines provides templates for state machines in DOTS | [Documentation](./com.trove.statemachines/README.md) |
| **Stats** | [com.trove.stats](https://openupm.com/packages/com.trove.stats/) | Trove Stats allows you to define identifiable "numbers" (stats) on entities, and add/remove "rules" (modifiers) that affect the evaluation of these numbers' values. This tool can be used to effortlessly setup gameplay mechanics such as RPG character/equipment stats, buffs, effects, roguelike-style stackable powerups and synergies, etc... | [Documentation](./com.trove.stats/README.md) |
| **Utility AI** | [com.trove.utilityai](https://openupm.com/packages/com.trove.utilityai/) | Trove Utility AI provides a flexible and efficient decision-making system for ECS. | [Documentation](./com.trove.utilityai/README.md) |
| **Tweens** | [com.trove.tweens](https://openupm.com/packages/com.trove.tweens/) | Trove Tweens is a simple tweening tool that allows you to create your own highly-efficient custom tweens. | [Documentation](./com.trove.tweens/README.md) |
