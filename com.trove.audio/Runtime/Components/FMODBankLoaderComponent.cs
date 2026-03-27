using Unity.Entities;
using Unity.Collections;

namespace DOTSFMOD
{
    /// <summary>
    /// Buffer element storing a bank name to be loaded or unloaded.
    /// </summary>
    public struct FMODBankReferenceElement : IBufferElementData
    {
        public FixedString512Bytes BankName;
    }

    /// <summary>
    /// Component controlling bank loading behavior.
    /// </summary>
    public struct FMODBankLoaderComponent : IComponentData
    {
        public bool PreloadSamples;
        public bool IsLoaded;
    }

    /// <summary>
    /// Enable/disable tag to request banks to be loaded.
    /// </summary>
    public struct FMODBankLoadRequest : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Enable/disable tag to request banks to be unloaded.
    /// </summary>
    public struct FMODBankUnloadRequest : IComponentData, IEnableableComponent
    {
    }
}
