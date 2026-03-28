using FMOD.Studio;
using Unity.Entities;
using Unity.Collections;

namespace Trove.Audio.FMOD
{
    /// <summary>
    /// Sets a global FMOD Studio parameter. The system applies the value each frame
    /// while the entity exists, or on demand via FMODGlobalParameterApplyRequest.
    /// </summary>
    public struct FMODGlobalParameterComponent : IComponentData
    {
        public FixedString64Bytes ParameterName;
        public float Value;
        public PARAMETER_ID CachedID;
        public bool IDCached;
    }

    /// <summary>
    /// Enable this to trigger a one-shot application of the global parameter value.
    /// </summary>
    public struct FMODGlobalParameterApplyRequest : IComponentData, IEnableableComponent
    {
    }
}
