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

These packages can be installed into your Unity project through the package manager:
* In the Unity package manager window, click the `+` button in the top right.
* Choose `Add package from git URL`.
* Enter the URL(s) of the package(s) below and press `Add`. This should import the package(s). For example, enter `https://github.com/PhilSA/Trove.git?path=/com.trove.attributes` there to import the Attributes package.


### Targeting a specific version

Versions are released as branches on the repository. For example, the `v0.1.0` branch represent the 0.1.0 version of the package. In order to import a specific version of the packages, add the version branch (for example: `#v0.1.0`) at the end of the git url.

So for importing the 0.1.0 version of the Attributes package, you'd enter `https://github.com/PhilSA/Trove.git?path=/com.trove.attributes#v0.1.0` as the git url.


### Dependencies

Some of the packages require you to install the dependencies manually. When this is the case, the "Install URLs" section of the table below will let you know.


### User Content
Moreover, be sure to check the "Samples" tab of each package's page in the Package Manager. Several of them come with "User Content" that must be installed in your project before you can use the package.

----------------------------

## Package list

Package statuses:
* ✅: Slightly experimental
* ⚠️: Experimental
* ⛔: Too experimental


| **Name** | **Install URLs** | **Description** | **Status** |
| :--- | :--- | :--- | :--- |
| **Common** | `https://github.com/PhilSA/Trove.git?path=/com.trove.common` | Trove Common provides various tools and utilities that may be used by other packages or projects. <br> ([Documentation](./com.trove.common/Documentation~/index.md)) | ✅ |
| **Attributes** | `https://github.com/PhilSA/Trove.git?path=/com.trove.attributes` | Trove Attributes allows you to define identifiable "numbers" (attributes) on entities, and add/remove "rules" (modifiers) that affect the evaluation of these numbers' values. This tool can be used to effortlessly setup gameplay mechanics such as RPG character/equipment attributes, buffs, effects, roguelike-style stackable powerups and synergies, etc... <br> ([Documentation](./com.trove.attributes/Documentation~/index.md)) | ⚠️ |
| **Utility AI** | First, install: <br> `https://github.com/PhilSA/Trove.git?path=/com.trove.common` <br><br> Then, install: <br> `https://github.com/PhilSA/Trove.git?path=/com.trove.utilityai` | Trove Utility AI provides a flexible and efficient decision-making system for ECS. <br> ([Documentation](./com.trove.utilityai/Documentation~/index.md)) | ⚠️ |
| **Tweens** | First, install: <br> `https://github.com/PhilSA/Trove.git?path=/com.trove.common` <br><br> Then, install: <br> `https://github.com/PhilSA/Trove.git?path=/com.trove.tweens` | Trove Tweens is a simple tweening tool that allows you to create your own highly-efficient custom tweens. <br> ([Documentation](./com.trove.tweens/Documentation~/index.md)) | ⚠️ |
| **Polymorphic Structs** | `https://github.com/PhilSA/Trove.git?path=/com.trove.polymorphicstructs` | Trove Polymorphic Structs provides a codegen tool for polymorphic behaviour in burstable unmanaged code. They can be used to solve a variety of problems that aren't so obvious to solve in ECS, such as: ordered events where order can't be determined by type, state machines without structural changes or large jobs overhead, etc... <br> ([Documentation](./com.trove.polymorphicstructs/Documentation~/index.md)) | ⚠️ |
