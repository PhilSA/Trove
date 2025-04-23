using System.Runtime.CompilerServices;
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

        internal NativeList<float4> TrianglePositions;
        internal NativeList<float4> TriangleColors;

        public bool IsCreated => LinePositions.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawLine(float3 start, float3 end, UnityEngine.Color color)
        {
            IsDirty.Value = true;

            LinePositions.Add(new float4(start, 0f));
            LinePositions.Add(new float4(end, 0f));

            float4 colorFloat = color.ToFloat4();
            LineColors.Add(colorFloat);
            LineColors.Add(colorFloat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawLine(float3 start, float3 end, UnityEngine.Color colorStart, UnityEngine.Color colorEnd)
        {
            IsDirty.Value = true;

            LinePositions.Add(new float4(start, 0f));
            LinePositions.Add(new float4(end, 0f));

            LineColors.Add(colorStart.ToFloat4());
            LineColors.Add(colorEnd.ToFloat4());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawRay(float3 start, float3 direction, float length, UnityEngine.Color color)
        {
            IsDirty.Value = true;

            LinePositions.Add(new float4(start, 0f));
            LinePositions.Add(new float4(start + (direction * length), 0f));

            float4 colorFloat = color.ToFloat4();
            LineColors.Add(colorFloat);
            LineColors.Add(colorFloat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawRay(float3 start, float3 direction, float length, UnityEngine.Color colorStart, UnityEngine.Color colorEnd)
        {
            IsDirty.Value = true;

            LinePositions.Add(new float4(start, 0f));
            LinePositions.Add(new float4(start + (direction * length), 0f));

            LineColors.Add(colorStart.ToFloat4());
            LineColors.Add(colorEnd.ToFloat4());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawArrow(
            float3 start, 
            float3 direction,
            float length,
            float arrowPointsLength,
            UnityEngine.Color color)
        {
            direction = math.normalizesafe(direction);
            float3 arrowEnd = start + (direction * length);
            DrawLine(start, arrowEnd, color);
            
            quaternion arrowRotation = quaternion.LookRotationSafe(direction, math.up());
            quaternion arrowPitchRotation = quaternion.AxisAngle(math.right(), math.PI / 8f);
            int arrowPoints = 4;
            for (int i = 0; i < arrowPoints; i++)
            {
                quaternion arrowRollRotation = quaternion.AxisAngle(math.forward(), i * (math.PI2 / (float)arrowPoints));
                float3 arrowPointEnd = arrowEnd - math.mul(math.mul(math.mul(arrowRotation, arrowRollRotation), arrowPitchRotation), math.forward() * arrowPointsLength);
                DrawLine(arrowEnd, arrowPointEnd, color);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawAxisSystem(
            float3 position, 
            quaternion rotation,
            float length)
        {
            // Right
            DrawArrow(position, math.mul(rotation, math.right()), length, length * 0.1f, UnityEngine.Color.red);
            // Up
            DrawArrow(position, math.mul(rotation, math.up()), length, length * 0.1f, UnityEngine.Color.green);
            // Forward
            DrawArrow(position, math.mul(rotation, math.forward()), length, length * 0.1f, UnityEngine.Color.blue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawDot(float3 position, quaternion rotation, float size, UnityEngine.Color color)
        {
            RigidTransform transform = new RigidTransform(rotation, position);

            float3 v0 = math.transform(transform, -size); // left-down-back
            float3 v1 = math.transform(transform, new float3(size, -size, -size)); // right-down-back
            float3 v2 = math.transform(transform, new float3(-size, size, -size)); // left-up-back
            float3 v3 = math.transform(transform, new float3(size, size, -size)); // right-up-back
            float3 v4 = math.transform(transform, new float3(-size, -size, size)); // left-down-front
            float3 v5 = math.transform(transform, new float3(size, -size, size)); // right-down-front
            float3 v6 = math.transform(transform, new float3(-size, size, size)); // left-up-front
            float3 v7 = math.transform(transform, size); // right-up-front

            DrawLine(v0, v7, color);
            DrawLine(v1, v6, color);
            DrawLine(v4, v3, color);
            DrawLine(v5, v2, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawTriangle(float3 v0, float3 v1, float3 v2, UnityEngine.Color color)
        {
            IsDirty.Value = true;

            TrianglePositions.Add(new float4(v0, 0f));
            TrianglePositions.Add(new float4(v1, 0f));
            TrianglePositions.Add(new float4(v2, 0f));

            float4 colorFloat = color.ToFloat4();
            TriangleColors.Add(colorFloat);
            TriangleColors.Add(colorFloat);
            TriangleColors.Add(colorFloat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawWireTriangle(float3 v0, float3 v1, float3 v2, UnityEngine.Color color)
        {
            DrawLine(v0, v1, color);
            DrawLine(v1, v2, color);
            DrawLine(v2, v0, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawQuad(float3 position, quaternion rotation, float2 extents, UnityEngine.Color color)
        {
            RigidTransform transform = new RigidTransform(rotation, position);
            float3 extents3D = new float3(extents.x, 0f, extents.y);
            
            float3 v0 = math.transform(transform, new float3(-extents.x, 0f, -extents.y)); // left-back
            float3 v1 = math.transform(transform, new float3(extents.x, 0f, -extents.y)); // right-back
            float3 v2 = math.transform(transform, new float3(-extents.x, 0f, extents.y)); // left-front
            float3 v3 = math.transform(transform, new float3(extents.x, 0f, extents.y)); // right-front
            
            // Up
            DrawTriangle(v1, v0, v2, color);
            DrawTriangle(v1, v2, v3, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawWireQuad(float3 position, quaternion rotation, float2 extents, UnityEngine.Color color)
        {
            RigidTransform transform = new RigidTransform(rotation, position);
            
            float3 v0 = math.transform(transform, new float3(-extents.x, 0f, -extents.y)); // left-back
            float3 v1 = math.transform(transform, new float3(extents.x, 0f, -extents.y)); // right-back
            float3 v2 = math.transform(transform, new float3(-extents.x, 0f, extents.y)); // left-front
            float3 v3 = math.transform(transform, new float3(extents.x, 0f, extents.y)); // right-front
            
            DrawLine(v0, v1, color);
            DrawLine(v1, v3, color);
            DrawLine(v3, v2, color);
            DrawLine(v2, v0, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawBox(float3 position, quaternion rotation, float3 extents, UnityEngine.Color color)
        {
            RigidTransform transform = new RigidTransform(rotation, position);
            
            float3 v0 = math.transform(transform, -extents); // left-down-back
            float3 v1 = math.transform(transform, new float3(extents.x, -extents.y, -extents.z)); // right-down-back
            float3 v2 = math.transform(transform, new float3(-extents.x, extents.y, -extents.z)); // left-up-back
            float3 v3 = math.transform(transform, new float3(extents.x, extents.y, -extents.z)); // right-up-back
            float3 v4 = math.transform(transform, new float3(-extents.x, -extents.y, extents.z)); // left-down-front
            float3 v5 = math.transform(transform, new float3(extents.x, -extents.y, extents.z)); // right-down-front
            float3 v6 = math.transform(transform, new float3(-extents.x, extents.y, extents.z)); // left-up-front
            float3 v7 = math.transform(transform, extents); // right-up-front
            
            // Up
            DrawTriangle(v3, v2, v6, color);
            DrawTriangle(v3, v6, v7, color);
            
            // Down
            DrawTriangle(v4, v0, v1, color);
            DrawTriangle(v5, v4, v1, color);
            
            // Front
            DrawTriangle(v7, v6, v5, color);
            DrawTriangle(v4, v5, v6, color);
            
            // Back
            DrawTriangle(v1, v2, v3, color);
            DrawTriangle(v2, v1, v0, color);
            
            // Right
            DrawTriangle(v1, v3, v7, color);
            DrawTriangle(v1, v7, v5, color);
            
            // Left
            DrawTriangle(v6, v2, v0, color);
            DrawTriangle(v4, v6, v0, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawWireBox(float3 position, quaternion rotation, float3 extents, UnityEngine.Color color)
        {
            RigidTransform transform = new RigidTransform(rotation, position);
            
            float3 v0 = math.transform(transform, -extents); // left-down-back
            float3 v1 = math.transform(transform, new float3(extents.x, -extents.y, -extents.z)); // right-down-back
            float3 v2 = math.transform(transform, new float3(-extents.x, extents.y, -extents.z)); // left-up-back
            float3 v3 = math.transform(transform, new float3(extents.x, extents.y, -extents.z)); // right-up-back
            float3 v4 = math.transform(transform, new float3(-extents.x, -extents.y, extents.z)); // left-down-front
            float3 v5 = math.transform(transform, new float3(extents.x, -extents.y, extents.z)); // right-down-front
            float3 v6 = math.transform(transform, new float3(-extents.x, extents.y, extents.z)); // left-up-front
            float3 v7 = math.transform(transform, extents); // right-up-front
            
            DrawLine(v0, v1, color);
            DrawLine(v0, v2, color);
            DrawLine(v3, v1, color);
            DrawLine(v3, v2, color);
            
            DrawLine(v7, v6, color);
            DrawLine(v7, v5, color);
            DrawLine(v4, v6, color);
            DrawLine(v4, v5, color);
            
            DrawLine(v0, v4, color);
            DrawLine(v1, v5, color);
            DrawLine(v2, v6, color);
            DrawLine(v3, v7, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawWireSphere(
            float3 center, 
            quaternion rotation,
            float radius, 
            int ringCount,
            int quarterRingSegments, 
            UnityEngine.Color color)
        {
            quarterRingSegments = math.max(quarterRingSegments, 2);
            ringCount = math.max(ringCount, 1);

            float3 localCenterToTop = math.up() * radius;
            float3 localCenterToRight = math.right() * radius;
            float3 centerToTop = math.mul(rotation, localCenterToTop);
            float3 centerToRight = math.mul(rotation, localCenterToRight);
            float3 top = center + centerToTop;
            float3 right = center + centerToRight;
            float segmentAnglesRadians = (math.PI * 0.5f) / (float)quarterRingSegments;
            float ringAnglesRadians = math.PI / (float)ringCount;
            
            for (int r = 0; r < ringCount; r++)
            {
                float3 verticalStart = top;
                float3 horizontalStart = right;
                quaternion verticalRingRotation = math.mul(rotation, quaternion.AxisAngle(math.up(), r * ringAnglesRadians));
                quaternion horizontalRingRotation = math.mul(rotation, quaternion.AxisAngle(math.right(), r * ringAnglesRadians));
                
                for (int s = 1; s <= quarterRingSegments * 4; s++)
                {
                    quaternion verticalSegmentRotation = quaternion.AxisAngle(math.right(), s * segmentAnglesRadians);
                    quaternion horizontalSegmentRotation = quaternion.AxisAngle(math.up(), s * segmentAnglesRadians);

                    quaternion verticalRotation = math.mul(verticalRingRotation, verticalSegmentRotation);
                    quaternion horizontalRotation = math.mul(horizontalRingRotation, horizontalSegmentRotation);

                    // Vertical rings
                    float3 verticalEnd = center + math.mul(verticalRotation, localCenterToTop);
                    DrawLine(verticalStart, verticalEnd, color);
                    verticalStart = verticalEnd;

                    // Horizontal rings
                    float3 horizontalEnd = center + math.mul(horizontalRotation, localCenterToRight);
                    DrawLine(horizontalStart, horizontalEnd, color);
                    horizontalStart = horizontalEnd;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawWireCapsule(
            float3 center, 
            quaternion rotation,
            float radius, 
            float height, 
            int ringCount,
            int quarterRingSegments, 
            UnityEngine.Color color)
        {
            height = math.max(height, radius * 2f);
            quarterRingSegments = math.max(quarterRingSegments, 2);
            ringCount = math.max(ringCount, 1);

            float halfHeight = height * 0.5f;
            float3 localHemiCenterToTop = math.up() * radius;
            float3 localHemiCenterToRight = math.right() * radius;
            float3 hemiCenterToTop = math.mul(rotation, localHemiCenterToTop);
            float3 hemiCenterToRight = math.mul(rotation, localHemiCenterToRight);
            float segmentAnglesRadians = (math.PI * 0.5f) / (float)quarterRingSegments;
            float ringAnglesRadians = math.PI / (float)ringCount;
            float3 topHemiCenterOffsetLocal = math.up() * (halfHeight - radius);
            float3 topHemiCenterOffset = math.mul(rotation, topHemiCenterOffsetLocal);
            
            // Vertical rings
            for (int r = 0; r < ringCount; r++)
            {
                float3 verticalStart = center + topHemiCenterOffset + hemiCenterToTop;
                quaternion verticalRingRotation = math.mul(rotation, quaternion.AxisAngle(math.up(), r * ringAnglesRadians));
                
                for (int s = 1; s <= quarterRingSegments * 4; s++)
                {
                    quaternion verticalSegmentRotation = quaternion.AxisAngle(math.right(), s * segmentAnglesRadians);
                    quaternion verticalRotation = math.mul(verticalRingRotation, verticalSegmentRotation);
                    
                    float3 hemiCenter = center + topHemiCenterOffset;
                    int phase = s / quarterRingSegments;
                    if(phase == 1 || phase == 2)
                    {
                        hemiCenter = center - topHemiCenterOffset;
                        
                        // Handle straight line of capsule body
                        if (s % quarterRingSegments == 0)
                        {
                            float3 straightVerticalEnd = hemiCenter + math.mul(verticalRotation, localHemiCenterToTop);
                            DrawLine(verticalStart, straightVerticalEnd, color);
                            verticalStart = straightVerticalEnd;
                        }
                    }

                    float3 verticalEnd = hemiCenter + math.mul(verticalRotation, localHemiCenterToTop);
                    DrawLine(verticalStart, verticalEnd, color);
                    verticalStart = verticalEnd;
                }
            }
            
            // Horizontal rings
            {
                float3 topHemiCenter = center + topHemiCenterOffset;
                float3 bottomHemiCenter = center - topHemiCenterOffset;
                float3 topStart = topHemiCenter + hemiCenterToRight;
                float3 bottomStart = bottomHemiCenter + hemiCenterToRight;
                
                for (int s = 1; s <= quarterRingSegments * 4; s++)
                {
                    quaternion horizontalSegmentRotation = quaternion.AxisAngle(math.up(), s * segmentAnglesRadians);
                    quaternion horizontalRotation = math.mul(rotation, horizontalSegmentRotation);
                    
                    // Upper ring
                    float3 topEnd = topHemiCenter + math.mul(horizontalRotation, localHemiCenterToRight);
                    DrawLine(topStart, topEnd, color);
                    topStart = topEnd;
                    
                    // Lower ring
                    float3 bottomEnd = bottomHemiCenter + math.mul(horizontalRotation, localHemiCenterToRight);
                    DrawLine(bottomStart, bottomEnd, color);
                    bottomStart = bottomEnd;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            IsDirty.Value = true;

            LinePositions.Clear();
            LineColors.Clear();

            TrianglePositions.Clear();
            TriangleColors.Clear();
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

            if (TrianglePositions.IsCreated)
            {
                TrianglePositions.Dispose(dep);
            }

            if (TriangleColors.IsCreated)
            {
                TriangleColors.Dispose(dep);
            }
        }
    }
}
