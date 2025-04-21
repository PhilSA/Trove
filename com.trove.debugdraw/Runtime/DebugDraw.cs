using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Trove.DebugDraw
{
    public struct DebugDrawGroup
    {
        internal NativeReference<bool> IsDirty;
        internal NativeList<float4> LinePositions;
        internal NativeList<float4> LineColors;
        
        public bool IsCreated => LinePositions.IsCreated;

        public void AddLine(float3 start, float3 end, UnityEngine.Color color)
        {
            IsDirty.Value = true; 
            
            LinePositions.Add(new float4(start, 0f));
            LinePositions.Add(new float4(end, 0f));
            
            float4 colorFloat = color.ToFloat4();
            LineColors.Add(colorFloat);
            LineColors.Add(colorFloat);
        }

        public void AddLine(float3 start, float3 end, UnityEngine.Color colorStart, UnityEngine.Color colorEnd)
        {
            IsDirty.Value = true; 
            
            LinePositions.Add(new float4(start,0f));
            LinePositions.Add(new float4(end, 0f));
            
            
            LineColors.Add(colorStart.ToFloat4());
            LineColors.Add(colorEnd.ToFloat4());
        }

        public void Clear()
        {
            IsDirty.Value = true; 
            
            LinePositions.Clear();
            LineColors.Clear();
        }

        public void Dispose(JobHandle dep = default)
        {
            if (LinePositions.IsCreated)
            {
                LinePositions.Dispose(dep);
            }

            if (LineColors.IsCreated)
            {
                LineColors.Dispose(dep);
            }
        }
    }
}
