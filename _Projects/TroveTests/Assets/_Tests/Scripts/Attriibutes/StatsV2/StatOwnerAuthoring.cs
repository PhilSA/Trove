using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Trove.Stats;
using System;


public enum StatType
{
    A = 0,
    B, 
    C,
}

[Serializable]
public struct ModifierAuthoring
{
    public StatType AffectedStatType;

    public StatModifier.Type ModifierType;
    public StatType StatA;
    public float ValueA;

    public BakingModifier<StatModifier, StatModifier.Stack> GetBakingModifier(Entity forEntity)
    {
        return new BakingModifier<StatModifier, StatModifier.Stack>
        {
            AffectedStatIndex = (int)AffectedStatType,
            Modifier = new StatModifier
            {
                ModifierType = ModifierType,
                StatA = new StatHandle(forEntity, (int)StatA),
                ValueA = ValueA,
            },
        };
    }
}

class StatOwnerAuthoring : MonoBehaviour
{
    public StatDefinition StatA = new StatDefinition((int)StatType.A, 10f);
    public StatDefinition StatB = new StatDefinition((int)StatType.B, 10f);
    public StatDefinition StatC = new StatDefinition((int)StatType.C, 10f);

    public List<ModifierAuthoring> InitialModifiers = new List<ModifierAuthoring>();

    class Baker : Baker<StatOwnerAuthoring>
    {
        public override void Bake(StatOwnerAuthoring authoring)
        {
            StatDefinition[] statDefinitions = new StatDefinition[]
            {
                authoring.StatA,
                authoring.StatB,
                authoring.StatC,
            };

            Entity entity = GetEntity(authoring, TransformUsageFlags.None);

            BakingModifier<StatModifier, StatModifier.Stack>[] bakingModifiers = 
                new BakingModifier<StatModifier, StatModifier.Stack>[authoring.InitialModifiers.Count];
            for (int i = 0; i < authoring.InitialModifiers.Count; i++)
            {
                bakingModifiers[i] = authoring.InitialModifiers[i].GetBakingModifier(entity);
            }

            StatUtilities.BakeStatsOwner(this, authoring, typeof(StatType), statDefinitions, bakingModifiers);
        }
    }
}