using Unity.Entities;
using UnityEngine;

class NoTransformAuthoring : MonoBehaviour
{
    class Baker : Baker<NoTransformAuthoring>
    {
        public override void Bake(NoTransformAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.ManualOverride);
        }
    }
}
