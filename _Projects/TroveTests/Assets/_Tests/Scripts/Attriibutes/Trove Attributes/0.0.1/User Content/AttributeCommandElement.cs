using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Trove.Attributes;
using AttributeCommand = Trove.Attributes.AttributeCommand<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;

public struct AttributeCommandElement : IBufferElementData
{
    public AttributeCommand Command;

    public static implicit operator AttributeCommandElement(AttributeCommand c)
    {
        return new AttributeCommandElement { Command = c };
    }

    public static Entity CreateAttributeCommandsEntity(EntityManager entityManager, out DynamicBuffer<AttributeCommand> attributeCommands)
    {
        Entity entity = entityManager.CreateEntity(typeof(AttributeCommandsProcessing), typeof(AttributeCommandElement));
        attributeCommands = entityManager.GetBuffer<AttributeCommandElement>(entity).Reinterpret<AttributeCommand>();
        return entity;
    }

    public static Entity CreateAttributeCommandsEntity(EntityCommandBuffer ecb, out DynamicBuffer<AttributeCommand> attributeCommands)
    {
        Entity entity = ecb.CreateEntity();
        ecb.AddComponent(entity, new AttributeCommandsProcessing());
        attributeCommands = ecb.AddBuffer<AttributeCommandElement>(entity).Reinterpret<AttributeCommand>();
        return entity;
    }

    public static Entity CreateAttributeCommandsEntity(EntityCommandBuffer.ParallelWriter ecb, int sortKey, out DynamicBuffer<AttributeCommand> attributeCommands)
    {
        Entity entity = ecb.CreateEntity(sortKey);
        ecb.AddComponent(sortKey, entity, new AttributeCommandsProcessing());
        attributeCommands = ecb.AddBuffer<AttributeCommandElement>(sortKey, entity).Reinterpret<AttributeCommand>();
        return entity;
    }
}
