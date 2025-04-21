using Unity.Entities;
using UnityEngine;

class TestDebugDrawAuthoring : MonoBehaviour
{
    public int LinesCount = 10000;
    public int TrianglesCount = 10000;
    public bool Update;
    public bool UseLegacyDebugLine;
}

class TestDebugDrawAuthoringBaker : Baker<TestDebugDrawAuthoring>
{
    public override void Bake(TestDebugDrawAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new TestDebugDraw
        {
            LinesCount = authoring.LinesCount,
            TrianglesCount = authoring.TrianglesCount,
            Update = authoring.Update,
            UseLegacyDebugLine = authoring.UseLegacyDebugLine
        });
    }
}
