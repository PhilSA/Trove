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
            statsBaker.CreateStat(authoring.StatA, false, out testStatOwner.StatA);
            statsBaker.CreateStat(authoring.StatB, false, out testStatOwner.StatB);
            statsBaker.CreateStat(authoring.StatC, false, out testStatOwner.StatC);

            // bool success = false;
            // success = statsBaker.TryAddStatModifier(testStatOwner.StatA, new TestStatModifier
            // {
            //     ModifierType = TestStatModifier.Type.AddFromStat,
            //     StatHandleA = testStatOwner.StatB,
            // }, out _);
            // UnityEngine.Debug.Log($"Add mod 1: {success}");
            //
            // success = statsBaker.TryAddStatModifier(testStatOwner.StatB, new TestStatModifier
            // {
            //     ModifierType = TestStatModifier.Type.AddFromStat,
            //     StatHandleA = testStatOwner.StatC,
            // }, out _);
            // UnityEngine.Debug.Log($"Add mod 2: {success}");
            //
            // success = statsBaker.TryAddStatModifier(testStatOwner.StatB, new TestStatModifier
            // {
            //     ModifierType = TestStatModifier.Type.AddFromStat,
            //     StatHandleA = testStatOwner.StatC,
            // }, out _);
            //
            // success = statsBaker.TryAddStatModifier(testStatOwner.StatC, new TestStatModifier
            // {
            //     ModifierType = TestStatModifier.Type.AddFromStat,
            //     StatHandleA = testStatOwner.StatA,
            // }, out _);
            // UnityEngine.Debug.Log($"Add mod 3: {success}");
            //
            // Entity otherEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic, true);
            // StatHandle otherEntityStatHandle = new StatHandle(otherEntity, 0);
            //
            // success = statsBaker.TryAddStatModifier(otherEntityStatHandle, new TestStatModifier
            // {
            //     ModifierType = TestStatModifier.Type.AddFromStat,
            //     StatHandleA = testStatOwner.StatC,
            // }, out _);
            // UnityEngine.Debug.Log($"Add mod 4: {success}");
            //
            // success = statsBaker.TryAddStatModifier(testStatOwner.StatA, new TestStatModifier
            // {
            //     ModifierType = TestStatModifier.Type.AddFromStat,
            //     StatHandleA = otherEntityStatHandle,
            // }, out _);
            // UnityEngine.Debug.Log($"Add mod 5: {success}");
            
            AddComponent(entity, testStatOwner);
        }
    }
}