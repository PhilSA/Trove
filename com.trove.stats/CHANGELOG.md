# Changelog

## 0.3.0
* Changed *everything*

## 0.2.0
* Removed `AttributeObserverCleanup`. Users must now remember to call a special functions on destroyed attribute-owning entities. 
* Changed the way Attribute commands are handled. Users are now expected to create a new commands entity every time they want to create commands, and that entity gets destroyed after commands are processed. 