using Trove.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Trove.Attributes
{
    public struct AttributeCommandsProcessing : IComponentData
    {
        public byte WasProcessed;
    }

    [Serializable]
    public struct AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> 
        where TAttributeModifier : unmanaged, IBufferElementData, IAttributeModifier<TAttributeModifierStack, TAttributeGetterSetter>
        where TAttributeModifierStack : unmanaged, IAttributeModifierStack
        where TAttributeGetterSetter : unmanaged, IAttributeGetterSetter
    {
        public enum CommandType
        {
            SetBaseValue,
            AddBaseValue,

            RecalculateAttributeAndAllObservers,
            RecalculateAllObservers,

            AddModifier,

            RemoveModifier,
            RemoveAllModifiers,
            RemoveAllModifiersAffectingAttribute,
            RemoveAllModifiersObservingEntityOnEntity,
            RemoveAllModifiersObservingAttribute,
            RemoveAllModifiersObservingAttributeOnEntity,
        }

        public CommandType Type;
        public int NotificationID;

        public AttributeReference AttributeReference;
        public float Value;
        public TAttributeModifier Modifier;
        public ModifierReference ModifierReference;
        public bool AutoRecalculate;
        public Entity EntityA;
        public Entity EntityB;

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_SetBaseValue(AttributeReference attribute, float newBaseValue, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.SetBaseValue,
                AttributeReference = attribute,
                Value = newBaseValue,
                AutoRecalculate = autoRecalculate,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_AddBaseValue(AttributeReference attribute, float addValue, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.AddBaseValue,
                AttributeReference = attribute,
                Value = addValue,
                AutoRecalculate = autoRecalculate,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_RecalculateAttributeAndAllObservers(AttributeReference attribute)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.RecalculateAttributeAndAllObservers,
                AttributeReference = attribute,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_RecalculateAllObservers(AttributeReference attribute)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.RecalculateAllObservers,
                AttributeReference = attribute,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_AddModifier(AttributeReference attribute, TAttributeModifier modifier, Entity modifierReferenceNotificationTarget = default, int notificationID = -1, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.AddModifier,
                NotificationID = notificationID,
                AttributeReference = attribute,
                Modifier = modifier,
                EntityA = modifierReferenceNotificationTarget,
                AutoRecalculate = autoRecalculate,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_RemoveModifier(ModifierReference modifierReference, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.RemoveModifier,
                ModifierReference = modifierReference,
                AutoRecalculate = autoRecalculate,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_RemoveAllModifiers(Entity onEntity, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.RemoveAllModifiers,
                EntityA = onEntity,
                AutoRecalculate = autoRecalculate,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_RemoveAllModifiersAffectingAttribute(AttributeReference attribute, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.RemoveAllModifiersAffectingAttribute,
                AttributeReference = attribute,
                AutoRecalculate = autoRecalculate,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_RemoveAllModifiersObservingEntityOnEntity(Entity observedEntity, Entity onEntity, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.RemoveAllModifiersObservingEntityOnEntity,
                EntityA = observedEntity,
                EntityB = onEntity,
                AutoRecalculate = autoRecalculate,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_RemoveAllModifiersObservingAttribute(AttributeReference attribute, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.RemoveAllModifiersObservingAttribute,
                AttributeReference = attribute,
                AutoRecalculate = autoRecalculate,
            };
        }

        public static AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> Create_RemoveAllModifiersObservingAttributeOnEntity(Entity onEntity, AttributeReference attribute, bool autoRecalculate = true)
        {
            return new AttributeCommand<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter>
            {
                Type = CommandType.RemoveAllModifiersObservingAttributeOnEntity,
                AttributeReference = attribute,
                EntityA = onEntity,
                AutoRecalculate = autoRecalculate,
            };
        }

        public void Process(ref AttributeChanger<TAttributeModifier, TAttributeModifierStack, TAttributeGetterSetter> attributeChanger, ref BufferLookup<ModifierReferenceNotification> notificationsBufferLookup)
        {
#if UNITY_EDITOR
            if (AttributeReference.Entity.Index < 0)
            {
                UnityEngine.Debug.LogError($"Error: DeferredAttributesChanger.Reader tried to process a command affecting an " +
                    $"entity that has not yet been created by ECB playback. You must make sure all entities affected by deferred " +
                    $"commands have been fully created before you process these commands; otherwise the command will not be processed.");
            }
#endif

            switch (Type)
            {
                case CommandType.SetBaseValue:
                    {
                        attributeChanger.SetBaseValue(AttributeReference, Value);
                    }
                    break;
                case CommandType.AddBaseValue:
                    {
                        attributeChanger.AddBaseValue(AttributeReference, Value);
                    }
                    break;
                case CommandType.RecalculateAttributeAndAllObservers:
                    {
                        attributeChanger.RecalculateAttributeAndAllObservers(AttributeReference);
                    }
                    break;
                case CommandType.RecalculateAllObservers:
                    {
                        attributeChanger.RecalculateAllObservers(AttributeReference);
                    }
                    break;
                case CommandType.AddModifier:
                    {
                        if (attributeChanger.AddModifier(AttributeReference, Modifier, out ModifierReference modifierReference))
                        {
                            // Notify
                            if (EntityA != Entity.Null &&
                                notificationsBufferLookup.TryGetBuffer(EntityA, out DynamicBuffer<ModifierReferenceNotification> notificationsBuffer))
                            {
                                notificationsBuffer.Add(new ModifierReferenceNotification
                                {
                                    ModifierReference = modifierReference,
                                    NotificationID = NotificationID,
                                });
                            }
                        }
                    }
                    break;
                case CommandType.RemoveModifier:
                    {
                        attributeChanger.RemoveModifier(ModifierReference, AutoRecalculate);
                    }
                    break;
                case CommandType.RemoveAllModifiers:
                    {
                        attributeChanger.RemoveAllModifiers(EntityA, AutoRecalculate);
                    }
                    break;
                case CommandType.RemoveAllModifiersAffectingAttribute:
                    {
                        attributeChanger.RemoveAllModifiersAffectingAttribute(AttributeReference, AutoRecalculate);
                    }
                    break;
                case CommandType.RemoveAllModifiersObservingEntityOnEntity:
                    {
                        attributeChanger.RemoveAllModifiersObservingEntityOnEntity(EntityA, EntityB, AutoRecalculate);
                    }
                    break;
                case CommandType.RemoveAllModifiersObservingAttribute:
                    {
                        attributeChanger.RemoveAllModifiersObservingAttribute(AttributeReference, AutoRecalculate);
                    }
                    break;
                case CommandType.RemoveAllModifiersObservingAttributeOnEntity:
                    {
                        attributeChanger.RemoveAllModifiersObservingAttributeOnEntity(EntityA, AttributeReference, AutoRecalculate);
                    }
                    break;
            }
        }
    }
}