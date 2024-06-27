using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Trove.Stats
{
    [System.Serializable]
    public struct StatDefinition
    {
        public float BaseValue;
    }

    class StatOwnerAuthoring : MonoBehaviour
    {
        // TODO: what to do about that
        public StatDefinition[] StatDefinitions;

        class Baker : Baker<StatOwnerAuthoring>
        {
            public override void Bake(StatOwnerAuthoring authoring)
            {
                Entity entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, new StatOwner
                {
                    ModifierIdCounter = 1,
                });
                DynamicBuffer<Stat> statsBuffer = AddBuffer<Stat>(entity);
                DynamicBuffer<StatModifier> statModifiersBuffer = AddBuffer<StatModifier>(entity);
                DynamicBuffer<StatObserver> statObserversBuffer = AddBuffer<StatObserver>(entity);
                DynamicBuffer<DirtyStat> dirtyStatsBuffer = AddBuffer<DirtyStat>(entity);
                AddComponent(entity, new HasDirtyStats());

                statsBuffer.Resize(authoring.StatDefinitions.Length, Unity.Collections.NativeArrayOptions.ClearMemory);
                dirtyStatsBuffer.Resize(authoring.StatDefinitions.Length, Unity.Collections.NativeArrayOptions.ClearMemory);
                for (int i = 0; i < authoring.StatDefinitions.Length; i++)
                {
                    statsBuffer[i] = new Stat
                    {
                        Exists = 1,
                        BaseValue = authoring.StatDefinitions[i].BaseValue,
                        Value = authoring.StatDefinitions[i].BaseValue,
                    };
                    dirtyStatsBuffer[i] = new DirtyStat
                    {
                        Value = 1,
                    };
                }
            }
        }
    }
}