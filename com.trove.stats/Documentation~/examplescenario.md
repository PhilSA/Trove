

[Home](../README.md)

# Example usage scenario

The following example may help you better understand what this solution is capable of. Keep in mind the equations displayed here are not actual code.

Imagine you have a game with the following entities and attributes:

| **Entity Name** | **Attributes**|
| :--- | :--- |
| CharacterA | `Intelligence` -- `Strength` -- `Constitution` -- `CurrentHealth` -- `MaxHealth` -- `MagicDefense` |
| CharacterB | `Intelligence` -- `Strength` -- `Constitution` -- `CurrentHealth` -- `MaxHealth` -- `MagicDefense` |
| Shield | `PhysicalDamageReduction` -- `MagicDamageReduction` |

For the sake of this example, let's assume all of these attributes start with a base value of 10. An attribute's "base value" is its value before any modifiers have been applied.

Upon creation, the CharacterA applies a modifier to its `MagicDefense` attribute, making `MagicDefense` get a bonus that scales with `Intelligence` and `Constitution`: 

`CharacterA.MagicDefense adds ((0.5f * CharacterA.Intelligence) + (0.1f * CharacterA.Constitution))`

Upon adding this modifier, the CharacterA's `MagicDefense` value will now be 16 (`10 + (0.5 * 10) + (0.1 * 10)`).

Now if CharacterA levels up and decides to add 4 points to their `Intelligence`'s base value, their `MagicDefense` will automatically be updated to a value of 18 (`10 + (0.5 * 14) + (0.1 * 10)`), without requiring any manual update step in code.

Then, CharacterA picks up a Shield. Upon pickup, it adds the following modifiers to the Shield in order to make its defense attributes scale with the character's attributes:

`Shield.PhysicalDamageReduction adds (0.4f * CharacterA.Strength)`

`Shield.MagicDamageReduction adds (0.8f * CharacterA.MagicDefense)`

Upon adding these modifiers, the Shield's `PhysicalDamageReduction` will have a value of 14 (`10 + (0.4 * 10)`), and its `MagicDamageReduction` will have a value of 24.4 (`10 + (0.8 * 18)`).

CharacterA then casts a spell to buff its Shield's `MagicDamageReduction`, which adds these modifiers:

`Shield.MagicDamageReduction adds a flat bonus of (5f)`

`add (0.1f) to a multiplier applied to Shield.MagicDamageReduction after all adds are applied. Default multiplier is (1f), so the multiplier value would be 1.1f`

Upon adding these modifiers, the Shield's `MagicDamageReduction` value now takes into account multiple modifiers, for a final value of 32.34 (`(10 + (0.8 * 18) + 5) * 1.1`).

Then, CharacterA casts a spell targeting CharacterB, adding the following modifier to CharacterA's `Intelligence`:

`CharacterA.Intelligence adds ((1f - (CharacterB.CurrentHealth / CharacterB.MaxHealth)) * 10f)`

Upon adding this modifier, the CharacterA will gain an `Intelligence` bonus that's inversely proportional to the current health ratio of CharacterB (the lower CharacterB's health gets, the higher CharacterA's `Intelligence` gets). If CharacterB is at 100% health, CharacterA's `Intelligence` will have a value of 14 (no bonus). If CharacterB's health is at 10%, CharacterA's `Intelligence` will have a value of 23 (+9 bonus). But remember; CharacterA's `MagicDefense` scales with `Intelligence`, and CharacterA's Shield's `MagicDamageReduction` scales with `MagicDefense`. So when CharacterB takes the hit that brings its health to 10%, all of the following attributes are re-evaluated:
* `CharacterB.CurrentHealth` = 1
* `CharacterB.MaxHealth` = 10
* `CharacterA.Intelligence` = 23 (depends on `CharacterB.CurrentHealth` and `CharacterB.MaxHealth`)
* `CharacterA.MagicDefense` = 22.5 (depends on `CharacterA.Intelligence` and `CharacterA.Constitution`)
* `Shield.MagicDamageReduction` = 36.3 (depends on `CharacterA.MagicDefense`)

Since the modifier responsible for this spell observes CharacterB's health attributes directly, all of these values will automatically update whenever any change happens to CharacterB's health ratio.

After 30 seconds however, CharacterA's spell expires. This spell kept track of which modifier it added to CharacterA's `Intelligence`, and it now removes that modifier. The attribute values are now back to what they were before this spell was cast, and CharacterA's `Intelligence` no longer scales with CharacterB's health ratio.

---

Attribute modifiers are essentially "rules" that define how attributes are evaluated at all times, and a rule affecting an attribute can depend on other attributes. These networks of inter-dependent attributes across multiple different entities can get as big and as complex as you want, and the operations made by modifiers are implemented by you, so there is almost no limit to what you can do with them. When an attribute changes, its dependent attributes are recalculated automatically.
