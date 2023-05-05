using Trove.Attributes;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using AttributeChanger = Trove.Attributes.AttributeChanger<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;
using AttributeUtilities = Trove.Attributes.AttributeUtilities<AttributeModifier, AttributeModifierStack, AttributeGetterSetter>;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
public partial struct AttributeObserversCleanupSystem : ISystem
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

        state.Dependency = new AttributeObserversCleanupJob
        {
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            AttributeChanger = _attributeChanger,
        }.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithNone(typeof(AttributeObserver))]
    [WithAll(typeof(AttributeObserverCleanup))]
    public partial struct AttributeObserversCleanupJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public AttributeChanger AttributeChanger;

        void Execute(Entity entity)
        {
            // When a modifier observing EntityA is added on EntityB, an observer cleanup element saying EntityB observes EntityA is added on EntityA.
            // When a modifier observing EntityA is removed on EntityB, the observer cleanup element saying EntityB observes EntityA is removed from EntityA.
            // When an entity is destroyed, we must go through all observer cleanup elements to find out which entities have modifiers that observe this destroyed entity,
            // and we must remove these modifiers.
            if (AttributeChanger.AttributeObserverCleanupLookup.TryGetBuffer(entity, out DynamicBuffer<AttributeObserverCleanup> cleanups))
            {
                for (int i = 0; i < cleanups.Length; i++)
                {
                    AttributeChanger.RemoveAllModifiersObservingEntityOnEntity(entity, cleanups[i].ObserverEntity);
                }
            }

            ECB.RemoveComponent<AttributeObserverCleanup>(entity);
        }
    }
}