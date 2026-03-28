using FMOD;
using FMOD.Studio;
using Trove.Audio.FMOD;
using FMODUnity;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Trove.Audio.FMOD
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    partial struct FMODSingletonSystem : ISystem
    {
        private NativeHashMap<global::FMOD.GUID, global::FMOD.Studio.EventDescription> _cachedEventDescriptions;

        public void OnCreate(ref SystemState state)
        {
            RuntimeUtils.EnforceLibraryOrder();

            _cachedEventDescriptions = new NativeHashMap<GUID, EventDescription>(32, Allocator.Persistent);

            // Create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new FMODSingleton
            {
                StudioSystem = FMODUnity.RuntimeManager.StudioSystem,
                CachedEventDescriptions = _cachedEventDescriptions,
            });

            state.RequireForUpdate<FMODSingleton>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_cachedEventDescriptions.IsCreated)
            {
                _cachedEventDescriptions.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            RuntimeUtils.EnforceLibraryOrder();

            // Update singleton, in case that's needed
            // TODO: is it ever needed? Ex: can a new StudioSystem be created sometimes? Can the whole RuntimeManager re-initialize sometimes?
            ref FMODSingleton singletonRef = ref SystemAPI.GetSingletonRW<FMODSingleton>().ValueRW;
            singletonRef.StudioSystem = FMODUnity.RuntimeManager.StudioSystem;
            
            if(!singletonRef.StudioSystem.isValid())
                return;

            EntityQuery singletonQuery = SystemAPI.QueryBuilder().WithAll<FMODSingleton>().Build();
            singletonQuery.CompleteDependency();
            
            ComponentLookup<FMODEventEmitterCleanup> eventEmitterCleanupLookup = 
                SystemAPI.GetComponentLookup<FMODEventEmitterCleanup>(false);
            
        }
    }
}