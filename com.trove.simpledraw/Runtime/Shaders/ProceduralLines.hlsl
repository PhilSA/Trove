#ifndef PROCEDURAL_CUSTOM
#define PROCEDURAL_CUSTOM

#ifdef UNITY_DOTS_INSTANCING_UNIFORM_BUFFER

CBUFFER_START(_Positions)
    float4 Positions[1024];
CBUFFER_END

void ProceduralCustom_float(int VertexID, int BaseIndex, out float3 Position)
{
    Position = Positions[VertexID + BaseIndex].xyz;
}

#else

StructuredBuffer<float4> _Positions;

void ProceduralCustom_float(int VertexID, int BaseIndex, out float3 Position)
{
    Position = _Positions[VertexID + BaseIndex].xyz;
}

#endif

#endif