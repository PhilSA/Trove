using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;

namespace DOTSFMOD
{
    /// <summary>
    /// Authoring component for FMOD bank loaders.
    /// Banks are loaded when FMODBankLoadRequest is enabled and unloaded
    /// when FMODBankUnloadRequest is enabled on the entity.
    /// </summary>
    [AddComponentMenu("DOTSFMOD/FMOD Bank Loader")]
    public class FMODBankLoaderAuthoring : MonoBehaviour
    {
        [Tooltip("List of FMOD bank names to load.")]
        [FMODUnity.BankRef]
        public List<string> Banks = new List<string>();

        [Tooltip("Preload sample data after loading banks.")]
        public bool PreloadSamples = false;

        [Tooltip("Automatically load banks when the entity is created.")]
        public bool LoadOnStart = true;

        public class Baker : Baker<FMODBankLoaderAuthoring>
        {
            public override void Bake(FMODBankLoaderAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new FMODBankLoaderComponent
                {
                    PreloadSamples = authoring.PreloadSamples,
                    IsLoaded = false,
                });

                AddComponent(entity, new FMODBankLoadRequest());
                AddComponent(entity, new FMODBankUnloadRequest());

                SetComponentEnabled<FMODBankLoadRequest>(entity, authoring.LoadOnStart);
                SetComponentEnabled<FMODBankUnloadRequest>(entity, false);

                var bankBuffer = AddBuffer<FMODBankReferenceElement>(entity);
                if (authoring.Banks != null)
                {
                    for (int i = 0; i < authoring.Banks.Count; i++)
                    {
                        bankBuffer.Add(new FMODBankReferenceElement
                        {
                            BankName = new FixedString512Bytes(authoring.Banks[i]),
                        });
                    }
                }
            }
        }
    }
}
