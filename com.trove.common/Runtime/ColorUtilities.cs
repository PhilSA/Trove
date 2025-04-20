using Unity.Entities;
using Unity.Mathematics;

public static class ColorUtilities
{
    public static UnityEngine.Color ToColor(this float4 vec)
    {
        return new UnityEngine.Color(vec.x, vec.y, vec.z, vec.w);
    }

    public static float4 ToFloat4(this UnityEngine.Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }
}
