using Unity.Entities;
using Unity.Mathematics;

public struct MemoryVisualizer : IComponentData
{
    public Entity ColorCubePrefab;
    public float2 XMinMax;

    public float4 DefaultColor;
    public float4 StaticDataColor;
    public float4 UnusedMetadataColor;
    public float4 UsedMetadataColor;
    public float4 UnusedDataColor;
    public float4 UsedDataColor;
    public float4 DataFreeRangeColor;
    public float4 MetadataFreeRangeColor;

    public bool Update;
    public Entity TestEntity;
}

public struct TestVirtualObjectElement : IBufferElementData
{
    public byte Data;
}