using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove
{
    public struct FreeIndexRange
    {
        public int Start;
        public int Length;
    }

    public static class MathUtilities
    {
        public static float4 ToFloat4(this float3 f)
        {
            return new float4(f.x, f.y, f.z, 0f);
        }
        public static float3 ToFloat3(this float4 f)
        {
            return new float3(f.x, f.y, f.z);
        }

        // From Unity.Physics
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToEuler(this quaternion q, math.RotationOrder order = math.RotationOrder.XYZ)
        {
            const float epsilon = 1e-6f;

            var qv = q.value;
            var d1 = qv * qv.wwww * new float4(2.0f);
            var d2 = qv * qv.yzxw * new float4(2.0f);
            var d3 = qv * qv;
            var euler = new float3(0.0f);

            const float CUTOFF = (1.0f - 2.0f * epsilon) * (1.0f - 2.0f * epsilon);

            switch (order)
            {
                case math.RotationOrder.ZYX:
                    {
                        var y1 = d2.z + d1.y;
                        if (y1 * y1 < CUTOFF)
                        {
                            var x1 = -d2.x + d1.z;
                            var x2 = d3.x + d3.w - d3.y - d3.z;
                            var z1 = -d2.y + d1.x;
                            var z2 = d3.z + d3.w - d3.y - d3.x;
                            euler = new float3(math.atan2(x1, x2), math.asin(y1), math.atan2(z1, z2));
                        }
                        else 
                        {
                            y1 = math.clamp(y1, -1.0f, 1.0f);
                            var abcd = new float4(d2.z, d1.y, d2.y, d1.x);
                            var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); //2(ad+bc)
                            var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                            euler = new float3(math.atan2(x1, x2), math.asin(y1), 0.0f);
                        }

                        break;
                    }

                case math.RotationOrder.ZXY:
                    {
                        var y1 = d2.y - d1.x;
                        if (y1 * y1 < CUTOFF)
                        {
                            var x1 = d2.x + d1.z;
                            var x2 = d3.y + d3.w - d3.x - d3.z;
                            var z1 = d2.z + d1.y;
                            var z2 = d3.z + d3.w - d3.x - d3.y;
                            euler = new float3(math.atan2(x1, x2), -math.asin(y1), math.atan2(z1, z2));
                        }
                        else
                        {
                            y1 = math.clamp(y1, -1.0f, 1.0f);
                            var abcd = new float4(d2.z, d1.y, d2.y, d1.x);
                            var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z);
                            var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                            euler = new float3(math.atan2(x1, x2), -math.asin(y1), 0.0f);
                        }

                        break;
                    }

                case math.RotationOrder.YXZ:
                    {
                        var y1 = d2.y + d1.x;
                        if (y1 * y1 < CUTOFF)
                        {
                            var x1 = -d2.z + d1.y;
                            var x2 = d3.z + d3.w - d3.x - d3.y;
                            var z1 = -d2.x + d1.z;
                            var z2 = d3.y + d3.w - d3.z - d3.x;
                            euler = new float3(math.atan2(x1, x2), math.asin(y1), math.atan2(z1, z2));
                        }
                        else
                        {
                            y1 = math.clamp(y1, -1.0f, 1.0f);
                            var abcd = new float4(d2.x, d1.z, d2.y, d1.x);
                            var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z);
                            var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                            euler = new float3(math.atan2(x1, x2), math.asin(y1), 0.0f);
                        }

                        break;
                    }

                case math.RotationOrder.YZX:
                    {
                        var y1 = d2.x - d1.z;
                        if (y1 * y1 < CUTOFF)
                        {
                            var x1 = d2.z + d1.y;
                            var x2 = d3.x + d3.w - d3.z - d3.y;
                            var z1 = d2.y + d1.x;
                            var z2 = d3.y + d3.w - d3.x - d3.z;
                            euler = new float3(math.atan2(x1, x2), -math.asin(y1), math.atan2(z1, z2));
                        }
                        else
                        {
                            y1 = math.clamp(y1, -1.0f, 1.0f);
                            var abcd = new float4(d2.x, d1.z, d2.y, d1.x);
                            var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); 
                            var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                            euler = new float3(math.atan2(x1, x2), -math.asin(y1), 0.0f);
                        }

                        break;
                    }

                case math.RotationOrder.XZY:
                    {
                        var y1 = d2.x + d1.z;
                        if (y1 * y1 < CUTOFF)
                        {
                            var x1 = -d2.y + d1.x;
                            var x2 = d3.y + d3.w - d3.z - d3.x;
                            var z1 = -d2.z + d1.y;
                            var z2 = d3.x + d3.w - d3.y - d3.z;
                            euler = new float3(math.atan2(x1, x2), math.asin(y1), math.atan2(z1, z2));
                        }
                        else
                        {
                            y1 = math.clamp(y1, -1.0f, 1.0f);
                            var abcd = new float4(d2.x, d1.z, d2.z, d1.y);
                            var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); 
                            var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                            euler = new float3(math.atan2(x1, x2), math.asin(y1), 0.0f);
                        }

                        break;
                    }

                case math.RotationOrder.XYZ:
                    {
                        var y1 = d2.z - d1.y;
                        if (y1 * y1 < CUTOFF)
                        {
                            var x1 = d2.y + d1.x;
                            var x2 = d3.z + d3.w - d3.y - d3.x;
                            var z1 = d2.x + d1.z;
                            var z2 = d3.x + d3.w - d3.y - d3.z;
                            euler = new float3(math.atan2(x1, x2), -math.asin(y1), math.atan2(z1, z2));
                        }
                        else  
                        {
                            y1 = math.clamp(y1, -1.0f, 1.0f);
                            var abcd = new float4(d2.z, d1.y, d2.x, d1.z);
                            var x1 = 2.0f * (abcd.x * abcd.w + abcd.y * abcd.z); 
                            var x2 = math.csum(abcd * abcd * new float4(-1.0f, 1.0f, -1.0f, 1.0f));
                            euler = new float3(math.atan2(x1, x2), -math.asin(y1), 0.0f);
                        }

                        break;
                    }
            }

            switch (order)
            {
                case math.RotationOrder.XZY:
                    return euler.xzy;
                case math.RotationOrder.YZX:
                    return euler.zxy;
                case math.RotationOrder.YXZ:
                    return euler.yxz;
                case math.RotationOrder.ZXY:
                    return euler.yzx;
                case math.RotationOrder.ZYX:
                    return euler.zyx;
                case math.RotationOrder.XYZ:
                default:
                    return euler;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivideIntCeil(int a, int b)
        {
            return (a / b) + math.select(0, 1, a % b > 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRoughlyEqual(this float a, float b, float error = 0.001f)
        {
            return math.distance(a, b) <= error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRoughlyEqual(this float2 a, float2 b, float error = 0.001f)
        {
            return math.distance(a.x, b.x) <= error && math.distance(a.y, b.y) <= error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRoughlyEqual(this float3 a, float3 b, float error = 0.001f)
        {
            return math.distance(a.x, b.x) <= error && math.distance(a.x, b.x) <= error && math.distance(a.z, b.z) <= error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRoughlyEqual(this quaternion a, quaternion b, float error = 0.001f)
        {
            return math.distance(a.value.x, b.value.x) <= error && math.distance(a.value.x, b.value.x) <= error && math.distance(a.value.z, b.value.z) <= error && math.distance(a.value.w, b.value.w) <= error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleRadians(float3 from, float3 to)
        {
            float denominator = (float)math.sqrt(math.lengthsq(from) * math.lengthsq(to));
            if (denominator < math.EPSILON)
                return 0F;

            float dot = math.clamp(math.dot(from, to) / denominator, -1F, 1F);
            return math.acos(dot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleRadiansToDotRatio(float angleRadians)
        {
            return math.cos(angleRadians);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotRatioToAngleRadians(float dotRatio)
        {
            return math.acos(dotRatio);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ProjectOnPlane(float3 vector, float3 onPlaneNormal)
        {
            return vector - math.projectsafe(vector, onPlaneNormal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ReverseProjectOnVector(float3 projectedVector, float3 onNormalizedVector, float maxLength)
        {
            float projectionRatio = math.dot(math.normalizesafe(projectedVector), onNormalizedVector);
            if (projectionRatio == 0f)
            {
                return projectedVector;
            }

            float deprojectedLength = math.clamp(math.length(projectedVector) / projectionRatio, 0f, maxLength);
            return onNormalizedVector * deprojectedLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ClampToMaxLength(float3 vector, float maxLength)
        {
            float sqrmag = math.lengthsq(vector);
            if (sqrmag > maxLength * maxLength)
            {
                float mag = math.sqrt(sqrmag);
                float normalized_x = vector.x / mag;
                float normalized_y = vector.y / mag;
                float normalized_z = vector.z / mag;
                return new float3(normalized_x * maxLength,
                    normalized_y * maxLength,
                    normalized_z * maxLength);
            }

            return vector;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ReorientVectorOnPlaneAlongDirection(float3 vector, float3 onPlaneNormal, float3 alongDirection)
        {
            float length = math.length(vector);

            if (length <= math.EPSILON)
                return float3.zero;

            float3 reorientAxis = math.cross(vector, alongDirection);
            float3 reorientedVector = math.normalizesafe(math.cross(onPlaneNormal, reorientAxis)) * length;

            return reorientedVector;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion CreateRotationWithUpPriority(float3 up, float3 forward)
        {
            if (math.abs(math.dot(forward, up)) == 1f)
            {
                forward = math.forward();
            }
            forward = math.normalizesafe(ProjectOnPlane(forward, up));

            return quaternion.LookRotationSafe(forward, up);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetAxisSystemFromForward(float3 fwd, out float3 right, out float3 up)
        {
            float3 initialVector = math.up();
            if (math.dot(fwd, initialVector) > 0.9f)
            {
                initialVector = math.right();
            }

            right = math.normalizesafe(math.cross(initialVector, fwd));
            up = math.normalizesafe(math.cross(fwd, right));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculatePointDisplacement(float3 pointWorldSpace, RigidTransform fromTransform, RigidTransform toTransform)
        {
            float3 pointLocalPositionRelativeToPreviousTransform = math.transform(math.inverse(fromTransform), pointWorldSpace);
            float3 pointNewWorldPosition = math.transform(toTransform, pointLocalPositionRelativeToPreviousTransform);
            return pointNewWorldPosition - pointWorldSpace;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetRotationAroundPoint(ref quaternion rotation, ref float3 position, float3 aroundPoint, quaternion targetRotation)
        {
            float3 localPointToTranslation = math.mul(math.inverse(rotation), position - aroundPoint);
            position = aroundPoint + math.mul(targetRotation, localPointToTranslation);
            rotation = targetRotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RotateAroundPoint(ref quaternion rotation, ref float3 position, float3 aroundPoint, quaternion addedRotation)
        {
            float3 localPointToTranslation = math.mul(math.inverse(rotation), position - aroundPoint);
            rotation = math.mul(rotation, addedRotation);
            position = aroundPoint + math.mul(rotation, localPointToTranslation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RangesOverlap(int startA, int lengthA, int startB, int lengthB)
        {
            return startA < (startB + lengthB) && startB < (startA + lengthA);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RangeContains(int start, int length, int containedStart, int containedLength)
        {
            return containedStart >= start && 
                   (containedStart + containedLength <= start + length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetGrid2DParams(int count, float spacing, out float2 extents, out int resolution)
        {
            resolution = (int)math.ceil(math.sqrt(count));
            extents = (resolution * 0.5f) * spacing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetGrid2DPosition(int index, float spacing, int resolution)
        {
            return new float2(
                (index % resolution) * spacing,
                ((index / resolution) % resolution) * spacing);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetGrid3DParams(int count, float spacing, out float3 extents, out int resolution)
        {
            resolution = (int)math.ceil(math.pow(count, 1f / 3f));
            if (resolution <= 1)
            {
                extents = float3.zero;
            }
            else
            {
                extents = (resolution * 0.5f) * spacing;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetGrid3DPosition(int index, float spacing, int resolution)
        {
            return new float3(
                (index % resolution) * spacing,
                (index / (resolution * resolution)) * spacing,
                ((index / resolution) % resolution) * spacing);
        }

        // TODO: untested
        public static void AddFreeRange(int start, int length, ref DynamicBuffer<FreeIndexRange> freeIndexRanges)
        {
            if (start < 0 || length <= 0)
            {
                return;
            }
            
            for (int i = 0; i < freeIndexRanges.Length; i++)
            {
                FreeIndexRange freeIndexRange = freeIndexRanges[i];

                if (MathUtilities.RangesOverlap(freeIndexRange.Start, freeIndexRange.Length, start, length))
                {
                    throw new Exception($"Error: FreeRanges overlap detected. This should never happen. Make sure FreeRanges data is never tampered with.");
                }
                
                // If the iterated free range ends where the new one starts, merge them (extend length)
                if (freeIndexRange.Start + freeIndexRange.Length == start)
                {
                    freeIndexRange.Length += length;

                    // Check if we need to merge with next free range
                    if (i + 1 < freeIndexRanges.Length)
                    {
                        FreeIndexRange nextFreeIndexRange = freeIndexRanges[i+1];
                        
                        if (MathUtilities.RangesOverlap(freeIndexRange.Start, freeIndexRange.Length, nextFreeIndexRange.Start, nextFreeIndexRange.Length))
                        {
                            throw new Exception($"Error: FreeRanges overlap detected. This should never happen. Make sure FreeRanges data is never tampered with.");
                        }
                        
                        // If the next free range starts where this one ends, merge them (extend length and remove next)
                        if (nextFreeIndexRange.Start == freeIndexRange.Start + freeIndexRange.Length)
                        {
                            freeIndexRanges.Length += nextFreeIndexRange.Length;
                            freeIndexRanges.RemoveAt(i+1);
                        }
                    }
                    
                    freeIndexRanges[i] = freeIndexRange;
                    
                    return;
                }
                // If freeRange starts after the new one ends, insert new one at this index
                else if (freeIndexRange.Start > start + length)
                {
                    freeIndexRanges.Insert(i, new FreeIndexRange
                    {
                        Start = start,
                        Length = length,
                    });
                    return;
                }
                // If the iterated freeRange starts where the new one ends, merge them 
                else if (freeIndexRange.Start == start + length)
                {
                    freeIndexRange.Start = start;
                    freeIndexRange.Length += length;
                    freeIndexRanges[i] = freeIndexRange;
                    return;
                }
            }
            
            // If we haven't returned yet, it means we didn't find a place to insert the new range. Add at the end.
            freeIndexRanges.Add(new FreeIndexRange
            {
                Start = start,
                Length = length,
            });
        }

        // TODO: untested
        public static void RemoveFreeRange(int start, int length, ref DynamicBuffer<FreeIndexRange> freeIndexRanges)
        {
            if (start < 0 || length <= 0)
            {
                return;
            }
            
            for (int i = 0; i < freeIndexRanges.Length; i++)
            {
                FreeIndexRange freeIndexRange = freeIndexRanges[i];

                // If the ranges overlap...
                if (MathUtilities.RangesOverlap(freeIndexRange.Start, freeIndexRange.Length, start, length))
                {
                    // If the range fully contains the one to remove, remove range
                    if (MathUtilities.RangeContains(freeIndexRange.Start, freeIndexRange.Length, start, length))
                    {
                        // If the ranges have the same start...
                        if (freeIndexRange.Start == start)
                        {
                            // if we remove the entire range...
                            if (freeIndexRange.Length == length)
                            {
                                freeIndexRanges.RemoveAt(i);   
                                return;
                            }
                            // Remove a starting portion of the range
                            else
                            {
                                freeIndexRange.Start += length;
                                freeIndexRange.Length -= length;
                                freeIndexRanges[i] = freeIndexRange;
                                return;
                            }
                        }
                        // If the ranges have the same end (but not the same start), remove ending portion of the range
                        else if (freeIndexRange.Start + freeIndexRange.Length == start + length)
                        {
                            freeIndexRange.Length -= length;
                            freeIndexRanges[i] = freeIndexRange;
                            return;
                        }
                        // If the removed range is in the middle of the iterated range, split into two
                        else
                        {
                            FreeIndexRange beginningFreeIndexRange = new FreeIndexRange
                            {
                                Start = freeIndexRange.Start,
                                Length = start - freeIndexRange.Start,
                            };
                            FreeIndexRange endingFreeIndexRange = new FreeIndexRange
                            {
                                Start = start + length,
                                Length = freeIndexRange.Start + freeIndexRange.Length,
                            };
                            freeIndexRanges[i] = endingFreeIndexRange;
                            freeIndexRanges.Insert(i, beginningFreeIndexRange);
                            return;
                        }
                    }
                    // If ranges overlap but the removed one is not fully contained in the iterated one, throw.
                    else
                    {
                        throw new Exception($"Error: Tried to remove a free range that was not fully allocated. This should never happen.");
                    }
                }
            }
        }
    }
}