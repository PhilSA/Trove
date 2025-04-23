using Unity.Entities;
using UnityEngine;



class TestDebugDrawAuthoring : MonoBehaviour
{
    public TestDebugDrawShape Shape = TestDebugDrawShape.Line;
    public int DrawCount = 10000;
    public bool Update;
    public bool UseLegacyDebugLine;
    public float TimeSpeed = 1f;
    public float ColorAlphaLine = 1f;
    public float ColorAlphaTri = 1f;
}

class TestDebugDrawAuthoringBaker : Baker<TestDebugDrawAuthoring>
{
    public override void Bake(TestDebugDrawAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new TestDebugDraw
        {
            Shape = authoring.Shape,
            DrawCount = authoring.DrawCount,
            Update = authoring.Update,
            UseLegacyDebugLine = authoring.UseLegacyDebugLine,
            TimeSpeed = authoring.TimeSpeed,
            ColorAlphaLine = authoring.ColorAlphaLine,
            ColorAlphaTri = authoring.ColorAlphaTri,
        });
    }
}
