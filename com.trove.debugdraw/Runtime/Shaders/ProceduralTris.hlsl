#ifndef PROCEDURAL_TRIANGLES
#define PROCEDURAL_TRIANGLES

#ifdef UNITY_DOTS_INSTANCING_UNIFORM_BUFFER

CBUFFER_START(_TrianglePositions)
    float4 TrianglePositions[1024];
CBUFFER_END
CBUFFER_START(_TriangleColors)
    float4 TriangleColors[1024];
CBUFFER_END

void GetData_float(int VertexID, out float3 Position, out float4 Color)
{
    Position = TrianglePositions[VertexID].xyz;
    Color = TriangleColors[VertexID].xyz;
}

#else

StructuredBuffer<float4> _TrianglePositions;
StructuredBuffer<float4> _TriangleColors;

void GetData_float(int VertexID, out float3 Position, out float4 Color)
{
    Position = _TrianglePositions[VertexID].xyz;
    Color = _TriangleColors[VertexID];
}

#endif

#endif