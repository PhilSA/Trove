using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

[MaterialProperty("_EmissionColor")]
public struct MaterialPropertyEmissionColor : IComponentData
{
    public float4 Value;
}

[UnityEngine.DisallowMultipleComponent]
public class MaterialPropertyEmissionColorAuthoring : UnityEngine.MonoBehaviour
{
    [Unity.Entities.RegisterBinding(typeof(MaterialPropertyEmissionColor), nameof(MaterialPropertyEmissionColor.Value))]
    [ColorUsage(true, true)]
    public UnityEngine.Color color = Color.black;

    class Baker : Unity.Entities.Baker<MaterialPropertyEmissionColorAuthoring>
    {
        public override void Bake(MaterialPropertyEmissionColorAuthoring authoring)
        {
            MaterialPropertyEmissionColor component = default(MaterialPropertyEmissionColor);
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