using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Trove.Stats;
using System.Runtime.CompilerServices;
using Unity.Collections;


class TestStatOwnerAuthoring : MonoBehaviour
{
    public float StatA = 1f;
    public float StatB = 1f;
    public float StatC = 1f;

    class Baker : Baker<TestStatOwnerAuthoring>
    {
        public override void Bake(TestStatOwnerAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new TestStatOwner
            {
                StatA = StatHandle.CreateUnititialized(authoring.StatA),
                StatB = StatHandle.CreateUnititialized(authoring.StatB),
                StatC = StatHandle.CreateUnititialized(authoring.StatC),
            });
        }
    }
}