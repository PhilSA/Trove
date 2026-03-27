using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace DOTSFMOD
{
    [AddComponentMenu("DOTSFMOD/FMODDOTSEmitter")]
    public class FMODEmitterAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct FMODParameterEntry
        {
            public string EventPath;
            public string Name;
            public float Value;
        }
        
        public FMODUnity.EventReference EventReference;
        
        [Header("Play/Stop options")]
        public bool PlayOnCreated = true;
        public bool PlayOnEnabled = true;
        public bool StopOnDestroyed = true;
        public bool StopOnDisabled = true;
        
        [Header("Override Attenuation")]
        public bool OverrideAttenuation = false;
        public float OverrideMinDistance = 1f;
        public float OverrideMaxDistance = 20f;
        
        [Header("Advanced controls")]
        public bool Preload = false;
        public bool AllowFadeout = true;
        public bool TriggerOnce = false;

        public List<FMODParameterEntry> Parameters = new List<FMODParameterEntry>();

        [SerializeField]
        [HideInInspector]
        public FMODUnity.EventReference _prevEventReference = default;

        public class Baker : Baker<FMODEmitterAuthoring>
        {
            public override void Bake(FMODEmitterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new FMODEmitterComponent
                {
                    EventGuid = authoring.EventReference.Guid,
                    
                    PlayOnEnabled = authoring.PlayOnEnabled,
                    StopOnDestroyed = authoring.StopOnDestroyed,
                    StopOnDisabled = authoring.StopOnDisabled,
                    
                    OverrideAttenuation = authoring.OverrideAttenuation,
                    OverrideMinDistance = authoring.OverrideMinDistance,
                    OverrideMaxDistance = authoring.OverrideMaxDistance,
                    
                    Preload = authoring.Preload,
                    AllowFadeout = authoring.AllowFadeout,
                    TriggerOnce = authoring.TriggerOnce,
                });

                AddComponent(entity, new FMODEmitterState());
                AddComponent(entity, new FMODPlayRequest());
                AddComponent(entity, new FMODStopRequest());

                // Enable play request if PlayOnStart
                SetComponentEnabled<FMODPlayRequest>(entity, authoring.PlayOnCreated);
                SetComponentEnabled<FMODStopRequest>(entity, false);

                // Add parameter buffer
                DynamicBuffer<FMODEmitterParameterElement> paramBuffer = AddBuffer<FMODEmitterParameterElement>(entity);
                if (authoring.Parameters != null)
                {
                    for (int i = 0; i < authoring.Parameters.Count; i++)
                    {
                        if (authoring.Parameters[i].Name != String.Empty)
                        {
                            paramBuffer.Add(new FMODEmitterParameterElement
                            {
                                Name = new FixedString128Bytes(authoring.Parameters[i].Name),
                                Value = authoring.Parameters[i].Value,
                                IDCached = false,
                            });
                        }
                    }
                }
            }
        }

        public void OnValidate()
        {
            if (EventReference.Guid != _prevEventReference.Guid)
            {
                Parameters.Clear();
            }

            bool removedAny = false;
            for (int i = 0; i < Parameters.Count; i++)
            {
                // Update event paths
                FMODParameterEntry param = Parameters[i];
                param.EventPath = EventReference.Path;
                Parameters[i] = param;

                // Remove duplicates
                if (param.Name != string.Empty)
                {
                    for (int j = Parameters.Count - 1; j > i; j--)
                    {
                        FMODParameterEntry otherParam = Parameters[j];
                        if (otherParam.Name == param.Name)
                        {
                            otherParam.Name = string.Empty;
                            otherParam.Value = default;
                            Parameters[j] = otherParam;
                            removedAny = true;
                            Debug.Log($"removed at position {j}");
                        }
                    }
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            if (removedAny)
            {
                Editor[] activeEditors = UnityEditor.ActiveEditorTracker.sharedTracker.activeEditors;
                for (int i = 0; i < activeEditors.Length; i++)
                {
                    activeEditors[i].Repaint();
                    //Debug.Log($"repaint editor {activeEditors[i].}");
                }
            }
#endif

            _prevEventReference = EventReference;
        }
        
        public void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "AudioSource Gizmo", true, Color.yellow);
        }
    }
}
