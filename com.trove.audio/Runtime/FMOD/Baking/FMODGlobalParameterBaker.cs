using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Trove.Audio.FMOD
{
    /// <summary>
    /// Authoring component for setting global FMOD Studio parameters.
    /// Enable FMODGlobalParameterApplyRequest on the entity to trigger the parameter set.
    /// </summary>
    [AddComponentMenu("DOTSFMOD/FMOD Global Parameter")]
    public class FMODGlobalParameterAuthoring : MonoBehaviour
    {
        [Tooltip("Name of the global FMOD Studio parameter.")]
        [FMODUnity.ParamRef]
        public string ParameterName;

        [Tooltip("Value to set the parameter to.")]
        public float Value;

        [Tooltip("Apply the parameter value immediately on entity creation.")]
        public bool ApplyOnStart = true;

        public class Baker : Baker<FMODGlobalParameterAuthoring>
        {
            public override void Bake(FMODGlobalParameterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new FMODGlobalParameterComponent
                {
                    ParameterName = new FixedString64Bytes(authoring.ParameterName ?? string.Empty),
                    Value = authoring.Value,
                    IDCached = false,
                });

                AddComponent(entity, new FMODGlobalParameterApplyRequest());
                SetComponentEnabled<FMODGlobalParameterApplyRequest>(entity, authoring.ApplyOnStart);
            }
        }
    }
}
