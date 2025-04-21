#ifndef PROCEDURAL_LINES
#define PROCEDURAL_LINES

#ifdef UNITY_DOTS_INSTANCING_UNIFORM_BUFFER

CBUFFER_START(_LinePositions)
    float4 LinePositions[1024];
CBUFFER_END
CBUFFER_START(_LineColors)
    float4 LineColors[1024];
CBUFFER_END

void GetData_float(int VertexID, out float3 Position, out float4 Color)
{
    Position = LinePositions[VertexID].xyz;
    Color = LineColors[VertexID].xyz;
}

#else

StructuredBuffer<float4> _LinePositions;
StructuredBuffer<float4> _LineColors;

void GetData_float(int VertexID, out float3 Position, out float4 Color)
{
    Position = _LinePositions[VertexID].xyz;
    Color = _LineColors[VertexID];
}

#endif

#endif