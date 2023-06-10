using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Trove.Attributes
{
    public static class AttributeUtilities<TAttributeModifier, TModifierStack, TAttributeGetterSetter>
            where TAttributeModifier : unmanaged, IBufferElementData, IAttributeModifier<TModifierStack, TAttributeGetterSetter>
            where TModifierStack : unmanaged, IAttributeModifierStack
            where TAttributeGetterSetter : unmanaged, IAttributeGetterSetter
    {
        public static void MakeAttributeOwner(IBaker baker)
        {
            baker.AddComponent(baker.GetEntity(TransformUsageFlags.None), new AttributesOwner());
            baker.AddBuffer<TAttributeModifier>(baker.GetEntity(TransformUsageFlags.None));
            baker.AddBuffer<AttributeObserver>(baker.GetEntity(TransformUsageFlags.None));
        }

        public static void MakeAttributeOwner(EntityManager entityManager, Entity entity)
        {
            if(!entityManager.HasComponent<AttributesOwner>(entity))
            {
                entityManager.AddComponentData(entity, new AttributesOwner());
            }
            if (!entityManager.HasBuffer<TAttributeModifier>(entity))
            {
                entityManager.AddBuffer<TAttributeModifier>(entity);
            }
            if (!entityManager.HasBuffer<AttributeObserver>(entity))
            {
                entityManager.AddBuffer<AttributeObserver>(entity);
            }
        }

        public static void MakeAttributeOwner(EntityCommandBuffer ecb, Entity entity)
        {
            ecb.AddComponent(entity, new AttributesOwner());
            ecb.AddBuffer<TAttributeModifier>(entity);
            ecb.AddBuffer<AttributeObserver>(entity);
        }

        public static void MakeAttributeOwner(EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity entity)
        {
            ecb.AddComponent(sortKey, entity, new AttributesOwner());
            ecb.AddBuffer<TAttributeModifier>(sortKey, entity);
            ecb.AddBuffer<AttributeObserver>(sortKey, entity);
        }

        public static bool GetNewModifierID(Entity ownerEntity, ref ComponentLookup<AttributesOwner> attributesOwnerLookup, out uint newID)
        {
            if (attributesOwnerLookup.TryGetComponent(ownerEntity, out AttributesOwner attributesOwner))
            {
                attributesOwner.ModifierIDCounter += 1;
                newID = attributesOwner.ModifierIDCounter;
                attributesOwnerLookup[ownerEntity] = attributesOwner;

                return true;
            }

            newID = default;
            return false;
        }

        public static bool GetModifier(ModifierReference modifierReference, DynamicBuffer<TAttributeModifier> modifiersBuffer, out TAttributeModifier modifier, out int modifierIndex)
        {
            for (int i = 0; i < modifiersBuffer.Length; i++)
            {
                TAttributeModifier tmpModifier = modifiersBuffer[i];
                if (tmpModifier.ModifierID == modifierReference.ID)
                {
                    modifier = tmpModifier;
                    modifierIndex = i;
                    return true;
                }
            }

            modifier = default;
            modifierIndex = -1;
            return false;
        }

        public static void NotifyAttributesOwnerDestruction(
            Entity destroyedEntity,
            ref DynamicBuffer<AttributeObserver> destroyedAttributesEntityObservers,
            ref DynamicBuffer<AttributeCommand<TAttributeModifier, TModifierStack, TAttributeGetterSetter>> attributeCommands)
        {
            for (int i = 0; i < destroyedAttributesEntityObservers.Length; i++)
            {
                AttributeReference observer = destroyedAttributesEntityObservers[i].ObserverAttribute;
                if (observer.Entity != destroyedEntity)
                {
                    attributeCommands.Add(AttributeCommand<TAttributeModifier, TModifierStack, TAttributeGetterSetter>.Create_RecalculateAttributeAndAllObservers(observer));
                }
            }
        }
    }

    public static class AttributeExtensions
    {
        public static bool IsSame(this AttributeReference a, AttributeReference b)
        {
            return (a.Entity == b.Entity) && (a.AttributeType == b.AttributeType);
        }

        public static bool IsSame(this AttributeObserver observer, AttributeReference observerAttribute, int observedAttributeType)
        {
            return (observer.ObserverAttribute.IsSame(observerAttribute)) && (observer.ObservedAttributeType == observedAttributeType);
        }
    }
}