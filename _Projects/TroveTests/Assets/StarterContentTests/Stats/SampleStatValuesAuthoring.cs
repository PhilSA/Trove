using Unity.Entities;
using UnityEngine;

class SampleStatValuesAuthoring : MonoBehaviour
{
    
}

class SampleStatValuesAuthoringBaker : Baker<SampleStatValuesAuthoring>
{
    public override void Bake(SampleStatValuesAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new SampleStatValues());
    }
}
