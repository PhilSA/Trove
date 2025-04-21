using Trove.DebugDraw;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

public struct TestDebugDraw : IComponentData
{
    public int LinesCount;
}

partial struct TestDebugDrawSystem : ISystem
{
    private DebugDrawGroup _debugDrawGroup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DebugDrawSingleton>();
        state.RequireForUpdate<TestDebugDraw>();
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ref TestDebugDraw testDebugDraw = ref SystemAPI.GetSingletonRW<TestDebugDraw>().ValueRW;
        
        if (!_debugDrawGroup.IsCreated)
        {
            ref DebugDrawSingleton debugDrawSingleton = ref SystemAPI.GetSingletonRW<DebugDrawSingleton>().ValueRW;
            _debugDrawGroup = debugDrawSingleton.AllocateDebugDrawGroup();

            int resolution = (int)math.ceil(math.pow(testDebugDraw.LinesCount, 1f/3f));
            float spacing = 2f;
            for (int i = 0; i < testDebugDraw.LinesCount; i++)
            {
                float xStart = (i % resolution) * spacing;
                float zStart = ((i / resolution) % resolution) * spacing;
                float yStart = (i / (resolution * resolution)) * spacing;
                float3 start = new float3(xStart, yStart, zStart);
                
                UnityEngine.Color tmpColor = UnityEngine.Color.HSVToRGB((((i % 20f) / 20f) + elapsedTime) % 1f, 1f, 1f);
                _debugDrawGroup.AddLine(start, start + math.up(), tmpColor);
            }
        }
    }
}
