using Trove.Audio.FMOD;
using FMODUnity;
using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct FMODSingletonSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // Create singleton
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new FMODSingleton
        {
            StudioSystem = FMODUnity.RuntimeManager.StudioSystem,
        });
        
        state.RequireForUpdate<FMODSingleton>();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        // Update singleton, in case that's needed
        // TODO: is it ever needed? Ex: can a new StudioSystem be created sometimes? Can the whole RuntimeManager re-initialize sometimes?
        ref FMODSingleton singletonRef = ref SystemAPI.GetSingletonRW<FMODSingleton>().ValueRW;
        singletonRef.StudioSystem = FMODUnity.RuntimeManager.StudioSystem;
    }
}
