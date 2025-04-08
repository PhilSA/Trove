
# Netcode

The easiest way to use Polymorphic structs in Netcode is to use the "Merged Fields" polymorphic struct type, as described here: [Polymorphic struct types](./poly-struct-types.md). You can simply create a Ghost Variant of a Merged Fields struct, and synchronize the merged fields you'd like to synchronize.

However it is also possible to use "Union Struct" polymorphic structs in Netcode. For this, you'll need to determine which of the "child structs" has the largest size, and make the union field of that type in the polymorphic struct a ghost field. Set "Quantization" to 0 and "SmoothingAction" to "Clamp".