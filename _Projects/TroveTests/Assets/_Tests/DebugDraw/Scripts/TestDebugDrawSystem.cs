using Trove.DebugDraw;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

public struct TestDebugDraw : IComponentData
{
    public int LinesCount;
    public int TrianglesCount;
    public bool Update;
    public bool UseLegacyDebugLine;
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
            
            float spacing = 2f;

            int linesResolution = (int)math.ceil(math.pow(testDebugDraw.LinesCount, 1f/3f));
            for (int i = 0; i < testDebugDraw.LinesCount; i++)
            {
                float xStart = (i % linesResolution) * spacing;
                float zStart = ((i / linesResolution) % linesResolution) * spacing;
                float yStart = (i / (linesResolution * linesResolution)) * spacing;
                float3 start = new float3(xStart, yStart, zStart);
                
                UnityEngine.Color tmpColor = UnityEngine.Color.HSVToRGB((((i % 20f) / 20f)) % 1f, 1f, 1f);
                _debugDrawGroup.AddLine(start, start + math.up(), tmpColor);
            }

            int trisResolution = (int)math.ceil(math.pow(testDebugDraw.TrianglesCount, 1f/3f));
            for (int i = 0; i < testDebugDraw.TrianglesCount; i++)
            {
                float xStart = (i % trisResolution) * spacing;
                float zStart = ((i / trisResolution) % trisResolution) * spacing;
                float yStart = (i / (trisResolution * trisResolution)) * spacing;
                float3 start = new float3(-xStart, -yStart, -zStart);
                
                UnityEngine.Color tmpColor = UnityEngine.Color.HSVToRGB((((i % 20f) / 20f)) % 1f, 1f, 1f);
                _debugDrawGroup.AddTriangle(start, start + math.up(), start + math.right(), tmpColor);
            }
        }

        if (testDebugDraw.Update)
        {
            _debugDrawGroup.Clear();
            
            float spacing = 2f;
            
            int linesResolution = (int)math.ceil(math.pow(testDebugDraw.LinesCount, 1f/3f));
            if (testDebugDraw.UseLegacyDebugLine)
            {
                for (int i = 0; i < testDebugDraw.LinesCount; i++)
                {
                    float xStart = (i % linesResolution) * spacing;
                    float zStart = ((i / linesResolution) % linesResolution) * spacing;
                    float yStart = (i / (linesResolution * linesResolution)) * spacing;
                    float3 start = new float3(xStart, yStart, zStart) + elapsedTime;
                
                    UnityEngine.Color tmpColor = UnityEngine.Color.HSVToRGB((((i % 20f) / 20f) + elapsedTime) % 1f, 1f, 1f);
                    UnityEngine.Debug.DrawLine(start, start + math.up(), tmpColor);
                }
            }
            else
            {
                for (int i = 0; i < testDebugDraw.LinesCount; i++)
                {
                    float xStart = (i % linesResolution) * spacing;
                    float zStart = ((i / linesResolution) % linesResolution) * spacing;
                    float yStart = (i / (linesResolution * linesResolution)) * spacing;
                    float3 start = new float3(xStart, yStart, zStart) + elapsedTime;
                
                    UnityEngine.Color tmpColor = UnityEngine.Color.HSVToRGB((((i % 20f) / 20f) + elapsedTime) % 1f, 1f, 1f);
                    _debugDrawGroup.AddLine(start, start + math.up(), tmpColor);
                }
            }

            int trisResolution = (int)math.ceil(math.pow(testDebugDraw.TrianglesCount, 1f/3f));
            for (int i = 0; i < testDebugDraw.TrianglesCount; i++)
            {
                float xStart = (i % trisResolution) * spacing;
                float zStart = ((i / trisResolution) % trisResolution) * spacing;
                float yStart = (i / (trisResolution * trisResolution)) * spacing;
                float3 start = new float3(-xStart, -yStart, -zStart) + elapsedTime;
                
                UnityEngine.Color tmpColor = UnityEngine.Color.HSVToRGB((((i % 20f) / 20f) + elapsedTime) % 1f, 1f, 1f);
                _debugDrawGroup.AddTriangle(start, start + math.up(), start + math.right(), tmpColor);
            }
        }
    }
}
