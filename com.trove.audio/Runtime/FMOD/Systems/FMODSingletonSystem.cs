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
    public unsafe partial struct FMODSingletonSystem : ISystem
    {
        private NativeReference<UnsafeHashMap<global::FMOD.GUID, global::FMOD.Studio.EventDescription>> _cachedEventDescriptions;

        public void OnCreate(ref SystemState state)
        {
            RuntimeUtils.EnforceLibraryOrder();

            _cachedEventDescriptions = new NativeReference<UnsafeHashMap<GUID, EventDescription>>(
                new UnsafeHashMap<GUID, EventDescription>(32, Allocator.Persistent), 
                Allocator.Persistent);

            // Create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new FMODSingleton
            {
                StudioSystem = FMODUnity.RuntimeManager.StudioSystem,
                CachedEventDescriptions = _cachedEventDescriptions.GetUnsafePtr(),
            });

            state.RequireForUpdate<FMODSingleton>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_cachedEventDescriptions.IsCreated)
            {
                if (_cachedEventDescriptions.Value.IsCreated)
                {
                    _cachedEventDescriptions.Value.Dispose();
                }
                _cachedEventDescriptions.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            RuntimeUtils.EnforceLibraryOrder();

            Settings settings = Settings.Instance;
            
            // Update singleton
            ref FMODSingleton singletonRef = ref SystemAPI.GetSingletonRW<FMODSingleton>().ValueRW;
            singletonRef.StudioSystem = FMODUnity.RuntimeManager.StudioSystem;
            singletonRef.StopEventsOutsideMaxDistance = settings.StopEventsOutsideMaxDistance;
            
            if(!singletonRef.StudioSystem.isValid())
                return;

            EntityQuery singletonQuery = SystemAPI.QueryBuilder().WithAll<FMODSingleton>().Build();
            singletonQuery.CompleteDependency();
            
            ComponentLookup<FMODEventEmitterState> eventEmitterCleanupLookup = 
                SystemAPI.GetComponentLookup<FMODEventEmitterState>(false);
            
        }
    }
}