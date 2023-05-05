using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RefVSLookupTesterAuthoring : MonoBehaviour
{
    public int HowMany = 10000;

    class Baker : Baker<RefVSLookupTesterAuthoring>
    {
        public override void Bake(RefVSLookupTesterAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new RefVSLookupTester
            {
                HowMany = authoring.HowMany,
            });
        }
    }
}