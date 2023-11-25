using Trove.Attributes;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using AttributeChanger = Trove.Attributes.AttributeChanger<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;
using AttributeUtilities = Trove.Attributes.AttributeUtilities<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;
using AttributeCommand = Trove.Attributes.AttributeCommand<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;

[System.Serializable]
public struct ChangingAttribute : IComponentData
{
    public AttributeType AttributeType;
}

public partial struct AttributesTesterSystem : ISystem
{
    private AttributeChanger _attributeChanger;

    public void OnCreate(ref SystemState state)
    {
        _attributeChanger = new AttributeChanger(ref state);

        state.RequireForUpdate<AttributesTester>();
    }

    public void OnDestroy(ref SystemState state)
    { }

    public void OnUpdate(ref SystemState state)
    {
        _attributeChanger.UpdateData(ref state);

        // Test init
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (test, entity) in SystemAPI.Query<AttributesTester>().WithEntityAccess())
        {
            Entity attributeCommandsEntity = AttributeCommandElement.CreateAttributeCommandsEntity(ecb, out DynamicBuffer<AttributeCommand> commands);

            // Entities with unchanging attributes
            for (int i = 0; i < test.UnchangingAttributesCount; i++)
            {
                CreateAttributesOwner(ecb, false, false, false);
            }

            // Entities with changing attributes
            for (int i = 0; i < test.ChangingAttributesCount; i++)
            {
                Entity newAttributeOwner = CreateAttributesOwner(ecb, true, false, false);

                Entity currentParentAttribute = newAttributeOwner;
                for (int c = 0; c < test.ChangingAttributesChildDepth; c++)
                {
                    Entity newChildAttribute = CreateAttributesOwner(ecb, false, false, false);
                    commands.Add(AttributeCommand.Create_AddModifier(
                        new AttributeReference(newChildAttribute, (int)AttributeType.Strength),
                        AttributeModifier.Create_AddFromAttribute(new AttributeReference(currentParentAttribute, (int)AttributeType.Strength))));

                    currentParentAttribute = newChildAttribute;
                }
            }

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        // Re-update data after structural changes
        _attributeChanger.UpdateData(ref state);

        // Process deferred changes
        state.WorldUnmanaged.GetExistingUnmanagedSystem<ProcessAttributeChangerCommandsSystem>().Update(state.WorldUnmanaged);

        // Changing attributes
        ChangingAttributesJob changingAttributesJob = new ChangingAttributesJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            AttributeChanger = _attributeChanger,
        };
        state.Dependency = changingAttributesJob.Schedule(state.Dependency);

        state.EntityManager.CompleteAllTrackedJobs();
    }

    private static Entity CreateAttributesOwner(EntityCommandBuffer ecb, bool changingStr, bool changingDex, bool changingInt)
    {
        Entity newAttributeOwner = ecb.CreateEntity();
        AttributeUtilities.MakeAttributeOwner(ecb, newAttributeOwner);

        ecb.AddComponent(newAttributeOwner, new Strength
        {
            Values = new AttributeValues(10f),
        });

        ecb.AddComponent(newAttributeOwner, new Dexterity
        {
            Values = new AttributeValues(10f),
        });

        ecb.AddComponent(newAttributeOwner, new Intelligence
        {
            Values = new AttributeValues(10f),
        });

        if (changingStr)
        {
            ecb.AddComponent(newAttributeOwner, new ChangingAttribute
            {
                AttributeType = AttributeType.Strength,
            });
        }

        if (changingDex)
        {
            ecb.AddComponent(newAttributeOwner, new ChangingAttribute
            {
                AttributeType = AttributeType.Dexterity,
            });
        }

        if (changingInt)
        {
            ecb.AddComponent(newAttributeOwner, new ChangingAttribute
            {
                AttributeType = AttributeType.Intelligence,
            });
        }

        return newAttributeOwner;
    }
}

[BurstCompile]
public partial struct ChangingAttributesJob : IJobEntity
{
    public float DeltaTime;
    public AttributeChanger AttributeChanger;

    void Execute(Entity entity, in ChangingAttribute changingAttribute)
    {
        AttributeChanger.AddBaseValue(new AttributeReference(entity, (int)changingAttribute.AttributeType), DeltaTime);
    }
}