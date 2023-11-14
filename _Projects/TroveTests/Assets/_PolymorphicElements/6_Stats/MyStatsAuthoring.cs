using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Trove.Stats;

public struct AuthoringStatDefinition
{
    public StatType Type;
    public float StartValue;
}

public class MyStatsAuthoring : MonoBehaviour
{
    public AuthoringStatDefinition[] StatDefinitions;

    class Baker : Baker<MyStatsAuthoring>
    {
        public override void Bake(MyStatsAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            DynamicBuffer<StatsData> statsDataBuffer = AddBuffer<StatsData>(entity);

            NativeList<StatDefinition> statDefinitions = new NativeList<StatDefinition>(authoring.StatDefinitions.Length, Allocator.Temp);
            for (int i = 0; i < authoring.StatDefinitions.Length; i++)
            {
                statDefinitions.Add(new StatDefinition
                {
                    TypeID = (ushort)authoring.StatDefinitions[i].Type,
                    StartValue = (ushort)authoring.StatDefinitions[i].StartValue,
                });
            }

            StatsHandler.InitializeStatsData(ref statsDataBuffer, in statDefinitions);
            statDefinitions.Dispose();
        }
    }
}