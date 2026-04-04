using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class AudioTestsAuthoring : MonoBehaviour
{
    public GameObject ListenerPrefab;
    public float2 ListenerSpeed;
    
    public GameObject EmitterPrefab;
    public int EmitterSpawnCount;
    public float EmitterSpacing;
    public float2 EmitterSpeed;
}

class AudioTestsAuthoringBaker : Baker<AudioTestsAuthoring>
{
    public override void Bake(AudioTestsAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new AudioTests
        {
            ListenerPrefab = GetEntity(authoring.ListenerPrefab, TransformUsageFlags.Dynamic),
            ListenerSpeed = authoring.ListenerSpeed,
            
            EmitterPrefab = GetEntity(authoring.EmitterPrefab, TransformUsageFlags.Dynamic),
            EmitterSpawnCount = authoring.EmitterSpawnCount,
            EmitterSpacing = authoring.EmitterSpacing,
            EmitterSpeed = authoring.EmitterSpeed,
        });
    }
}
