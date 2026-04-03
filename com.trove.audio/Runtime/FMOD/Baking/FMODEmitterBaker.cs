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

            bool playOnCreated = false;
            bool playOnEnabled = false;
            bool stopOnDestroyed = false;
            bool stopOnDisabled = false;
            switch (authoring.EventPlayTrigger)
            {
                case EmitterGameEvent.ObjectStart:
                    playOnCreated = true;
                    break;
                case EmitterGameEvent.ObjectEnable:
                    playOnEnabled = true;
                    break;
                case EmitterGameEvent.ObjectDestroy:
                    stopOnDestroyed = true;
                    break;
                case EmitterGameEvent.ObjectDisable:
                    stopOnDisabled = true;
                    break;
                default:
                    UnityEngine.Debug.LogError($"Event Play Trigger {authoring.EventPlayTrigger} is not supported in ECS");
                    break;
            }
            
            AddComponent(entity, new FMODEventEmitter
            {
                EventGUID = authoring.EventReference.Guid,

                PlayOnCreated = playOnCreated,
                PlayOnEnabled = playOnEnabled,
                StopOnDestroyed = stopOnDestroyed,
                StopOnDisabled = stopOnDisabled,

                OverrideAttenuation = authoring.OverrideAttenuation,
                OverrideMinDistance = authoring.OverrideMinDistance,
                OverrideMaxDistance = authoring.OverrideMaxDistance,

                Preload = authoring.Preload,
                AllowFadeout = authoring.AllowFadeout,
                TriggerOnce = authoring.TriggerOnce,
                NonRigidbodyVelocity = authoring.NonRigidbodyVelocity,
            });

            AddComponent(entity, new IsActiveEmitter());
            AddComponent(entity, new LoadEventDescriptionRequest());
            AddComponent(entity, new FMODEmitterPlayStateUpdate());
            
            SetComponentEnabled<IsActiveEmitter>(entity, false);
            SetComponentEnabled<LoadEventDescriptionRequest>(entity, false);
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
