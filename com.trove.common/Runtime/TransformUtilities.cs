using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Trove
{
    public static class TransformUtilities
    {
        public static float3 Position(this float4x4 transform)
        {
            return new float3(transform.c3.x, transform.c3.y, transform.c3.z);
        }

        public static quaternion Rotation(this float4x4 transform)
        {
            return new quaternion(transform);
        }

        public static float3 Scale(this float4x4 transform)
        {
            float scaleX = math.length(transform.c0.xyz);
            float scaleY = math.length(transform.c1.xyz);
            float scaleZ = math.length(transform.c2.xyz);
            return new float3(scaleX, scaleY, scaleZ);
        }

        public static float ScaleX(this float4x4 transform)
        {
            return math.length(transform.c0.xyz);
        }

        public static float ScaleY(this float4x4 transform)
        {
            return math.length(transform.c1.xyz);
        }

        public static float ScaleZ(this float4x4 transform)
        {
            return math.length(transform.c2.xyz);
        }

        public static float3 Scale(this float3x3 transform)
        {
            float scaleX = math.length(transform.c0.xyz);
            float scaleY = math.length(transform.c1.xyz);
            float scaleZ = math.length(transform.c2.xyz);
            return new float3(scaleX, scaleY, scaleZ);
        }

        public static float ScaleX(this float3x3 transform)
        {
            return math.length(transform.c0.xyz);
        }

        public static float ScaleY(this float3x3 transform)
        {
            return math.length(transform.c1.xyz);
        }

        public static float ScaleZ(this float3x3 transform)
        {
            return math.length(transform.c2.xyz);
        }

        public static float UniformScale(this float4x4 transform)
        {
            float scaleX = math.length(transform.c0.xyz);
            float scaleY = math.length(transform.c1.xyz);
            float scaleZ = math.length(transform.c2.xyz);
            return math.max(scaleX, math.max(scaleY, scaleZ));
        }

        public static bool GetWorldTransform(
            Entity entity, 
            in ComponentLookup<Parent> parentLookup, 
            in ComponentLookup<LocalTransform> localTransformLookup,
            out float4x4 worldTransform)
        {
            worldTransform = float4x4.identity;

            if(localTransformLookup.TryGetComponent(entity, out LocalTransform localTransform))
            {
                worldTransform = float4x4.TRS(localTransform.Position, localTransform.Rotation, localTransform.Scale);

                while(parentLookup.TryGetComponent(entity, out Parent parent))
                {
                    entity = parent.Value;
                    if(localTransformLookup.TryGetComponent(entity, out LocalTransform parentLocalTransform))
                    {
                        worldTransform = math.mul(float4x4.TRS(parentLocalTransform.Position, parentLocalTransform.Rotation, parentLocalTransform.Scale), worldTransform);
                    }
                }

                return true;
            }

            return false;
        }
    }
}