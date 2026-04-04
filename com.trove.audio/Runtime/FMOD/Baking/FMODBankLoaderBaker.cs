using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;

namespace Trove.Audio.FMOD
{
    public class FMODBankLoaderAuthoring : MonoBehaviour
    {
        [FMODUnity.BankRef]
        public List<string> Banks = new List<string>();
        public bool PreloadSamples = false;
        public bool LoadOnStart = true;

        public class Baker : Baker<FMODBankLoaderAuthoring>
        {
            public override void Bake(FMODBankLoaderAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new FMODBankLoaderComponent
                {
                    PreloadSamples = authoring.PreloadSamples,
                });
                AddComponent(entity, new FMODBankLoadRequest());
                AddComponent(entity, new FMODBankUnloadRequest());

                SetComponentEnabled<FMODBankLoadRequest>(entity, authoring.LoadOnStart);
                SetComponentEnabled<FMODBankUnloadRequest>(entity, false);

                DynamicBuffer<FMODBankElement> bankBuffer = AddBuffer<FMODBankElement>(entity);
                if (authoring.Banks != null)
                {
                    for (int i = 0; i < authoring.Banks.Count; i++)
                    {
                        bankBuffer.Add(new FMODBankElement
                        {
                            BankName = new FixedString512Bytes(authoring.Banks[i]),
                        });
                    }
                }
            }
        }
    }
}
