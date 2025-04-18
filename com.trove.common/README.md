
# Trove Common

## Installation

Refer to [Trove Readme](https://github.com/PhilSA/Trove/blob/main/README.md#installing-the-packages) for installation instructions.

## Overview

Trove Common provides various tools and utilities that may be used by other packages or projects.

Most notably, it includes:
* `MathUtilities`: various math helpers.
* `CollectionUtilities`: utilities for collections.
* `Pool`: a type of object pool where each added element is guaranteed to hold an unchanging index, allowing us to refer to it by handle. 
* `SubList`: tool for storing multiple independent growable lists in one list/buffer.
* `BitUtilities`: utilities for setting/getting bits in bytes or ints.
* `ByteArrayUtilities`: utilities for reading and writing various types to byte arrays.
* `PolymorphicObjectUtilities`: utilities for reading and writing `IPolymorphicObject`s to byte arrays.
* `InstanceHandle`: Serves as a better-performing but baking-incompatible alternative to `UnityObjectRef`.
* `EasingUtilities`: utilities for easing functions (tweening curves).
* `TransformUtilities`: utilities for getting/setting the world transform of entities (including child entities).
* `ParametricCurve`: a completely unmanaged curve represented by choice of several functions and parameters.
* `FileWriter` and `CodegenUtils`: helpers for codegen.