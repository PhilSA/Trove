using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class MemoryVisualizerAuthoring : MonoBehaviour
{
    public GameObject ColorCubePrefab;
    public float2 XMinMax;

    public Color DefaultColor;
    public Color StaticDataColor;
    public Color UnusedMetadataColor;
    public Color UsedMetadataColor;
    public Color UnusedDataColor;
    public Color UsedDataColor;
    public Color DataFreeRangeColor;
    public Color MetadataFreeRangeColor;
}

class MemoryVisualizerAuthoringBaker : Baker<MemoryVisualizerAuthoring>
{
    public override void Bake(MemoryVisualizerAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.None);
        AddComponent(entity, new MemoryVisualizer
        {
            ColorCubePrefab = GetEntity(authoring.ColorCubePrefab, TransformUsageFlags.Dynamic),
            XMinMax = authoring.XMinMax,

            DefaultColor = ColorToFloat4(authoring.DefaultColor),
            StaticDataColor = ColorToFloat4(authoring.StaticDataColor),
            UnusedMetadataColor = ColorToFloat4(authoring.UnusedMetadataColor),
            UsedMetadataColor = ColorToFloat4(authoring.UsedMetadataColor),
            UnusedDataColor = ColorToFloat4(authoring.UnusedDataColor),
            UsedDataColor = ColorToFloat4(authoring.UsedDataColor),
            DataFreeRangeColor = ColorToFloat4(authoring.DataFreeRangeColor),
            MetadataFreeRangeColor = ColorToFloat4(authoring.MetadataFreeRangeColor),

            Update = false,
        });
    }

    private float4 ColorToFloat4(Color color)
    {
        return (float4)(Vector4)color;
    }
}
