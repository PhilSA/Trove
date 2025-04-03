using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

[MaterialProperty("_BaseColor")]
public struct MaterialPropertyBaseColor : IComponentData
{
    public float4 Value;
}

[UnityEngine.DisallowMultipleComponent]
public class MaterialPropertyBaseColorAuthoring : UnityEngine.MonoBehaviour
{
    [Unity.Entities.RegisterBinding(typeof(MaterialPropertyBaseColor), nameof(MaterialPropertyBaseColor.Value))]
    [ColorUsage(true, false)]
    public UnityEngine.Color color = Color.black;

    class Baker : Unity.Entities.Baker<MaterialPropertyBaseColorAuthoring>
    {
        public override void Bake(MaterialPropertyBaseColorAuthoring authoring)
        {
            MaterialPropertyBaseColor component = default(MaterialPropertyBaseColor);
            float4 colorValues;
            colorValues.x = authoring.color.r;
            colorValues.y = authoring.color.g;
            colorValues.z = authoring.color.b;
            colorValues.w = authoring.color.a;
            component.Value = colorValues;
            AddComponent(GetEntity(TransformUsageFlags.None), component);
        }
    }
}