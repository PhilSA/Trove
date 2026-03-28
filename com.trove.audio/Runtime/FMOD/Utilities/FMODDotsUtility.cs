using System.Runtime.CompilerServices;
using FMOD;
using FMODUnity;
using FMOD.Studio;
using Unity.Collections;
using Unity.Entities;
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

        public static global::FMOD.Studio.EventDescription LoadEventFromGUID(
            ref FMODSingleton singleton,
            in global::FMOD.GUID eventGUID,
            ref DynamicBuffer<FMODEmitterParameterElement> parameters)
        {
            global::FMOD.Studio.EventDescription eventDescription = 
                FMODDotsUtility.GetOrCreateEventDescription(ref singleton, in eventGUID);

            if (eventDescription.isValid())
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    FMODEmitterParameterElement paremeterElement = parameters[i];
                    eventDescription.getParameterDescriptionByName(paremeterElement.Name.ConvertToString(), out global::FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription);
                    paremeterElement.CachedID = parameterDescription.id;
                    parameters[i] = paremeterElement;
                }
            }
            
            return eventDescription;
        }

        public static global::FMOD.Studio.EventDescription GetOrCreateEventDescription(ref FMODSingleton singleton, in global::FMOD.GUID eventGUID)
        {
            global::FMOD.Studio.EventDescription eventDesc;
            if (singleton.CachedEventDescriptions.ContainsKey(eventGUID) && singleton.CachedEventDescriptions[eventGUID].isValid())
            {
                eventDesc = singleton.CachedEventDescriptions[eventGUID];
            }
            else
            {
                RESULT result = singleton.StudioSystem.getEventByID(eventGUID, out eventDesc);

                if (result != RESULT.OK)
                {
                    throw new EventNotFoundException(eventGUID);
                }

                if (eventDesc.isValid())
                {
                    singleton.CachedEventDescriptions[eventGUID] = eventDesc;
                }
            }
            return eventDesc;
        }
    }
}
