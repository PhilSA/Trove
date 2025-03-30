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
            
            StatsBaker<TestStatModifier, TestStatModifier.Stack> statsBaker = 
                new StatsBaker<TestStatModifier, TestStatModifier.Stack>(this, entity);
            statsBaker.AddComponents();
            TestStatOwner testStatOwner = new TestStatOwner();
            
            statsBaker.CreateStat(authoring.StatA, true, out testStatOwner.StatA);
            statsBaker.CreateStat(authoring.StatB, true, out testStatOwner.StatB);
            statsBaker.CreateStat(authoring.StatC, true, out testStatOwner.StatC);
            
            AddComponent(entity, testStatOwner);
        }
    }
}