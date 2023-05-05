using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Trove
{
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
    }
}