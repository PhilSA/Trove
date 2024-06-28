using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Trove.Stats;

class StatOwnerAuthoring : MonoBehaviour
{
    public StatDefinition[] StatDefinitions;

    class Baker : Baker<StatOwnerAuthoring>
    {
        public override void Bake(StatOwnerAuthoring authoring)
        {
            StatUtilities.BakeStatsOwner<StatModifier, StatModifier.Stack>(this, authoring, authoring.StatDefinitions);
        }
    }
}