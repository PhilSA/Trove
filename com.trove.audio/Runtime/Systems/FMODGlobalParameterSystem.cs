using Unity.Entities;

namespace DOTSFMOD
{
    /// <summary>
    /// Applies global FMOD Studio parameter values from FMODGlobalParameterComponent entities.
    /// Triggered by enabling FMODGlobalParameterApplyRequest on the entity.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FMODGlobalParameterSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<FMODGlobalParameterComponent>();
        }

        protected override void OnUpdate()
        {
            if (!FMODUnity.RuntimeManager.IsInitialized)
                return;

            var studioSystem = FMODUnity.RuntimeManager.StudioSystem;
            var ecb = new Unity.Collections.NativeArray<Entity>(0, Unity.Collections.Allocator.Temp);

            foreach (var (param, entity)
                in SystemAPI.Query<RefRW<FMODGlobalParameterComponent>>()
                    .WithAll<FMODGlobalParameterApplyRequest>()
                    .WithEntityAccess())
            {
                EntityManager.SetComponentEnabled<FMODGlobalParameterApplyRequest>(entity, false);

                ref var globalParam = ref param.ValueRW;

                if (globalParam.ParameterName.Length == 0)
                    continue;

                if (!globalParam.IDCached)
                {
                    var result = studioSystem.getParameterDescriptionByName(
                        globalParam.ParameterName.ToString(),
                        out FMOD.Studio.PARAMETER_DESCRIPTION desc);

                    if (result != FMOD.RESULT.OK)
                    {
                        UnityEngine.Debug.LogError(
                            $"[DOTSFMOD] Failed to lookup global parameter '{globalParam.ParameterName}': {result}");
                        continue;
                    }

                    globalParam.CachedID = desc.id;
                    globalParam.IDCached = true;
                }

                studioSystem.setParameterByID(globalParam.CachedID, globalParam.Value);
            }

            ecb.Dispose();
        }
    }
}
