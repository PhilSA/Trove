using Unity.Entities;
using UnityEngine;

class TestDebugDrawAuthoring : MonoBehaviour
{
    public int LinesCount = 10000;
}

class TestDebugDrawAuthoringBaker : Baker<TestDebugDrawAuthoring>
{
    public override void Bake(TestDebugDrawAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new TestDebugDraw
        {
            LinesCount = authoring.LinesCount,
        });
    }
}
