using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class TestBaker : MonoBehaviour
{
    class Baker : Baker<TestBaker>
    {
        public override void Bake(TestBaker authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent<URPMaterialPropertyBaseColor>(entity);
        }
    }
}
