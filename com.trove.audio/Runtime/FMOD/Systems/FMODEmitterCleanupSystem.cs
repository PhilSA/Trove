using FMOD.Studio;
using Unity.Entities;
using Unity.Collections;

namespace Trove.Audio.FMOD
{
    /// <summary>
    /// Cleanup system that releases FMOD event instances when emitter entities are destroyed.
    /// Uses ICleanupComponentData to ensure instances are properly released.
    /// </summary>
    public struct FMODEmitterCleanup : ICleanupComponentData
    {
        public ulong InstanceHandle;
        public bool AllowFadeout;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(FMODEmitterSystem))]
    public partial class FMODEmitterCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Tag newly created emitters with cleanup data
            var ecbAdd = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (state, emitter, entity)
                in SystemAPI.Query<RefRO<FMODEmitterState>, RefRO<FMODEmitterComponent>>()
                    .WithNone<FMODEmitterCleanup>()
                    .WithEntityAccess())
            {
                ecbAdd.AddComponent(entity, new FMODEmitterCleanup
                {
                    InstanceHandle = state.ValueRO.InstanceHandle,
                    AllowFadeout = emitter.ValueRO.AllowFadeout,
                });
            }
            ecbAdd.Playback(EntityManager);
            ecbAdd.Dispose();

            // Update cleanup data with current instance handle
            foreach (var (cleanup, state, emitter)
                in SystemAPI.Query<RefRW<FMODEmitterCleanup>, RefRO<FMODEmitterState>, RefRO<FMODEmitterComponent>>())
            {
                cleanup.ValueRW.InstanceHandle = state.ValueRO.InstanceHandle;
                cleanup.ValueRW.AllowFadeout = emitter.ValueRO.AllowFadeout;
            }

            // Clean up orphaned instances (entity destroyed but cleanup component remains)
            var ecbRemove = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (cleanup, entity)
                in SystemAPI.Query<RefRO<FMODEmitterCleanup>>()
                    .WithNone<FMODEmitterComponent>()
                    .WithEntityAccess())
            {
                var instance = new EventInstance
                {
                    handle = (System.IntPtr)(long)cleanup.ValueRO.InstanceHandle
                };

                if (instance.isValid())
                {
                    instance.stop(cleanup.ValueRO.AllowFadeout
                        ? STOP_MODE.ALLOWFADEOUT
                        : STOP_MODE.IMMEDIATE);
                    instance.release();
                }

                ecbRemove.RemoveComponent<FMODEmitterCleanup>(entity);
            }
            ecbRemove.Playback(EntityManager);
            ecbRemove.Dispose();
        }
    }
}
