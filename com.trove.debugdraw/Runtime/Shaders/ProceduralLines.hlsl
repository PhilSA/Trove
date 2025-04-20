#ifndef PROCEDURAL_CUSTOM
#define PROCEDURAL_CUSTOM

#ifdef UNITY_DOTS_INSTANCING_UNIFORM_BUFFER

CBUFFER_START(_Positions)
    float4 Positions[1024];
CBUFFER_END
CBUFFER_START(_Colors)
    float4 Colors[1024];
CBUFFER_END

void ProceduralCustom_float(int VertexID, int BaseIndex, out float3 Position, out float4 Color)
{
    Position = Positions[VertexID + BaseIndex].xyz;
    Color = Colors[VertexID + BaseIndex].xyz;
}

#else

StructuredBuffer<float4> _Positions;
StructuredBuffer<float4> _Colors;

void ProceduralCustom_float(int VertexID, int BaseIndex, out float3 Position, out float4 Color)
{
    Position = _Positions[VertexID + BaseIndex].xyz;
    Color = _Colors[VertexID + BaseIndex];
}

#endif

#endif