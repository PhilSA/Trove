using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public struct EventsTest : IComponentData
{
    public Entity CubePrefab;
    public Entity CubeInstance;

    public bool EnableStressTestEventsTest;
    public int TransformEventsJobThreads;
    public int TransformEventsCount;
    public int ColorEventsJobThreads;
    public int ColorEventsCount;

    public bool EnableManagedEventsTest;

    public bool EnableEntityManagerEventsTest;

    public bool EnableEntityEventsTest;
}

public class EventsTestAuthoring : MonoBehaviour
{
    public EventsTest EventsTest;
    public GameObject CubePrefab;

    class Baker : Baker<EventsTestAuthoring>
    {
        public override void Bake(EventsTestAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            authoring.EventsTest.CubePrefab = GetEntity(authoring.CubePrefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, authoring.EventsTest);
        }
    }
}