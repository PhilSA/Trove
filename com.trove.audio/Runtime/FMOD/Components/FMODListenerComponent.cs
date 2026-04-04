using Unity.Entities;
using Unity.Mathematics;

namespace Trove.Audio.FMOD
{
    public struct FMODListener : IComponentData
    {
        public int ListenerIndex;
        public Entity AttenuationEntity;
        public bool NonRigidbodyVelocity;

        public float3 AttenuationPosition;
        public float3 PreviousPosition;
    }
}
