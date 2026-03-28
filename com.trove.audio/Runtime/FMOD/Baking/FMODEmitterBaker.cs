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
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            bool playOnCreated = false;
            bool playOnEnabled = false;
            bool stopOnDestroyed = false;
            bool stopOnDisabled = false;
            if (authoring.EventPlayTrigger == EmitterGameEvent.ObjectStart)
            {
                playOnCreated = true;
            }
            if (authoring.EventPlayTrigger == EmitterGameEvent.ObjectEnable)
            {
                playOnEnabled = true;
            }
            if (authoring.EventPlayTrigger == EmitterGameEvent.ObjectDestroy)
            {
                stopOnDestroyed = true;
            }
            if (authoring.EventPlayTrigger == EmitterGameEvent.ObjectDisable)
            {
                stopOnDisabled = true;
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
            });

            AddComponent(entity, new FMODEmitterState());
            AddComponent(entity, new LoadEventDescriptionRequest());
            AddComponent(entity, new FMODPlayRequest());
            AddComponent(entity, new FMODStopRequest());
            
            SetComponentEnabled<FMODPlayRequest>(entity, false);

            // Enable play request if PlayOnStart
            SetComponentEnabled<FMODPlayRequest>(entity, false);
            SetComponentEnabled<FMODStopRequest>(entity, false);

            // Add parameter buffer
            
            DynamicBuffer<FMODEmitterParameterElement> paramBuffer = AddBuffer<FMODEmitterParameterElement>(entity);
            if (authoring.Params != null)
            {
                for (int i = 0; i < authoring.Params.Length; i++)
                {
                    ParamRef paramRef = authoring.Params[i];
                    if (paramRef.Name != String.Empty)
                    {
                        Debug.Log($"Adding param with ID {paramRef.ID.data1} - {paramRef.ID.data2}");
                        paramBuffer.Add(new FMODEmitterParameterElement
                        {
                            Value = paramRef.Value,
                            IDCached = false,
                            CachedID = default,
                        });
                    }
                }
            }
        }
    }
}
