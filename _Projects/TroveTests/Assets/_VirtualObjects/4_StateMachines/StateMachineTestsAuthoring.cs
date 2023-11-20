using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct StateMachineTests : IComponentData
{
    public int StateMachinesCount;
    public Entity StateMachinePrefab;

}

public class StateMachineTestsAuthoring : MonoBehaviour
{
    public StateMachineTests Params;
    public GameObject StateMachinePrefab;

    class Baker : Baker<StateMachineTestsAuthoring>
    {
        public override void Bake(StateMachineTestsAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            authoring.Params.StateMachinePrefab = GetEntity(authoring.StateMachinePrefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, authoring.Params);
        }
    }
}
