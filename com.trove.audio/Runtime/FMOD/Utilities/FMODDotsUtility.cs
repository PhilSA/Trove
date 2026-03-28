using System.Runtime.CompilerServices;
using FMOD;
using Unity.Mathematics;
using Unity.Transforms;

namespace Trove.Audio.FMOD
{
    /// <summary>
    /// Utility methods for converting ECS transform data to FMOD 3D attributes.
    /// </summary>
    public static class FMODDotsUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VECTOR ToFMODVector(float3 v)
        {
            return new VECTOR { x = v.x, y = v.y, z = v.z };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ATTRIBUTES_3D To3DAttributes(in LocalToWorld ltw, float3 velocity)
        {
            return new ATTRIBUTES_3D
            {
                position = ToFMODVector(ltw.Position),
                velocity = ToFMODVector(velocity),
                forward = ToFMODVector(ltw.Forward),
                up = ToFMODVector(ltw.Up)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ATTRIBUTES_3D To3DAttributes(in LocalTransform transform, float3 velocity)
        {
            return new ATTRIBUTES_3D
            {
                position = ToFMODVector(transform.Position),
                velocity = ToFMODVector(velocity),
                forward = ToFMODVector(math.mul(transform.Rotation, math.forward())),
                up = ToFMODVector(math.mul(transform.Rotation, math.up()))
            };
        }
    }
}
