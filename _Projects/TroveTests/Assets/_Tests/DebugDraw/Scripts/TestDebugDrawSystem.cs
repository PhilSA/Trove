using System;
using Trove.DebugDraw;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

public enum TestDebugDrawShape
{
    Line,
    
    Triangle,
    WireTriangle,
    WireMeshTriangle,
    
    Quad,
    WireQuad,
    WireMeshQuad,
    
    Box,
    WireBox,
    WireMeshBox,
    
    WireSphere,
    WireCapsule,
}

public struct TestDebugDraw : IComponentData
{
    public TestDebugDrawShape Shape;
    public int DrawCount;
    public bool Update;
    public bool UseLegacyDebugLine;
    public float TimeSpeed;
    public float ColorAlphaLine;
    public float ColorAlphaTri;
}

partial struct TestDebugDrawSystem : ISystem
{
    private DebugDrawGroup _debugDrawGroup;
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DebugDrawSingleton>();
        state.RequireForUpdate<TestDebugDraw>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ref TestDebugDraw testDebugDraw = ref SystemAPI.GetSingletonRW<TestDebugDraw>().ValueRW;
        
        elapsedTime *= testDebugDraw.TimeSpeed;
        
        if (!_debugDrawGroup.IsCreated)
        {
            ref DebugDrawSingleton debugDrawSingleton = ref SystemAPI.GetSingletonRW<DebugDrawSingleton>().ValueRW;
            _debugDrawGroup = debugDrawSingleton.AllocateDebugDrawGroup();
            
            Draw(ref testDebugDraw, elapsedTime);
        }

        if (testDebugDraw.Update)
        {
            Draw(ref testDebugDraw, elapsedTime);
        }
    }

    private void Draw(ref TestDebugDraw testDebugDraw, float elapsedTime)
    {
        _debugDrawGroup.Clear();

        float spacing = 2f;
        int elementResolution = (int)math.ceil(math.pow(testDebugDraw.DrawCount, 1f / 3f));
        
        for (int i = 0; i < testDebugDraw.DrawCount; i++)
        {
            float xStart = (i % elementResolution) * spacing;
            float zStart = ((i / elementResolution) % elementResolution) * spacing;
            float yStart = (i / (elementResolution * elementResolution)) * spacing;
            float3 start = new float3(xStart, yStart, zStart) + elapsedTime;

            UnityEngine.Color tmpColorMesh = UnityEngine.Color.HSVToRGB((((i % 20f) / 20f) + elapsedTime) % 1f, 1f, 1f);
            UnityEngine.Color tmpColorLine = tmpColorMesh;
            tmpColorMesh.a = testDebugDraw.ColorAlphaTri;
            tmpColorLine.a = testDebugDraw.ColorAlphaLine;
            
            switch (testDebugDraw.Shape)
            {
                case TestDebugDrawShape.Line:
                    if (testDebugDraw.UseLegacyDebugLine)
                    {
                        UnityEngine.Debug.DrawLine(start, start + math.up(), tmpColorLine);
                    }
                    else
                    {
                        _debugDrawGroup.DrawLine(start, start + math.up(), tmpColorLine);
                    }
                    break;
                case TestDebugDrawShape.Triangle:
                    _debugDrawGroup.DrawTriangle(start, start + math.up(), start + math.right(), tmpColorMesh);
                    break;
                case TestDebugDrawShape.WireTriangle:
                    _debugDrawGroup.DrawWireTriangle(start, start + math.up(), start + math.right(), tmpColorLine);
                    break;
                case TestDebugDrawShape.WireMeshTriangle:
                    _debugDrawGroup.DrawTriangle(start, start + math.up(), start + math.right(), tmpColorMesh);
                    _debugDrawGroup.DrawWireTriangle(start, start + math.up(), start + math.right(), tmpColorLine);
                    break;
                case TestDebugDrawShape.Quad:
                    _debugDrawGroup.DrawQuad(start, quaternion.identity, 0.5f, tmpColorMesh);
                    break;
                case TestDebugDrawShape.WireQuad:
                    _debugDrawGroup.DrawWireQuad(start, quaternion.identity, 0.5f, tmpColorLine);
                    break;
                case TestDebugDrawShape.WireMeshQuad:
                    _debugDrawGroup.DrawQuad(start, quaternion.identity, 0.5f, tmpColorMesh);
                    _debugDrawGroup.DrawWireQuad(start, quaternion.identity, 0.5f, tmpColorLine);
                    break;
                case TestDebugDrawShape.Box:
                    _debugDrawGroup.DrawBox(start, quaternion.identity, 0.5f, tmpColorMesh);
                    break;
                case TestDebugDrawShape.WireBox:
                    _debugDrawGroup.DrawWireBox(start, quaternion.identity, 0.5f, tmpColorLine);
                    break;
                case TestDebugDrawShape.WireMeshBox:
                    _debugDrawGroup.DrawBox(start, quaternion.identity, 0.5f, tmpColorMesh);
                    _debugDrawGroup.DrawWireBox(start, quaternion.identity, 0.5f, tmpColorLine);
                    break;
                case TestDebugDrawShape.WireSphere:
                    _debugDrawGroup.DrawWireSphere(start, quaternion.identity, 0.5f, 2, 2, tmpColorLine);
                    break;
                case TestDebugDrawShape.WireCapsule:
                    _debugDrawGroup.DrawWireCapsule(start, quaternion.identity, 0.25f, 1f, 2, 2, tmpColorLine);
                    break;
            }
        }
    }
}
