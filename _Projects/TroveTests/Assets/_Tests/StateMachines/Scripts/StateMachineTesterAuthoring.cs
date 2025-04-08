using Unity.Entities;
using UnityEngine;

class StateMachineTesterAuthoring : MonoBehaviour
{
    public GameObject Prefab;
    public int SpawnCount;
    public float SpawnSpacing;
}

class StateMachineTesterAuthoringBaker : Baker<StateMachineTesterAuthoring>
{
    public override void Bake(StateMachineTesterAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new StateMachineTester
        {
            Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.None),
            SpawnCount = authoring.SpawnCount,
            SpawnSpacing = authoring.SpawnSpacing,
        });
    }
}
