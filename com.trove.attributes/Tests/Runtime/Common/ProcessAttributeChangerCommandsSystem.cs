using Trove.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using AttributeChanger = Trove.Attributes.AttributeChanger<Trove.Attributes.Tests.AttributeModifier, Trove.Attributes.Tests.AttributeModifierStack, Trove.Attributes.Tests.AttributeGetterSetter>;
using AttributeCommand = Trove.Attributes.AttributeCommand<Trove.Attributes.Tests.AttributeModifier, Trove.Attributes.Tests.AttributeModifierStack, Trove.Attributes.Tests.AttributeGetterSetter>;

namespace Trove.Attributes.Tests
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndInitializationEntityCommandBufferSystem))]
    public partial struct ProcessAttributeChangerCommandsSystem : ISystem
    {
        private AttributeChanger _attributeChanger;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _attributeChanger = new AttributeChanger(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _attributeChanger.UpdateData(ref state);

            state.Dependency = new AttributeChangerCommandsJob
            {
                AttributeChanger = _attributeChanger,
                modifierNotificationsLookup = SystemAPI.GetBufferLookup<ModifierReferenceNotification>(false),
            }.Schedule(state.Dependency);

            state.Dependency = new RemoveAttributeCommandsJob
            {
                ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithChangeFilter(typeof(AttributeCommandElement))]
        public partial struct AttributeChangerCommandsJob : IJobEntity
        {
            public AttributeChanger AttributeChanger;
            public BufferLookup<ModifierReferenceNotification> modifierNotificationsLookup;

            void Execute(ref DynamicBuffer<AttributeCommandElement> changerCommands)
            {
                for (int i = 0; i < changerCommands.Length; i++)
                {
                    changerCommands[i].Command.Process(ref AttributeChanger, ref modifierNotificationsLookup);
                }
                changerCommands.Clear();
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(AttributeCommandElement))]
        [WithAll(typeof(AttributeCommandElement))]
        [WithAll(typeof(RemoveAttributeCommands))]
        public partial struct RemoveAttributeCommandsJob : IJobEntity
        {
            public EntityCommandBuffer ecb;

            void Execute(Entity entity)
            {
                ecb.RemoveComponent<AttributeCommandElement>(entity);
                ecb.RemoveComponent<RemoveAttributeCommands>(entity);
            }
        }
    }
}