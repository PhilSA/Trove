using Unity.Entities;
using UnityEngine;

namespace DOTSFMOD
{
    [AddComponentMenu("DOTSFMOD/FMODDOTSListener")]
    public class FMODListenerAuthoring : MonoBehaviour
    {
        public class Baker : Baker<FMODListenerAuthoring>
        {
            public override void Bake(FMODListenerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new FMODListener
                {
                    ListenerIndex = -1,
                    PreviousPosition = authoring.transform.position,
                });
            }
        }
    }
}
