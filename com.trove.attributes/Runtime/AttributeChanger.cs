using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Trove.Attributes
{
    [BurstCompile]
    public struct AttributeChanger<TAttributeModifier, TModifierStack, TAttributeGetterSetter> 
        where TAttributeModifier : unmanaged, IBufferElementData, IAttributeModifier<TModifierStack, TAttributeGetterSetter> 
        where TModifierStack : unmanaged, IAttributeModifierStack
        where TAttributeGetterSetter : unmanaged, IAttributeGetterSetter
    {
        public TAttributeGetterSetter AttributeGetterSetter;
        public ComponentLookup<AttributesOwner> AttributesOwnerLookup;
        public BufferLookup<AttributeObserver> AttributeObserverLookup;
        public BufferLookup<AttributeObserverCleanup> AttributeObserverCleanupLookup;
        public BufferLookup<TAttributeModifier> AttributeModifierLookup;
        public FixedList512Bytes<AttributeReference> AttributesOnbservedByModifier; // Allow a modifier to observe up to 42 attributes

        public AttributeChanger(ref SystemState state)
        {
            AttributeGetterSetter = default;
            AttributeGetterSetter.OnSystemCreate(ref state);
            AttributesOwnerLookup = state.GetComponentLookup<AttributesOwner>(false);
            AttributeObserverLookup = state.GetBufferLookup<AttributeObserver>(false);
            AttributeObserverCleanupLookup = state.GetBufferLookup<AttributeObserverCleanup>(false);
            AttributeModifierLookup = state.GetBufferLookup<TAttributeModifier>(false);
            AttributesOnbservedByModifier = new FixedList512Bytes<AttributeReference>();
        }

        [BurstCompile]
        public void UpdateData(ref SystemState state)
        {
            AttributeGetterSetter.OnSystemUpdate(ref state);
            AttributesOwnerLookup.Update(ref state);
            AttributeObserverLookup.Update(ref state);
            AttributeObserverCleanupLookup.Update(ref state);
            AttributeModifierLookup.Update(ref state);
        }

        [BurstCompile]
        public bool GetAttributeValues(AttributeReference attribute, out AttributeValues value)
        {
            return AttributeGetterSetter.GetAttributeValues(attribute, out value);
        }

        [BurstCompile]
        public bool SetBaseValue(AttributeReference attribute, float newBaseValue, bool autoRecalculate = true)
        {
            AttributeValues attributeValue = new AttributeValues(newBaseValue);
            if (AttributeGetterSetter.SetAttributeValues(attribute, attributeValue))
            {
                if (autoRecalculate &&
                    AttributeModifierLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                    AttributeObserverLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<AttributeObserver> attributeObservers))
                {
                    RecalculateAttributeAndAllObservers(attribute, attributeValue, ref attributeModifiers, ref attributeObservers);
                }
                else
                {
                    AttributeGetterSetter.SetAttributeValues(attribute, attributeValue);
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public bool AddBaseValue(AttributeReference attribute, float addValue, bool autoRecalculate = true)
        {
            if (AttributeGetterSetter.GetAttributeValues(attribute, out AttributeValues attributeValue))
            {
                attributeValue.__internal__baseValue += addValue;

                if (autoRecalculate &&
                    AttributeModifierLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                    AttributeObserverLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<AttributeObserver> attributeObservers))
                {
                    RecalculateAttributeAndAllObservers(attribute, attributeValue, ref attributeModifiers, ref attributeObservers);
                }
                else
                {
                    AttributeGetterSetter.SetAttributeValues(attribute, attributeValue);
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public bool RecalculateAttributeAndAllObservers(AttributeReference attribute)
        {
            if(AttributeGetterSetter.GetAttributeValues(attribute, out AttributeValues attributeValue) &&
                AttributeModifierLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                AttributeObserverLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                return RecalculateAttributeAndAllObservers(attribute, attributeValue, ref attributeModifiers, ref attributeObservers);
            }

            return false;
        }

        [BurstCompile]
        public bool RecalculateAttributeAndAllObservers(
            AttributeReference attribute,
            AttributeValues attributeValue,
            ref DynamicBuffer<TAttributeModifier> attributeModifiers,
            ref DynamicBuffer<AttributeObserver> attributeObservers)
        {
            // Recalculate this attribute
            TModifierStack modifierStack = default(TModifierStack);
            modifierStack.Initialize();

            // Apply all modifiers that affect this attribute type
            for (int i = attributeModifiers.Length - 1; i >= 0; i--)
            {
                TAttributeModifier iteratedModifier = attributeModifiers[i];
                if (iteratedModifier.AffectedAttributeType == attribute.AttributeType)
                {
                    iteratedModifier.ApplyModifier(ref modifierStack, in AttributeGetterSetter, out bool shouldRemoveModifier);

                    // Modifiers are typically removed when they couldn't resolve all the other attributes they depend on.
                    // In those cases, we remove them since the outcome wouldn't make sense.
                    if (shouldRemoveModifier)
                    {
                        attributeModifiers.RemoveAtSwapBack(i);
                    }
                }
            }

            attributeValue.__internal__value = modifierStack.CalculateFinalValue(attributeValue.__internal__baseValue);
            if (AttributeGetterSetter.SetAttributeValues(attribute, attributeValue))
            {
                RecalculateAllObservers(attribute, ref attributeObservers);
            }
            else
            {
                return false;
            }

            return true;
        }

        [BurstCompile]
        public bool RecalculateAllObservers(AttributeReference attribute)
        {
            if (AttributeObserverLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                RecalculateAllObservers(attribute, ref attributeObservers);
                return true;
            }

            return false;
        }

        [BurstCompile]
        public void RecalculateAllObservers(
            AttributeReference attribute,
            ref DynamicBuffer<AttributeObserver> attributeObservers)
        {
            // Recalculate observers that observe this attribute type on this entity
            for (int i = attributeObservers.Length - 1; i >= 0; i--)
            {
                AttributeObserver iteratedObserver = attributeObservers[i];
                if (iteratedObserver.ObservedAttributeType == attribute.AttributeType)
                {
                    if (AttributeGetterSetter.GetAttributeValues(iteratedObserver.ObserverAttribute, out AttributeValues iteratedObserverAttributeValue) &&
                        AttributeModifierLookup.TryGetBuffer(iteratedObserver.ObserverAttribute.Entity, out DynamicBuffer<TAttributeModifier> attributeModifiersOnOnserver) &&
                        AttributeObserverLookup.TryGetBuffer(iteratedObserver.ObserverAttribute.Entity, out DynamicBuffer<AttributeObserver> attributeObserversOnOnserver))
                    {
                        RecalculateAttributeAndAllObservers(iteratedObserver.ObserverAttribute, iteratedObserverAttributeValue, ref attributeModifiersOnOnserver, ref attributeObserversOnOnserver);
                    }
                    else
                    {
                        // If unsuccessful, it's because we couldn't find that attribute anymore. 
                        // (either because the entity was destroyed or the component was removed)
                        // Therefore, remove this observer so that we won't pay the pointless cost of iterating it again in the future.
                        attributeObservers.RemoveAtSwapBack(i);
                    }
                }
            }
        }

        // TODO: optimization idea: when adding modifiers, insert them in order of attribute type, so all mods of same type are contiguous
        //      When removing modifier, don't sawp back.
        //      This way, when we iterate buffer to find mods for a given attribute, we can early out
        [BurstCompile]
        public bool AddModifier(AttributeReference attribute, TAttributeModifier modifier, out ModifierReference modifierReference, bool autoRecalculate = true)
        {
            if (AttributeGetterSetter.GetAttributeValues(attribute, out AttributeValues attributeValue) &&
                AttributeModifierLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                AttributeObserverLookup.TryGetBuffer(attribute.Entity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                return AddModifier(attribute, modifier, attributeValue, ref attributeModifiers, ref attributeObservers, out modifierReference, autoRecalculate);
            }

            modifierReference = default;
            return false;
        }

        [BurstCompile]
        public bool AddModifier(
            AttributeReference attribute, 
            TAttributeModifier modifier, 
            AttributeValues attributeValues,
            ref DynamicBuffer<TAttributeModifier> attributeModifiers,
            ref DynamicBuffer<AttributeObserver> attributeObservers,
            out ModifierReference modifierReference, 
            bool autoRecalculate = true)
        {
            if (AttributeUtilities<TAttributeModifier, TModifierStack, TAttributeGetterSetter>.GetNewModifierID(attribute.Entity, ref AttributesOwnerLookup, out uint newModifierID))
            {
                modifier.ModifierID = newModifierID;

                if (AddAttributeAsObserverOfAllAttributesObservedByModifier(attribute, modifier))
                {
                    modifier.AffectedAttributeType = attribute.AttributeType;
                    attributeModifiers.Add(modifier);

                    modifierReference = new ModifierReference
                    {
                        AffectedAttribute = attribute,
                        ID = newModifierID,
                    };

                    if (autoRecalculate)
                    {
                        RecalculateAttributeAndAllObservers(attribute, attributeValues, ref attributeModifiers, ref attributeObservers);
                    }

                    return true;
                }
                else
                {
                    // If adding observer failed, the modifier won't work. Cancel adding modifier.
                    modifierReference = default;
                    return false;
                }
            }

            modifierReference = default;
            return false;
        }

        [BurstCompile]
        public bool RemoveModifier(ModifierReference modifierReference, bool autoRecalculate = true)
        {
            if (AttributeModifierLookup.TryGetBuffer(modifierReference.AffectedAttribute.Entity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                AttributeObserverLookup.TryGetBuffer(modifierReference.AffectedAttribute.Entity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                for (int i = attributeModifiers.Length - 1; i >= 0; i--)
                {
                    TAttributeModifier iteratedModifier = attributeModifiers[i];
                    if (iteratedModifier.ModifierID == modifierReference.ID)
                    {
                        return RemoveModifier(modifierReference.AffectedAttribute.Entity, i, ref attributeModifiers, ref attributeObservers, autoRecalculate);
                    }
                }
            }

            return false;
        }

        [BurstCompile]
        public bool RemoveModifier(
            Entity onEntity, 
            int modifierIndexInBuffer, 
            ref DynamicBuffer<TAttributeModifier> attributeModifiers, 
            ref DynamicBuffer<AttributeObserver> attributeObservers,
            bool autoRecalculate = true)
        {
            if (modifierIndexInBuffer < attributeModifiers.Length)
            {
                TAttributeModifier iteratedModifier = attributeModifiers[modifierIndexInBuffer];
                AttributeReference affectedAttribute = new AttributeReference(onEntity, iteratedModifier.AffectedAttributeType);

                attributeModifiers.RemoveAtSwapBack(modifierIndexInBuffer);

                /*  
                 *  Remove self from observers of all attributes observed by the modifier.
                 *  A modifier can observe several other attributes, which means the entities of those observed attributes
                 *  will have this modifier's affectedAttribute as an observer (so it can change when the observed
                 *  attribute changes). Since we're removing the modifier, we're removing the affectedAttribute as
                 *  an observer in order to not receive change notifications anymore.
                 *  
                 *  Example:
                 *  A observes B on same entity.
                 *  ...so the entity has a modifier affecting A + observing B
                 *  ...and the entity has an observer where B is observed and A is the observer.
                 *  ...if we remove the modifier affecting A, we need to get rid of the observer saying that A observes B
                 *  ...and since A has lost a modifier, we'd have to tigger an autoRecalculation of A
                 *  ...which means we'll iterate the observes buffer for observers observing A, and recalculate those.
                */
                RemoveAttributeAsObserverOfAllAttributesObservedByModifier(affectedAttribute, iteratedModifier);

                if (autoRecalculate)
                {
                    if (AttributeGetterSetter.GetAttributeValues(affectedAttribute, out AttributeValues attributeValue))
                    {
                        RecalculateAttributeAndAllObservers(affectedAttribute, attributeValue, ref attributeModifiers, ref attributeObservers);
                    }
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public bool RemoveAllModifiers(Entity onEntity, bool autoRecalculate = true)
        {
            if (AttributeModifierLookup.TryGetBuffer(onEntity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                AttributeObserverLookup.TryGetBuffer(onEntity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                for (int i = attributeModifiers.Length - 1; i >= 0; i--)
                {
                    RemoveModifier(onEntity, i, ref attributeModifiers, ref attributeObservers, autoRecalculate);
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public bool RemoveAllModifiersAffectingAttribute(AttributeReference affectedAttribute, bool autoRecalculate = true)
        {
            if (AttributeModifierLookup.TryGetBuffer(affectedAttribute.Entity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                AttributeObserverLookup.TryGetBuffer(affectedAttribute.Entity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                for (int i = attributeModifiers.Length - 1; i >= 0; i--)
                {
                    TAttributeModifier iteratedModifier = attributeModifiers[i];
                    if (iteratedModifier.AffectedAttributeType == affectedAttribute.AttributeType)
                    {
                        RemoveModifier(affectedAttribute.Entity, i, ref attributeModifiers, ref attributeObservers, autoRecalculate);
                    }
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public bool RemoveAllModifiersObservingEntityOnEntity(Entity observedEntity, Entity onEntity, bool autoRecalculate = true)
        {
            if (AttributeModifierLookup.TryGetBuffer(onEntity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                AttributeObserverLookup.TryGetBuffer(onEntity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                for (int i = attributeModifiers.Length - 1; i >= 0; i--)
                {
                    if(i >= attributeModifiers.Length)
                    {
                        i--;
                        continue;
                    }

                    TAttributeModifier iteratedModifier = attributeModifiers[i];

                    AttributesOnbservedByModifier.Clear();
                    iteratedModifier.AddObservedAttributesToList(ref AttributesOnbservedByModifier);

                    // If we find at least one observed attribute on the target entity, remove modifier
                    for (int a = 0; a < AttributesOnbservedByModifier.Length; a++)
                    {
                        AttributeReference attributeObservedByModifier = AttributesOnbservedByModifier[a];
                        if (attributeObservedByModifier.Entity == observedEntity)
                        {
                            RemoveModifier(onEntity, i, ref attributeModifiers, ref attributeObservers, autoRecalculate);
                            break;
                        }
                    }
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public bool RemoveAllModifiersObservingAttribute(AttributeReference observedAttribute, bool autoRecalculate = true)
        {
            // for each observer observing the observedAttribute, remove all modifiers observing that attribute on the entity of that observer
            if (AttributeObserverLookup.TryGetBuffer(observedAttribute.Entity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                // Check all observers observing this attribute...
                for (int o = attributeObservers.Length - 1; o >= 0; o--)
                {
                    AttributeObserver iteratedObserver = attributeObservers[o];
                    if (iteratedObserver.ObservedAttributeType == observedAttribute.AttributeType)
                    {
                        if (AttributeModifierLookup.TryGetBuffer(iteratedObserver.ObserverAttribute.Entity, out DynamicBuffer<TAttributeModifier> attributeModifiersOnObserver) &&
                            AttributeObserverLookup.TryGetBuffer(iteratedObserver.ObserverAttribute.Entity, out DynamicBuffer<AttributeObserver> attributeObserversOnObserver))
                        {
                            RemoveAllModifiersObservingAttributeOnEntity(iteratedObserver.ObserverAttribute.Entity, observedAttribute, ref attributeModifiersOnObserver, ref attributeObserversOnObserver, autoRecalculate);
                        }
                    }
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public bool RemoveAllModifiersObservingAttributeOnEntity(Entity onEntity, AttributeReference observedAttribute, bool autoRecalculate = true)
        {
            if (AttributeModifierLookup.TryGetBuffer(onEntity, out DynamicBuffer<TAttributeModifier> attributeModifiers) &&
                AttributeObserverLookup.TryGetBuffer(onEntity, out DynamicBuffer<AttributeObserver> attributeObservers))
            {
                RemoveAllModifiersObservingAttributeOnEntity(onEntity, observedAttribute, ref attributeModifiers, ref attributeObservers, autoRecalculate);
                return true;
            }

            return false;
        }

        [BurstCompile]
        private void RemoveAllModifiersObservingAttributeOnEntity(
            Entity onEntity,
            AttributeReference observedAttribute,
            ref DynamicBuffer<TAttributeModifier> attributeModifiers,
            ref DynamicBuffer<AttributeObserver> attributeObservers,
            bool autoRecalculate = true)
        {
            for (int i = attributeModifiers.Length - 1; i >= 0; i--)
            {
                TAttributeModifier iteratedModifier = attributeModifiers[i];

                AttributesOnbservedByModifier.Clear();
                iteratedModifier.AddObservedAttributesToList(ref AttributesOnbservedByModifier);

                for (int a = 0; a < AttributesOnbservedByModifier.Length; a++)
                {
                    AttributeReference attributeObservedByModifier = AttributesOnbservedByModifier[a];
                    if (attributeObservedByModifier.IsSame(observedAttribute))
                    {
                        RemoveModifier(onEntity, i, ref attributeModifiers, ref attributeObservers, autoRecalculate);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the specified attribute as an observer of the attributes that the modifier observes
        /// </summary>
        [BurstCompile]
        private bool AddAttributeAsObserverOfAllAttributesObservedByModifier(AttributeReference attribute, TAttributeModifier modifier)
        {
            AttributesOnbservedByModifier.Clear();
            modifier.AddObservedAttributesToList(ref AttributesOnbservedByModifier);

            // For each attribute observed by the modifier...
            for (int i = 0; i < AttributesOnbservedByModifier.Length; i++)
            {
                AttributeReference attributeObservedByModifier = AttributesOnbservedByModifier[i];
                if (attribute.IsSame(attributeObservedByModifier) ||
                    IsAttributeAAnObserverOfAttributeBOrAnyObserverOfAttributeB(attributeObservedByModifier, attribute))
                {
                    // We found that the attribute that we'd want to observe is either ourselves or is already an observer of us.
                    // We cancel in order to avoid creating an infinite observers loop.
                    Debug.LogWarning("WARNING: detected an infinite attribute observers loop and prevented adding the modifier that would've caused it");
                    return false;
                }
                else
                {
                    // Get the observers buffer on the entity of the attribute that the modifier observes
                    if (AttributeObserverLookup.TryGetBuffer(attributeObservedByModifier.Entity, out DynamicBuffer<AttributeObserver> attributeObserversBufferOnTheEntityOfTheObservedAttribute) &&
                        AttributeObserverCleanupLookup.TryGetBuffer(attributeObservedByModifier.Entity, out DynamicBuffer<AttributeObserverCleanup> attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute))
                    {
                        // ...and add this attrite as an observer of the modifier's observed attribute
                        AddOrIncrementObserver(attribute, attributeObservedByModifier.AttributeType, attributeObservedByModifier.Entity, ref attributeObserversBufferOnTheEntityOfTheObservedAttribute, ref attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute);
                    }
                    else
                    {
                        // No observers buffer
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Removes the specified attribute as an observer of the attributes that the modifier observes
        /// </summary>
        [BurstCompile]
        private void RemoveAttributeAsObserverOfAllAttributesObservedByModifier(AttributeReference attribute, TAttributeModifier modifier)
        {
            AttributesOnbservedByModifier.Clear();
            modifier.AddObservedAttributesToList(ref AttributesOnbservedByModifier);

            // For each attribute observed by the modifier...
            for (int i = 0; i < AttributesOnbservedByModifier.Length; i++)
            {
                // Get the observers buffer on the entity of the attribute that the modifier observes
                AttributeReference attributeObservedByModifier = AttributesOnbservedByModifier[i];
                if (AttributeObserverLookup.TryGetBuffer(attributeObservedByModifier.Entity, out DynamicBuffer<AttributeObserver> attributeObserversBufferOnTheEntityOfTheObservedAttribute) &&
                    AttributeObserverCleanupLookup.TryGetBuffer(attributeObservedByModifier.Entity, out DynamicBuffer<AttributeObserverCleanup> attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute))
                {
                    // ...and remove this attrite as an observer of the modifier's observed attribute
                    RemoveOrDecrementObserver(attribute, attributeObservedByModifier.AttributeType, ref attributeObserversBufferOnTheEntityOfTheObservedAttribute, ref attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute);
                }
            }
        }

        [BurstCompile]
        private void RemoveOrDecrementObserver(
            AttributeReference observer, 
            int observedAttributeType, 
            ref DynamicBuffer<AttributeObserver> attributeObserversBufferOnTheEntityOfTheObservedAttribute,
            ref DynamicBuffer<AttributeObserverCleanup> attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute)
        {
            // Iterate the observers in buffer and try to find self
            for (int o = attributeObserversBufferOnTheEntityOfTheObservedAttribute.Length - 1; o >= 0; o--)
            {
                AttributeObserver iteratedObserver = attributeObserversBufferOnTheEntityOfTheObservedAttribute[o];
                if (iteratedObserver.IsSame(observer, observedAttributeType))
                {
                    // If found the attribute as observer, remove it.
                    // Decrement the amount of times this attribute is counted as an observer
                    iteratedObserver.Count -= 1;
                    if (iteratedObserver.Count <= 0)
                    {
                        // If 0, remove completely
                        attributeObserversBufferOnTheEntityOfTheObservedAttribute.RemoveAtSwapBack(o);

                        // ...and remove from cleanup
                        for (int c = attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute.Length - 1; c >= 0; c--)
                        {
                            if(attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute[c].ObserverEntity == iteratedObserver.ObserverAttribute.Entity)
                            {
                                attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute.RemoveAtSwapBack(c);
                            }
                        }
                    }
                    else
                    {
                        // If greater than 0, simply update the value
                        attributeObserversBufferOnTheEntityOfTheObservedAttribute[o] = iteratedObserver;
                    }

                    break;
                }
            }
        }

        [BurstCompile]
        private void AddOrIncrementObserver(
            AttributeReference observer, 
            int observedAttributeType, 
            Entity entityOfObserverBuffers,
            ref DynamicBuffer<AttributeObserver> attributeObserversBufferOnTheEntityOfTheObservedAttribute, 
            ref DynamicBuffer<AttributeObserverCleanup> attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute)
        {
            // Try to see if the attribute is already an observer in the buffer
            for (int o = attributeObserversBufferOnTheEntityOfTheObservedAttribute.Length - 1; o >= 0; o--)
            {
                AttributeObserver iteratedObserver = attributeObserversBufferOnTheEntityOfTheObservedAttribute[o];
                if (iteratedObserver.IsSame(observer, observedAttributeType))
                {
                    // If found the attribute as observer, increment it
                    iteratedObserver.Count += 1;
                    attributeObserversBufferOnTheEntityOfTheObservedAttribute[o] = iteratedObserver;

                    return;
                }
            }

            // If we haven't returned yet, it means we haven't found an existing match. Add new observer to buffer
            attributeObserversBufferOnTheEntityOfTheObservedAttribute.Add(new AttributeObserver
            {
                ObserverAttribute = observer,
                ObservedAttributeType = observedAttributeType,
                Count = 1,
            });

            // ...and add to cleanup if not already present and if not self entity
            {
                bool shouldAddToCleanup = observer.Entity != entityOfObserverBuffers;
                for (int i = 0; i < attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute.Length; i++)
                {
                    if (attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute[i].ObserverEntity == observer.Entity)
                    {
                        shouldAddToCleanup = false;
                        break;
                    }
                }
                if (shouldAddToCleanup)
                {
                    attributeObserversCleanupBufferOnTheEntityOfTheObservedAttribute.Add(new AttributeObserverCleanup
                    {
                        ObserverEntity = observer.Entity,
                    });
                }
            }
        }

        /// <summary>
        /// Is attributeA an observer of attributeB (or an observer of any entity that observes attributeB directly or indirectly)?
        /// 
        /// One reason why we might want to know this is to avoid creating infinite observers loop.
        /// If we find out with this function that attributeA is an observer (direct or indirect) of attributeB, 
        /// Then we would know that adding attributeB as an observer of attributeA would create an infinite observers loop.
        /// </summary>
        [BurstCompile]
        private bool IsAttributeAAnObserverOfAttributeBOrAnyObserverOfAttributeB(AttributeReference attributeA, AttributeReference attributeB)
        {
            // Try to see if we find attributeA in the observers of attributeB
            if (AttributeObserverLookup.TryGetBuffer(attributeB.Entity, out DynamicBuffer<AttributeObserver> observerAttributes))
            {
                for (int i = 0; i < observerAttributes.Length; i++)
                {
                    AttributeObserver observerOfAttributeBEntity = observerAttributes[i];

                    // If this is an observer of attribute B...
                    if (observerOfAttributeBEntity.ObservedAttributeType == attributeB.AttributeType)
                    {
                        AttributeReference observerOfAttributeB = observerOfAttributeBEntity.ObserverAttribute;
                        if (observerOfAttributeB.IsSame(attributeA) ||
                            // This observer of attributeB wasn't attributeA...
                            // ...but maybe attributeA could be an observer of this observer of attributeB...
                            // ...and this would make attributeA an observer of attributeB by extension!
                            // ...We must continue the search up every branch of the observers tree until we've reached the end of these branches
                            IsAttributeAAnObserverOfAttributeBOrAnyObserverOfAttributeB(attributeA, observerOfAttributeB))
                        {
                            // We've found attributeA as an observer of attributeB (direct or indirect)
                            return true;
                        }
                    }
                }
            }

            // No observers buffer means no attribute entity; therefore it's the end of this observers bramch
            // of attributeB and we haven't found attributeA
            return false;
        }
    }
}