
# Trove Common

Trove Common provides various tools and utilities that may be used by other packages or projects.

Most notably, it includes:
* `MathUtilities`: various math helpers.
* `CollectionUtilities`: utilities for collections.
* `VersionedPool`: a type of object pool where each element has a "Version" allowing us to determine if it corresponds to a saved handle. 
* `CompactMultiLinkedList`: tool for storing multiple independent growable lists in one list/buffer. The encompassing list/buffer will remain compact.
* `BitUtilities`: utilities for setting/getting bits in bytes or ints.
* `ByteArrayUtilities`: utilities for reading and writing various types to byte arrays.
* `PolymorphicObjectUtilities`: utilities for reading and writing `IPolymorphicObject`s to byte arrays.
* `EasingUtilities`: utilities for easing functions (tweening curves).
* `TransformUtilities`: utilities for getting/setting the world transform of entities (including child entities).
* `ParametricCurve`: a completely unmanaged curve represented by choice of several functions and parameters.
* `FileWriter` and `CodegenUtils`: helpers for codegen.