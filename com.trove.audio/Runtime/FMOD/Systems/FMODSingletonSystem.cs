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
        private NativeReference<UnsafeList<FMODSingleton.ListenerData>> _activeListenerDatas;

        public void OnCreate(ref SystemState state)
        {
            RuntimeUtils.EnforceLibraryOrder();

            _cachedEventDescriptions = new NativeReference<UnsafeHashMap<GUID, EventDescription>>(
                new UnsafeHashMap<GUID, EventDescription>(32, Allocator.Persistent), 
                Allocator.Persistent);
            _activeListenerDatas = new NativeReference<UnsafeList<FMODSingleton.ListenerData>>(
                new UnsafeList<FMODSingleton.ListenerData>(8, Allocator.Persistent), 
                Allocator.Persistent);

            // Create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new FMODSingleton
            {
                StudioSystem = FMODUnity.RuntimeManager.StudioSystem,
                CachedEventDescriptions = _cachedEventDescriptions.GetUnsafePtr(),
                ActiveListenerDatas = _activeListenerDatas.GetUnsafePtr(),
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
            
            if (_activeListenerDatas.IsCreated)
            {
                if (_activeListenerDatas.Value.IsCreated)
                {
                    _activeListenerDatas.Value.Dispose();
                }
                _activeListenerDatas.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            RuntimeUtils.EnforceLibraryOrder();

            EntityQuery singletonQuery = SystemAPI.QueryBuilder().WithAll<FMODSingleton>().Build();
            singletonQuery.CompleteDependency();

            Settings settings = Settings.Instance;
            
            // Update singleton
            ref FMODSingleton singletonRef = ref SystemAPI.GetSingletonRW<FMODSingleton>().ValueRW;
            singletonRef.StudioSystem = FMODUnity.RuntimeManager.StudioSystem;
            singletonRef.StopEventsOutsideMaxDistance = settings.StopEventsOutsideMaxDistance;
        }
    }
}