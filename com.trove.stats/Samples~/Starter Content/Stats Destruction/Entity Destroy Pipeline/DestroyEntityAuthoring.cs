using Unity.Entities;
using UnityEngine;

class DestroyEntityAuthoring : MonoBehaviour
{ }

class DestroyEntityAuthoringBaker : Baker<DestroyEntityAuthoring>
{
    public override void Bake(DestroyEntityAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new DestroyEntity());
        SetComponentEnabled<DestroyEntity>(entity, false);
    }
}
