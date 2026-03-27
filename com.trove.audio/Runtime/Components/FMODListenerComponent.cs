using Unity.Entities;
using Unity.Mathematics;

namespace DOTSFMOD
{
    public struct FMODListener : IComponentData
    {
        public int ListenerIndex;
        public float3 Attenuation;
        public float3 PreviousPosition;
    }
}
