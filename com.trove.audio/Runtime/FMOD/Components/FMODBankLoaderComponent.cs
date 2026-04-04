using Unity.Entities;
using Unity.Collections;

namespace Trove.Audio.FMOD
{
    public struct FMODBankElement : IBufferElementData
    {
        public FixedString512Bytes BankName;
    }

    public struct FMODBankLoaderComponent : IComponentData
    {
        public bool PreloadSamples;
    }

    public struct FMODBankLoadRequest : IComponentData, IEnableableComponent
    {
    }

    public struct FMODBankUnloadRequest : IComponentData, IEnableableComponent
    {
    }
}
