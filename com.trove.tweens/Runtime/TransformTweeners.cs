using System;
using Trove.Tweens;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Trove.Tweens
{
    [Serializable]
    public struct LocalTransformTweener
    {
        public enum Type
        {
            Position,
            PositionX,
            PositionY,
            PositionZ,

            Rotation,
            RotationEulerX,
            RotationEulerY,
            RotationEulerZ,

            UniformScale,
        }

        public enum Mode
        {
            Absolute,
            Additive,
            AdditiveLocalSpace,
        }

        public Type TweenerType;
        public Mode TweenerMode;
        public float4 Initial;
        public float4 Target;
        public float4 __internal__addedTarget;

        public static LocalTransformTweener Position(float3 target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.Position,
                TweenerMode = mode,
                Target = new float4(target.x, target.y, target.z, 0f),
            };
        }

        public static LocalTransformTweener PositionX(float target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.PositionX,
                TweenerMode = mode,
                Target = new float4(target, 0f, 0f, 0f),
            };
        }

        public static LocalTransformTweener PositionY(float target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.PositionY,
                TweenerMode = mode,
                Target = new float4(0f, target, 0f, 0f),
            };
        }

        public static LocalTransformTweener PositionZ(float target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.PositionZ,
                TweenerMode = mode,
                Target = new float4(0f, 0f, target, 0f),
            };
        }

        public static LocalTransformTweener Rotation(quaternion target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.Rotation,
                TweenerMode = mode,
                Target = target.value,
            };
        }

        public static LocalTransformTweener RotationEuler(float3 target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.Rotation,
                TweenerMode = mode,
                Target = quaternion.Euler(target).value,
            };
        }

        public static LocalTransformTweener RotationEulerX(float target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.RotationEulerX,
                TweenerMode = mode,
                Target = new float4(target, 0f, 0f, 0f),
            };
        }

        public static LocalTransformTweener RotationEulerY(float target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.RotationEulerY,
                TweenerMode = mode,
                Target = new float4(0f, target, 0f, 0f),
            };
        }

        public static LocalTransformTweener RotationEulerZ(float target, Mode mode = Mode.Absolute)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.RotationEulerZ,
                TweenerMode = mode,
                Target = new float4(0f, 0f, target, 0f),
            };
        }

        public static LocalTransformTweener UniformScale(float target, bool isAdditive = false)
        {
            return new LocalTransformTweener
            {
                TweenerType = Type.UniformScale,
                TweenerMode = isAdditive ? Mode.Additive : Mode.Absolute,
                Target = new float4(target, 0f, 0f, 0f),
            };
        }

        public void StoreInitialValue(LocalTransform tweenedStruct)
        {
            if (TweenerMode == Mode.Absolute)
            {
                switch (TweenerType)
                {
                    case Type.Position:
                        Initial = new float4(tweenedStruct.Position, 0f);
                        break;
                    case Type.PositionX:
                        Initial.x = tweenedStruct.Position.x;
                        break;
                    case Type.PositionY:
                        Initial.y = tweenedStruct.Position.y;
                        break;
                    case Type.PositionZ:
                        Initial.z = tweenedStruct.Position.z;
                        break;
                    case Type.Rotation:
                        Initial = tweenedStruct.Rotation.value;
                        break;
                    case Type.RotationEulerX:
                    case Type.RotationEulerY:
                    case Type.RotationEulerZ:
                        Initial = new float4(tweenedStruct.Rotation.ToEuler(), 0f);
                        break;
                    case Type.UniformScale:
                        Initial.x = tweenedStruct.Scale;
                        break;
                }
            }
            else
            {
                switch (TweenerType)
                {
                    case Type.Position:
                    case Type.PositionX:
                    case Type.PositionY:
                    case Type.PositionZ:
                    case Type.UniformScale:
                    case Type.RotationEulerX:
                    case Type.RotationEulerY:
                    case Type.RotationEulerZ:
                        __internal__addedTarget = default;
                        Initial = default;
                        break;
                    case Type.Rotation:
                        __internal__addedTarget = quaternion.identity.value;
                        Initial = quaternion.identity.value;
                        break;
                }
            }
        }

        public void Update(float ratio, ref LocalTransform tweenedStruct)
        {
            // Tween value from initial to target
            switch (TweenerType)
            {
                case Type.Position:
                    {
                        float3 initial = Initial.ToFloat3();
                        float3 target = Target.ToFloat3();
                        float3 ratioResult = math.lerp(initial, target, ratio);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    tweenedStruct.Position = ratioResult;
                                }
                                break;
                            case Mode.Additive:
                                {
                                    float3 added = ratioResult - __internal__addedTarget.ToFloat3();
                                    __internal__addedTarget = ratioResult.ToFloat4();
                                    tweenedStruct.Position += added;
                                }
                                break;
                            case Mode.AdditiveLocalSpace:
                                {
                                    float3 added = ratioResult - __internal__addedTarget.ToFloat3();
                                    __internal__addedTarget = ratioResult.ToFloat4();
                                    added = math.rotate(tweenedStruct.Rotation, added);
                                    tweenedStruct.Position += added;
                                }
                                break;
                        }
                    }
                    break;
                case Type.PositionX:
                    {
                        float initial = Initial.x;
                        float target = Target.x;
                        float ratioResult = math.lerp(initial, target, ratio);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    tweenedStruct.Position.x = ratioResult;
                                }
                                break;
                            case Mode.Additive:
                                {
                                    float added = ratioResult - __internal__addedTarget.x;
                                    __internal__addedTarget.x = ratioResult;
                                    tweenedStruct.Position.x += added;
                                }
                                break;
                            case Mode.AdditiveLocalSpace:
                                {
                                    float3 added = math.right() * (ratioResult - __internal__addedTarget.x);
                                    __internal__addedTarget.x = ratioResult;
                                    added = math.rotate(tweenedStruct.Rotation, added);
                                    tweenedStruct.Position += added;
                                }
                                break;
                        }
                    }
                    break;
                case Type.PositionY:
                    {
                        float initial = Initial.y;
                        float target = Target.y;
                        float ratioResult = math.lerp(initial, target, ratio);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    tweenedStruct.Position.y = ratioResult;
                                }
                                break;
                            case Mode.Additive:
                                {
                                    float added = ratioResult - __internal__addedTarget.y;
                                    __internal__addedTarget.y = ratioResult;
                                    tweenedStruct.Position.y += added;
                                }
                                break;
                            case Mode.AdditiveLocalSpace:
                                {
                                    float3 added = math.up() * (ratioResult - __internal__addedTarget.y);
                                    __internal__addedTarget.y = ratioResult;
                                    added = math.rotate(tweenedStruct.Rotation, added);
                                    tweenedStruct.Position += added;
                                }
                                break;
                        }
                    }
                    break;
                case Type.PositionZ:
                    {
                        float initial = Initial.z;
                        float target = Target.z;
                        float ratioResult = math.lerp(initial, target, ratio);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    tweenedStruct.Position.z = ratioResult;
                                }
                                break;
                            case Mode.Additive:
                                {
                                    float added = ratioResult - __internal__addedTarget.z;
                                    __internal__addedTarget.z = ratioResult;
                                    tweenedStruct.Position.z += added;
                                }
                                break;
                            case Mode.AdditiveLocalSpace:
                                {
                                    float3 added = math.forward() * (ratioResult - __internal__addedTarget.z);
                                    __internal__addedTarget.z = ratioResult;
                                    added = math.rotate(tweenedStruct.Rotation, added);
                                    tweenedStruct.Position += added;
                                }
                                break;
                        }
                    }
                    break;
                case Type.Rotation:
                    {
                        quaternion initial = new quaternion(Initial);
                        quaternion target = new quaternion(Target);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    tweenedStruct.Rotation = math.slerp(initial, target, ratio);
                                }
                                break;
                            case Mode.Additive:
                                {
                                    quaternion ratioResult = math.slerp(initial, target, ratio);
                                    quaternion added = math.mul(math.inverse(new quaternion(__internal__addedTarget)), ratioResult);
                                    __internal__addedTarget = ratioResult.value;
                                    tweenedStruct.Rotation = math.mul(added, tweenedStruct.Rotation);
                                }
                                break;
                            case Mode.AdditiveLocalSpace:
                                {
                                    quaternion ratioResult = math.slerp(initial, target, ratio);
                                    quaternion added = math.mul(math.inverse(ratioResult), new quaternion(__internal__addedTarget));
                                    __internal__addedTarget = ratioResult.value;
                                    tweenedStruct.Rotation = math.mul(tweenedStruct.Rotation, added);
                                }
                                break;
                        }
                    }
                    break;
                case Type.RotationEulerX:
                    {
                        float initial = Initial.x;
                        float target = Target.x;
                        float ratioResult = math.lerp(initial, target, ratio);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    float3 currentEuler = tweenedStruct.Rotation.ToEuler();
                                    tweenedStruct.Rotation = quaternion.Euler(ratioResult, currentEuler.y, currentEuler.z);
                                }
                                break;
                            case Mode.Additive:
                                {
                                    float added = ratioResult - __internal__addedTarget.x;
                                    __internal__addedTarget.x = ratioResult;
                                    tweenedStruct.Rotation = math.mul(quaternion.Euler(added, 0f, 0f), tweenedStruct.Rotation);
                                }
                                break;
                            case Mode.AdditiveLocalSpace:
                                {
                                    float added = ratioResult - __internal__addedTarget.x;
                                    __internal__addedTarget.x = ratioResult;
                                    tweenedStruct.Rotation = math.mul(tweenedStruct.Rotation, quaternion.Euler(added, 0f, 0f));
                                }
                                break;
                        }
                    }
                    break;
                case Type.RotationEulerY:
                    {
                        float initial = Initial.y;
                        float target = Target.y;
                        float ratioResult = math.lerp(initial, target, ratio);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    float3 currentEuler = tweenedStruct.Rotation.ToEuler();
                                    tweenedStruct.Rotation = quaternion.Euler(currentEuler.x, ratioResult, currentEuler.z);
                                }
                                break;
                            case Mode.Additive:
                                {
                                    float added = ratioResult - __internal__addedTarget.y;
                                    __internal__addedTarget.y = ratioResult;
                                    tweenedStruct.Rotation = math.mul(quaternion.Euler(0f, added, 0f), tweenedStruct.Rotation);
                                }
                                break;
                            case Mode.AdditiveLocalSpace:
                                {
                                    float added = ratioResult - __internal__addedTarget.y;
                                    __internal__addedTarget.y = ratioResult;
                                    tweenedStruct.Rotation = math.mul(tweenedStruct.Rotation, quaternion.Euler(0f, added, 0f));
                                }
                                break;
                        }
                    }
                    break;
                case Type.RotationEulerZ:
                    {
                        float initial = Initial.z;
                        float target = Target.z;
                        float ratioResult = math.lerp(initial, target, ratio);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    float3 currentEuler = tweenedStruct.Rotation.ToEuler();
                                    tweenedStruct.Rotation = quaternion.Euler(currentEuler.x, currentEuler.y, ratioResult);
                                }
                                break;
                            case Mode.Additive:
                                {
                                    float added = ratioResult - __internal__addedTarget.z;
                                    __internal__addedTarget.z = ratioResult;
                                    tweenedStruct.Rotation = math.mul(quaternion.Euler(0f, 0f, added), tweenedStruct.Rotation);
                                }
                                break;
                            case Mode.AdditiveLocalSpace:
                                {
                                    float added = ratioResult - __internal__addedTarget.z;
                                    __internal__addedTarget.z = ratioResult;
                                    tweenedStruct.Rotation = math.mul(tweenedStruct.Rotation, quaternion.Euler(0f, 0f, added));
                                }
                                break;
                        }
                    }
                    break;
                case Type.UniformScale:
                    {
                        float ratioResult = math.lerp(Initial.x, Target.x, ratio);
                        switch (TweenerMode)
                        {
                            case Mode.Absolute:
                                {
                                    tweenedStruct.Scale = ratioResult;
                                }
                                break;
                            case Mode.Additive:
                            case Mode.AdditiveLocalSpace:
                                {
                                    float addedScale = ratioResult - __internal__addedTarget.x;
                                    __internal__addedTarget.x = ratioResult;
                                    tweenedStruct.Scale += addedScale;
                                }
                                break;
                        }
                    }
                    break;
            }
        }
    }

    [Serializable]
    public struct ScaleTweener
    {
        public enum Type
        {
            Scale,
            ScaleX,
            ScaleY,
            ScaleZ,
        }

        public Type TweenerType;
        public bool Additive;
        public float3 Initial;
        public float3 Target;
        public float3 __internal__addedTarget;

        public static ScaleTweener Scale(float3 target, bool additive = false)
        {
            return new ScaleTweener
            {
                TweenerType = Type.Scale,
                Additive = additive,
                Target = target,
            };
        }

        public static ScaleTweener ScaleX(float target, bool additive = false)
        {
            return new ScaleTweener
            {
                TweenerType = Type.ScaleX,
                Additive = additive,
                Target = new float3(target, 0f, 0f),
            };
        }

        public static ScaleTweener ScaleY(float target, bool additive = false)
        {
            return new ScaleTweener
            {
                TweenerType = Type.ScaleY,
                Additive = additive,
                Target = new float3(0f, target, 0f),
            };
        }

        public static ScaleTweener ScaleZ(float target, bool additive = false)
        {
            return new ScaleTweener
            {
                TweenerType = Type.ScaleZ,
                Additive = additive,
                Target = new float3(0f, 0f, target),
            };
        }

        public void StoreInitialValue(in PostTransformMatrix tweenedStruct)
        {
            if (Additive)
            {
                __internal__addedTarget = default;
                Initial = default;
            }
            else
            {
                switch (TweenerType)
                {
                    case Type.Scale:
                        Initial = tweenedStruct.Value.Scale();
                        break;
                    case Type.ScaleX:
                        Initial.x = tweenedStruct.Value.ScaleX();
                        break;
                    case Type.ScaleY:
                        Initial.y = tweenedStruct.Value.ScaleY();
                        break;
                    case Type.ScaleZ:
                        Initial.z = tweenedStruct.Value.ScaleZ();
                        break;
                }
            }
        }

        public void Update(float ratio, ref PostTransformMatrix tweenedStruct)
        {
            // Tween value from initial to target
            switch (TweenerType)
            {
                case Type.Scale:
                    {
                        float3 initial = new float3(Initial.x, Initial.y, Initial.z);
                        float3 target = new float3(Target.x, Target.y, Target.z);
                        float3 ratioResult = math.lerp(initial, target, ratio);
                        if (Additive)
                        {
                            float3 addedScale = ratioResult - __internal__addedTarget.x;
                            __internal__addedTarget = ratioResult;
                            float3 newScale = tweenedStruct.Value.Scale() + addedScale;
                            tweenedStruct.Value = float4x4.Scale(newScale);

                        }
                        else
                        {
                            tweenedStruct.Value = float4x4.Scale(ratioResult);
                        }
                    }
                    break;
                case Type.ScaleX:
                    {
                        float initial = Initial.x;
                        float target = Target.x;
                        float ratioResult = math.lerp(initial, target, ratio);
                        if (Additive)
                        {
                            float addedScale = ratioResult - __internal__addedTarget.x;
                            __internal__addedTarget = ratioResult;
                            float3 newScale = tweenedStruct.Value.Scale() + new float3(addedScale, 0f, 0f);
                            tweenedStruct.Value = float4x4.Scale(newScale);

                        }
                        else
                        {
                            float3 currentScale = tweenedStruct.Value.Scale();
                            float3 newScale = new float3(ratioResult, currentScale.y, currentScale.z);
                            tweenedStruct.Value = float4x4.Scale(newScale);
                        }
                    }
                    break;
                case Type.ScaleY:
                    {
                        float initial = Initial.y;
                        float target = Target.y;
                        float ratioResult = math.lerp(initial, target, ratio);
                        if (Additive)
                        {
                            float addedScale = ratioResult - __internal__addedTarget.y;
                            __internal__addedTarget = ratioResult;
                            float3 newScale = tweenedStruct.Value.Scale() + new float3(0f, addedScale, 0f);
                            tweenedStruct.Value = float4x4.Scale(newScale);

                        }
                        else
                        {
                            float3 currentScale = tweenedStruct.Value.Scale();
                            float3 newScale = new float3(currentScale.x, ratioResult, currentScale.z);
                            tweenedStruct.Value = float4x4.Scale(newScale);
                        }
                    }
                    break;
                case Type.ScaleZ:
                    {
                        float initial = Initial.z;
                        float target = Target.z;
                        float ratioResult = math.lerp(initial, target, ratio);
                        if (Additive)
                        {
                            float addedScale = ratioResult - __internal__addedTarget.z;
                            __internal__addedTarget = ratioResult;
                            float3 newScale = tweenedStruct.Value.Scale() + new float3(0f, 0f, addedScale);
                            tweenedStruct.Value = float4x4.Scale(newScale);

                        }
                        else
                        {
                            float3 currentScale = tweenedStruct.Value.Scale();
                            float3 newScale = new float3(currentScale.x, currentScale.y, ratioResult);
                            tweenedStruct.Value = float4x4.Scale(newScale);
                        }
                    }
                    break;
            }
        }
    }
}