using Unity.Entities;
using UnityEngine;

namespace Trove.Audio.FMOD
{
    class FMODEmitterPlayPropertiesAuthoring : MonoBehaviour
    {
        public bool PlayOnCreated = false;
        public bool StopOnDestroyed = true;
        public bool PlayOnEnabled = false;
        public bool StopOnDisabled = false;
    }

    class FMODEmitterPlayPropertiesAuthoringBaker : Baker<FMODEmitterPlayPropertiesAuthoring>
    {
        public override void Bake(FMODEmitterPlayPropertiesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new FMODEmitterPlayProperties
            {
                PlayOnCreated = authoring.PlayOnCreated,
                StopOnDestroyed = authoring.StopOnDestroyed,
                PlayOnEnabled = authoring.PlayOnEnabled,
                StopOnDisabled = authoring.StopOnDisabled,
            });
        }
    }
}
