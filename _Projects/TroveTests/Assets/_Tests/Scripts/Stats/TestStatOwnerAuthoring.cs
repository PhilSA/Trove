using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Trove.Stats;
using System.Runtime.CompilerServices;
using Unity.Collections;


class TestStatOwnerAuthoring : MonoBehaviour
{
    public float StatA = 10f;
    public float StatB = 10f;
    public float StatC = 10f;

    class Baker : Baker<TestStatOwnerAuthoring>
    {
        public override void Bake(TestStatOwnerAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            
            StatsUtilities.BakeStatsComponents(this, entity, out StatsBaker<TestStatModifier, TestStatModifier.Stack> statsBaker);
            
            TestStatOwner testStatOwner = new TestStatOwner();
            statsBaker.CreateStat(authoring.StatA, true, out testStatOwner.StatA);
            statsBaker.CreateStat(authoring.StatB, true, out testStatOwner.StatB);
            statsBaker.CreateStat(authoring.StatC, true, out testStatOwner.StatC);
            AddComponent(entity, testStatOwner);
        }
    }
}