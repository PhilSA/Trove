using FMOD.Studio;
using Unity.Entities;

namespace Trove.Audio.FMOD
{
    /// <summary>
    /// Applies parameter values from FMODEmitterParameterElement buffers to active FMOD event instances.
    /// Runs after FMODEmitterSystem to ensure instances are created before parameters are applied.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FMODEmitterSystem))]
    public partial class FMODParameterSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<FMODEmitterParameterElement>();
        }

        protected override void OnUpdate()
        {
            if (!FMODUnity.RuntimeManager.IsInitialized)
                return;

            UnityEngine.Debug.Log($"PARAMSYSTEM update");

            foreach (var (state, paramBuffer, emitter)
                in SystemAPI.Query<RefRO<FMODEmitterState>, DynamicBuffer<FMODEmitterParameterElement>, RefRO<FMODEmitterComponent>>())
            {
                DynamicBuffer<FMODEmitterParameterElement> buff = paramBuffer;

                var instance = new EventInstance
                {
                    handle = (System.IntPtr)(long)state.ValueRO.InstanceHandle
                };

                UnityEngine.Debug.Log($"PARAMSYSTEM iterating a buffer");

                if (!instance.isValid())
                    continue;

                UnityEngine.Debug.Log($"PARAMSYSTEM iterating a params");

                for (int i = 0; i < paramBuffer.Length; i++)
                {
                    FMODEmitterParameterElement param = buff[i];

                    UnityEngine.Debug.Log($"{param.Name}, {param.ID}");

                    if (!param.IDCached)
                    {
                        var eventRef = new FMODUnity.EventReference { Guid = emitter.ValueRO.EventGuid };
                        var desc = FMODUnity.RuntimeManager.GetEventDescription(eventRef);
                        if (desc.isValid())
                        {
                            UnityEngine.Debug.Log($"SETTING uncached");
                            desc.getParameterDescriptionByName(
                                param.Name.ToString(),
                                out PARAMETER_DESCRIPTION paramDesc);
                            param.ID = paramDesc.id;
                            param.IDCached = true;
                            buff[i] = param;
                        }
                    }

                    if (param.IDCached)
                    {
                        UnityEngine.Debug.Log($"SETTING cached");
                        instance.setParameterByID(param.ID, param.Value);
                    }
                }
            }
        }
    }
}
