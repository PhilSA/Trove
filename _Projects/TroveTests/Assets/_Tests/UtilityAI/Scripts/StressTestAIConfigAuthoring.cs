using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Trove.UtilityAI;

public class StressTestAIConfigAuthoring : MonoBehaviour
{
    public GameObject Prefab;
    public int SpawnCount;

    class Baker : Baker<StressTestAIConfigAuthoring>
    {
        public override void Bake(StressTestAIConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new StressTestAIConfig
            {
                Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.None),
                SpawnCount = authoring.SpawnCount,
            });
        }
    }
}
