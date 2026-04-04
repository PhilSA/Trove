using System;
using System.Collections.Generic;
using FMODUnity;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Trove.Audio.FMOD
{
    public class StudioEventEmitterBaker : Baker<StudioEventEmitter>
    {
        public override void Bake(StudioEventEmitter authoring)
        {
            if (authoring.gameObject.GetComponents<StudioEventEmitter>().Length > 1)
            {
                UnityEngine.Debug.LogError("Cannot have more than one StudioEventEmitter component on the same GameObject");
                return;
            }
            
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            if(authoring.EventPlayTrigger != EmitterGameEvent.None)
            {
                UnityEngine.Debug.LogError($"Play trigger events are not supported in ECS. Use the FMODEmitterPlayPropertiesAuthoring component instead");
            }
            if(authoring.EventStopTrigger != EmitterGameEvent.None)
            {
                UnityEngine.Debug.LogError($"Stop trigger events are not supported in ECS. Use the FMODEmitterPlayPropertiesAuthoring component instead");
            }
            
            AddComponent(entity, new FMODEventEmitter
            {
                EventGUID = authoring.EventReference.Guid,

                OverrideAttenuation = authoring.OverrideAttenuation,
                OverrideMinDistance = authoring.OverrideMinDistance,
                OverrideMaxDistance = authoring.OverrideMaxDistance,

                Preload = authoring.Preload,
                AllowFadeout = authoring.AllowFadeout,
                TriggerOnce = authoring.TriggerOnce,
                NonRigidbodyVelocity = authoring.NonRigidbodyVelocity,
            });

            AddComponent(entity, new IsEnabledEmitter());
            AddComponent(entity, new IsActiveEmitterToStopOutsideOfMaxDistance());
            AddComponent(entity, new FMODEmitterPlayStateUpdate());
            
            SetComponentEnabled<IsEnabledEmitter>(entity, true);
            SetComponentEnabled<IsActiveEmitterToStopOutsideOfMaxDistance>(entity, false);
            SetComponentEnabled<FMODEmitterPlayStateUpdate>(entity, false);

            // Add parameter buffer
            DynamicBuffer<FMODEventParameter> paramBuffer = AddBuffer<FMODEventParameter>(entity);
            if (authoring.Params != null)
            {
                for (int i = 0; i < authoring.Params.Length; i++)
                {
                    ParamRef paramRef = authoring.Params[i];
                    if (paramRef.Name != String.Empty)
                    {
                        paramBuffer.Add(new FMODEventParameter
                        {
                            Name = paramRef.Name,
                            Value = paramRef.Value,
                            ID = default,
                        });
                    }
                }
            }
        }
    }
}
