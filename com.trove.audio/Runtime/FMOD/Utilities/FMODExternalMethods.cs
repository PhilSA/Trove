using System;
using System.Runtime.InteropServices;
using FMOD;
using FMOD.Studio;
using Unity.Entities;

namespace Trove.Audio.FMOD
{
    public struct FMODExternalMethods : IComponentData
    {
        [DllImport(STUDIO_VERSION.dll)]
        internal static extern RESULT FMOD_Studio_EventInstance_SetParametersByIDs(IntPtr _event, IntPtr ids,
            IntPtr values, int count, bool ignoreseekspeed);

        [DllImport(STUDIO_VERSION.dll)]
        internal static extern RESULT FMOD_Studio_EventDescription_GetParameterDescriptionByName(IntPtr eventdescription,
            IntPtr name, out PARAMETER_DESCRIPTION parameter);
    }
}