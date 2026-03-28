using FMOD;
using FMOD.Studio;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;

namespace Trove.Audio.FMOD
{
    /// <summary>
    /// Manages FMOD event instance lifecycle for emitter entities.
    /// Handles play/stop requests, 3D attribute updates, and distance-based culling.
    /// Replaces StudioEventEmitter and RuntimeManager's attached instance tracking.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FMODListenerSystem))]
    public partial class FMODEmitterSystem : SystemBase
    {
        private NativeHashMap<Entity, float3> _previousPositions;
        private NativeList<float3> _listenerPositions;

        protected override void OnCreate()
        {
            _previousPositions = new NativeHashMap<Entity, float3>(256, Allocator.Persistent);
            _listenerPositions = new NativeList<float3>(CONSTANTS.MAX_LISTENERS, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            // Release all active instances before shutdown
            foreach (var (state, emitter)
                in SystemAPI.Query<RefRW<FMODEmitterState>, RefRO<FMODEmitterComponent>>())
            {
                ReleaseInstance(ref state.ValueRW, emitter.ValueRO.AllowFadeout);
            }

            if (_previousPositions.IsCreated)
                _previousPositions.Dispose();
            if (_listenerPositions.IsCreated)
                _listenerPositions.Dispose();
        }

        protected override void OnUpdate()
        {
            FMODSingleton singleton = SystemAPI.GetSingleton<FMODSingleton>();
            if(!singleton.StudioSystem.isValid())
                return;

            GatherListenerPositions();
            UpdateEmitter3DAttributes();
            ProcessPlayRequests();
            ProcessStopRequests();
        }

        private void GatherListenerPositions()
        {
            _listenerPositions.Clear();
            foreach (var (listener, transform)
                in SystemAPI.Query<RefRO<FMODListener>, RefRO<LocalTransform>>())
            {
                _listenerPositions.Add(transform.ValueRO.Position);
            }
        }

        private float DistanceSquaredToNearestListener(float3 position)
        {
            float result = float.MaxValue;
            for (int i = 0; i < _listenerPositions.Length; i++)
            {
                float distSq = math.distancesq(position, _listenerPositions[i]);
                if (distSq < result)
                    result = distSq;
            }
            return result;
        }

        private void ProcessPlayRequests()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (emitter, state, transform, entity)
                in SystemAPI.Query<RefRO<FMODEmitterComponent>, RefRW<FMODEmitterState>, RefRO<LocalToWorld>>()
                    .WithAll<FMODPlayRequest>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<FMODPlayRequest>(entity, false);

                var emitterData = emitter.ValueRO;
                ref var emitterState = ref state.ValueRW;

                if (emitterData.TriggerOnce && emitterState.HasTriggered)
                    continue;

                if (emitterData.EventGuid.IsNull)
                    continue;

                // Cache event description info
                if (!emitterState.DescriptionCached)
                {
                    CacheDescriptionInfo(ref emitterState, emitterData);
                }

                emitterState.IsActive = true;
                bool shouldPlayNow = true;

                // Distance-based culling for 3D non-oneshot events
                if (!emitterState.IsOneshot && emitterState.MaxDistance > 0)
                {
                    float distSq = DistanceSquaredToNearestListener(transform.ValueRO.Position);
                    if (distSq > emitterState.MaxDistance * emitterState.MaxDistance)
                        shouldPlayNow = false;
                }

                if (shouldPlayNow)
                {
                    PlayInstance(ref emitterState, emitterData, transform.ValueRO, entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessStopRequests()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (emitter, state, entity)
                in SystemAPI.Query<RefRO<FMODEmitterComponent>, RefRW<FMODEmitterState>>()
                    .WithAll<FMODStopRequest>()
                    .WithEntityAccess())
            {
                ecb.SetComponentEnabled<FMODStopRequest>(entity, false);

                ref var emitterState = ref state.ValueRW;
                emitterState.IsActive = false;
                ReleaseInstance(ref emitterState, emitter.ValueRO.AllowFadeout);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void UpdateEmitter3DAttributes()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var stopList = new NativeList<Entity>(Allocator.Temp);
            var playList = new NativeList<Entity>(Allocator.Temp);

            foreach (var (emitter, state, transform, entity)
                in SystemAPI.Query<RefRO<FMODEmitterComponent>, RefRW<FMODEmitterState>, RefRO<LocalToWorld>>()
                    .WithEntityAccess())
            {
                ref var emitterState = ref state.ValueRW;
                var instance = new EventInstance { handle = (System.IntPtr)(long)emitterState.InstanceHandle };

                if (!instance.isValid())
                {
                    // Distance-based re-evaluation for active non-oneshot emitters
                    if (emitterState.IsActive && !emitterState.IsOneshot && emitterState.MaxDistance > 0)
                    {
                        float distSq = DistanceSquaredToNearestListener(transform.ValueRO.Position);
                        if (distSq <= emitterState.MaxDistance * emitterState.MaxDistance)
                        {
                            playList.Add(entity);
                        }
                    }
                    continue;
                }

                // Check if instance is still playing
                instance.getPlaybackState(out PLAYBACK_STATE playbackState);
                if (playbackState == PLAYBACK_STATE.STOPPED)
                {
                    emitterState.InstanceHandle = 0;
                    continue;
                }

                // Update 3D attributes
                float3 position = transform.ValueRO.Position;
                float3 velocity = float3.zero;

                if (_previousPositions.TryGetValue(entity, out float3 prevPos) && deltaTime > 0)
                {
                    velocity = (position - prevPos) / deltaTime;
                    float speed = math.length(velocity);
                    if (speed > 20f)
                        velocity = math.normalize(velocity) * 20f;
                }
                _previousPositions[entity] = position;

                var attributes = FMODDotsUtility.To3DAttributes(transform.ValueRO, velocity);
                instance.set3DAttributes(attributes);

                // Distance-based culling for active non-oneshot emitters
                if (emitterState.IsActive && !emitterState.IsOneshot && emitterState.MaxDistance > 0)
                {
                    float distSq = DistanceSquaredToNearestListener(position);
                    if (distSq > emitterState.MaxDistance * emitterState.MaxDistance)
                    {
                        stopList.Add(entity);
                    }
                }
            }

            // Process deferred stop/play to avoid modifying during iteration
            foreach (var entity in stopList)
            {
                var state = SystemAPI.GetComponentRW<FMODEmitterState>(entity);
                var emitter = SystemAPI.GetComponentRO<FMODEmitterComponent>(entity);
                ref var emitterState = ref state.ValueRW;
                StopInstance(ref emitterState, emitter.ValueRO.AllowFadeout);
            }

            foreach (var entity in playList)
            {
                var state = SystemAPI.GetComponentRW<FMODEmitterState>(entity);
                var emitter = SystemAPI.GetComponentRO<FMODEmitterComponent>(entity);
                var transform = SystemAPI.GetComponentRO<LocalToWorld>(entity);
                ref var emitterState = ref state.ValueRW;
                PlayInstance(ref emitterState, emitter.ValueRO, transform.ValueRO, entity);
            }

            stopList.Dispose();
            playList.Dispose();
        }

        private void CacheDescriptionInfo(ref FMODEmitterState state, FMODEmitterComponent emitter)
        {
            var eventRef = new FMODUnity.EventReference { Guid = emitter.EventGuid };
            var desc = FMODUnity.RuntimeManager.GetEventDescription(eventRef);

            if (desc.isValid())
            {
                desc.isOneshot(out bool isOneshot);
                desc.is3D(out bool is3D);

                if (is3D)
                {
                    if (emitter.OverrideAttenuation)
                    {
                        state.MaxDistance = emitter.OverrideMaxDistance;
                    }
                    else
                    {
                        desc.getMinMaxDistance(out _, out float maxDist);
                        state.MaxDistance = maxDist;
                    }
                }
                else
                {
                    state.MaxDistance = 0;
                }

                state.IsOneshot = isOneshot;
                state.DescriptionCached = true;
            }
        }

        private void PlayInstance(ref FMODEmitterState state, FMODEmitterComponent emitter,
            LocalToWorld transform, Entity entity)
        {
            var currentInstance = new EventInstance { handle = (System.IntPtr)(long)state.InstanceHandle };

            // Release previous oneshot if still valid
            if (state.IsOneshot && currentInstance.isValid())
            {
                currentInstance.release();
                state.InstanceHandle = 0;
            }

            if (!currentInstance.isValid() || state.InstanceHandle == 0)
            {
                var eventRef = new FMODUnity.EventReference { Guid = emitter.EventGuid };
                var desc = FMODUnity.RuntimeManager.GetEventDescription(eventRef);

                if (!desc.isValid())
                    return;

                desc.createInstance(out EventInstance newInstance);
                state.InstanceHandle = (ulong)(long)newInstance.handle;

                // Set 3D attributes
                desc.is3D(out bool is3D);
                if (is3D)
                {
                    var attributes = FMODDotsUtility.To3DAttributes(transform, default);
                    newInstance.set3DAttributes(attributes);
                    _previousPositions[entity] = transform.Position;

                    if (emitter.OverrideAttenuation)
                    {
                        newInstance.setProperty(EVENT_PROPERTY.MINIMUM_DISTANCE, emitter.OverrideMinDistance);
                        newInstance.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, emitter.OverrideMaxDistance);
                    }
                }

                // Apply parameters from buffer if present
                if (SystemAPI.HasBuffer<FMODEmitterParameterElement>(entity))
                {
                    var paramBuffer = SystemAPI.GetBuffer<FMODEmitterParameterElement>(entity);
                    for (int i = 0; i < paramBuffer.Length; i++)
                    {
                        var param = paramBuffer[i];
                        if (!param.IDCached)
                        {
                            desc.getParameterDescriptionByName(param.Name.ToString(), out PARAMETER_DESCRIPTION paramDesc);
                            param.ID = paramDesc.id;
                            param.IDCached = true;
                            paramBuffer[i] = param;
                        }
                        newInstance.setParameterByID(param.ID, param.Value);
                    }
                }

                newInstance.start();
                state.HasTriggered = true;
            }
        }

        private void StopInstance(ref FMODEmitterState state, bool allowFadeout)
        {
            var instance = new EventInstance { handle = (System.IntPtr)(long)state.InstanceHandle };
            if (instance.isValid())
            {
                instance.stop(allowFadeout
                    ? STOP_MODE.ALLOWFADEOUT
                    : STOP_MODE.IMMEDIATE);
                instance.release();
                if (!allowFadeout)
                {
                    state.InstanceHandle = 0;
                }
            }
        }

        private void ReleaseInstance(ref FMODEmitterState state, bool allowFadeout)
        {
            StopInstance(ref state, allowFadeout);
            state.IsActive = false;
        }
    }
}
