# Changelog

## 0.2.0
* Removed `AttributeObserverCleanup`. Users must now remember to call a special functions on destroyed attribute-owning entities. See more details [here](https://github.com/PhilSA/Trove/blob/v0.2.0/com.trove.attributes/Documentation~/how-it-works-destruction.md)
* Changed the way Attribute commands are handled. Users are now expected to create a new commands entity every time they want to create commands, and that entity gets destroyed after commands are processed. See more details [here](https://github.com/PhilSA/Trove/blob/v0.2.0/com.trove.attributes/Documentation~/how-it-works-attribute-commands.md)