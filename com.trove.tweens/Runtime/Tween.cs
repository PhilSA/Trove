using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Trove.Tweens
{
    [Serializable]
    public struct TweenAuthoringData
    {
        public float Duration;
        public float Speed;
        public bool IsLoop;
        public bool IsRewind;

        public static TweenAuthoringData GetDefault()
        {
            return new TweenAuthoringData
            {
                IsLoop = false,
                IsRewind = false,
                Duration = 1f,
                Speed = 1f,
            };
        }
    }

    [Serializable]
    public struct TweenTimer : IComponentData
    {
        public float Speed;

        public float __internal__duration;
        public float __internal__time;
        public float __internal__normalizedTime;
        public float __internal__excessTime;
        public ushort __internal__loopsCount;
        public byte __internal__flags;

        private const int HasChangedBitPosition = 0;
        private const int IsPlayingBitPosition = 1;
        private const int WasPlayingBitPosition = 2;
        private const int HasCompletedBitPosition = 3;
        private const int ProgressionDirectionBitPosition = 4;
        private const int IsLoopBitPosition = 5;
        private const int IsRewindBitPosition = 6;

        private bool InternalHasChanged { get { return BitUtilities.GetBit(__internal__flags, HasChangedBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, HasChangedBitPosition); } }
        public bool IsPlaying { get { return BitUtilities.GetBit(__internal__flags, IsPlayingBitPosition); } private set { BitUtilities.SetBit(value, ref __internal__flags, IsPlayingBitPosition); } }
        private bool InternalWasPlaying { get { return BitUtilities.GetBit(__internal__flags, WasPlayingBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, WasPlayingBitPosition); } }
        private bool InternalHasCompleted { get { return BitUtilities.GetBit(__internal__flags, HasCompletedBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, HasCompletedBitPosition); } }
        public bool IsGoingInReverse { get { return BitUtilities.GetBit(__internal__flags, ProgressionDirectionBitPosition); } private set { BitUtilities.SetBit(value, ref __internal__flags, ProgressionDirectionBitPosition); } }
        public bool IsLoop { get { return BitUtilities.GetBit(__internal__flags, IsLoopBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, IsLoopBitPosition); } }
        public bool IsRewind { get { return BitUtilities.GetBit(__internal__flags, IsRewindBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, IsRewindBitPosition); } }

        public TweenTimer(TweenAuthoringData data, bool autoPlay = false)
        {
            Speed = data.Speed;

            __internal__duration = data.Duration;
            __internal__time = 0f;
            __internal__normalizedTime = 0f;
            __internal__excessTime = 0f;
            __internal__loopsCount = 0;

            __internal__flags = 0;
            IsPlaying = false;
            InternalWasPlaying = false;
            InternalHasCompleted = false;
            IsGoingInReverse = false;
            IsLoop = data.IsLoop;
            IsRewind = data.IsRewind;

            ApplyChanges();

            if (autoPlay)
            {
                Play(true);
            }
        }

        public TweenTimer(float duration, bool isLoop, bool isRewind, float speed = 1f, bool autoPlay = false)
        {
            Speed = speed;

            __internal__duration = duration;
            __internal__time = 0f;
            __internal__normalizedTime = 0f;
            __internal__excessTime = 0f;
            __internal__loopsCount = 0;

            __internal__flags = 0;
            IsPlaying = false;
            InternalWasPlaying = false;
            InternalHasCompleted = false;
            IsGoingInReverse = false;
            IsLoop = isLoop;
            IsRewind = isRewind;

            ApplyChanges();

            if(autoPlay)
            {
                Play(true);
            }
        }

        public void Update(float deltaTime)
        {
            Update(deltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged);
        }

        public void Update(float deltaTime, out bool hasChanged)
        {
            Update(deltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out hasChanged);
        }

        public void Update(float deltaTime, out bool hasStartedPlaying, out bool hasStoppedPlaying, out bool hasChanged)
        {
            hasStartedPlaying = false;
            hasStoppedPlaying = false;
            hasChanged = InternalHasChanged;

            if (IsPlaying)
            {
                InternalHasChanged = true;
                __internal__time += Speed * deltaTime * (IsGoingInReverse ? -1f : 1f);
                ApplyChanges();

                hasChanged = true;

                if (!InternalWasPlaying)
                {
                    hasStartedPlaying = true;
                }
            }
            else if (InternalWasPlaying)
            {
                hasStoppedPlaying = true;
            }

            InternalWasPlaying = IsPlaying;
            InternalHasChanged = false;
        }

        public float GetTime()
        {
            return __internal__time;
        }

        public void SetTime(float value)
        {
            __internal__time = value;
            InternalHasChanged = true;
            ApplyChanges();
        }

        public float GetDuration()
        {
            return __internal__duration;
        }

        public void SetDuration(float value)
        {
            __internal__duration = value;
            InternalHasChanged = true;
            ApplyChanges();
        }

        public float GetNormalizedTime()
        {
            return __internal__normalizedTime;
        }

        public float GetInverseNormalizedTime()
        {
            return 1f - __internal__normalizedTime;
        }

        public void SetNormalizedTime(float value)
        {
            __internal__time = value * __internal__duration;
            InternalHasChanged = true;
            ApplyChanges();
        }

        public float GetExcessTime()
        {
            return __internal__excessTime;
        }

        public void Play(bool reset)
        {
            if (reset)
            {
                Stop();
            }
            IsPlaying = true;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Stop()
        {
            ResetState();
            InternalHasChanged = true;
            SetTime(0f);
        }

        public void ResetState()
        {
            IsPlaying = false;
            InternalWasPlaying = false;
            InternalHasCompleted = false;
            IsGoingInReverse = false;
            InternalHasChanged = true;
            __internal__loopsCount = 0;
            ApplyChanges();
        }

        public bool HasCompleted()
        {
            return InternalHasCompleted;
        }

        public int GetLoopsCount()
        {
            return __internal__loopsCount;
        }

        public void SetCourse(bool forward)
        {
            IsGoingInReverse = !forward;
            InternalHasChanged = true;
            ApplyChanges();
        }

        private void ApplyChanges()
        {
            InternalHasCompleted = false;

            if (__internal__duration > 0f)
            {
                // Reverse direction
                if (IsGoingInReverse)
                {
                    // Check reached completion
                    __internal__excessTime = -__internal__time;
                    if (__internal__excessTime > 0f)
                    {
                        if (IsRewind)
                        {
                            // Reached end of reverse progression
                            if (IsLoop)
                            {
                                IsGoingInReverse = false;
                                __internal__time = __internal__excessTime;
                                __internal__loopsCount++;
                            }
                            else
                            {
                                InternalHasCompleted = true;
                                IsPlaying = false;
                                IsGoingInReverse = false;
                                __internal__time = 0f;
                            }
                        }
                        else
                        {
                            InternalHasCompleted = true;
                            IsPlaying = false;
                            __internal__time = 0f;
                        }
                    }
                }
                // Forward direction
                else
                {
                    // Check reached completion
                    __internal__excessTime = __internal__time - __internal__duration;
                    if (__internal__excessTime > 0f)
                    {
                        if (IsRewind)
                        {
                            IsGoingInReverse = true;
                            __internal__time = __internal__duration - __internal__excessTime;
                        }
                        else
                        {
                            // Reached completion of forward progression
                            if (IsLoop)
                            {
                                __internal__time = __internal__excessTime;
                                __internal__loopsCount++;
                            }
                            else
                            {
                                InternalHasCompleted = true;
                                IsPlaying = false;
                                __internal__time = __internal__duration;
                            }
                        }
                    }
                }

                __internal__normalizedTime = math.saturate(__internal__time / __internal__duration);
            }
        }
    }

    public struct TweenerFloat
    {
        private const int CaptureInitialOnStartPlayingBitPosition = 0;
        private const int TargetIsRelativeBitPosition = 1;

        public EasingType Easing;
        public float Target;

        public byte _flags;
        public float _initial;
        public float _initialTweened;

        private bool CaptureInitialOnStartPlaying { get { return BitUtilities.GetBit(_flags, CaptureInitialOnStartPlayingBitPosition); } set { BitUtilities.SetBit(value, ref _flags, CaptureInitialOnStartPlayingBitPosition); } }
        private bool TargetIsRelative { get { return BitUtilities.GetBit(_flags, TargetIsRelativeBitPosition); } set { BitUtilities.SetBit(value, ref _flags, TargetIsRelativeBitPosition); } }

        public TweenerFloat(
            float target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = default;
            _initialTweened = default;

            _flags = 0;
            CaptureInitialOnStartPlaying = true;
            TargetIsRelative = targetIsRelative;
        }

        public TweenerFloat(
            float initial,
            float target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = initial;
            _initialTweened = initial;

            _flags = 0;
            CaptureInitialOnStartPlaying = false;
            TargetIsRelative = targetIsRelative;
        }

        public void Update(float normalizedTime, bool hasStartedPlaying, ref float value)
        {
            if (hasStartedPlaying)
            {
                if (CaptureInitialOnStartPlaying)
                {
                    _initial = value;
                }

                if (TargetIsRelative)
                {
                    _initialTweened = default;
                }
            }

            float tweenedValue = math.lerp(_initialTweened, Target, EasingUtilities.CalculateEasing(normalizedTime, Easing));
            if (TargetIsRelative)
            {
                value = _initial + tweenedValue;
            }
            else
            {
                value = tweenedValue;
            }
        }
    }

    public struct TweenerFloat2
    {
        private const int CaptureInitialOnStartPlayingBitPosition = 0;
        private const int TargetIsRelativeBitPosition = 1;

        public EasingType Easing;
        public float2 Target;

        public byte _flags;
        public float2 _initial;
        public float2 _initialTweened;

        private bool CaptureInitialOnStartPlaying { get { return BitUtilities.GetBit(_flags, CaptureInitialOnStartPlayingBitPosition); } set { BitUtilities.SetBit(value, ref _flags, CaptureInitialOnStartPlayingBitPosition); } }
        private bool TargetIsRelative { get { return BitUtilities.GetBit(_flags, TargetIsRelativeBitPosition); } set { BitUtilities.SetBit(value, ref _flags, TargetIsRelativeBitPosition); } }

        public TweenerFloat2(
            float2 target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = default;
            _initialTweened = default;

            _flags = 0;
            CaptureInitialOnStartPlaying = true;
            TargetIsRelative = targetIsRelative;
        }

        public TweenerFloat2(
            float2 initial,
            float2 target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = initial;
            _initialTweened = initial;

            _flags = 0;
            CaptureInitialOnStartPlaying = false;
            TargetIsRelative = targetIsRelative;
        }

        public void Update(float normalizedTime, bool hasStartedPlaying, ref float2 value)
        {
            if (hasStartedPlaying)
            {
                if (CaptureInitialOnStartPlaying)
                {
                    _initial = value;
                }

                if (TargetIsRelative)
                {
                    _initialTweened = default;
                }
            }

            float2 tweenedValue = math.lerp(_initialTweened, Target, EasingUtilities.CalculateEasing(normalizedTime, Easing));
            if (TargetIsRelative)
            {
                value = _initial + tweenedValue;
            }
            else
            {
                value = tweenedValue;
            }
        }
    }

    public struct TweenerFloat3
    {
        private const int CaptureInitialOnStartPlayingBitPosition = 0;
        private const int TargetIsRelativeBitPosition = 1;

        public EasingType Easing;
        public float3 Target;

        public byte _flags;
        public float3 _initial;
        public float3 _initialTweened;

        private bool CaptureInitialOnStartPlaying { get { return BitUtilities.GetBit(_flags, CaptureInitialOnStartPlayingBitPosition); } set { BitUtilities.SetBit(value, ref _flags, CaptureInitialOnStartPlayingBitPosition); } }
        private bool TargetIsRelative { get { return BitUtilities.GetBit(_flags, TargetIsRelativeBitPosition); } set { BitUtilities.SetBit(value, ref _flags, TargetIsRelativeBitPosition); } }

        public TweenerFloat3(
            float3 target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = default;
            _initialTweened = default;

            _flags = 0;
            CaptureInitialOnStartPlaying = true;
            TargetIsRelative = targetIsRelative;
        }

        public TweenerFloat3(
            float3 initial,
            float3 target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = initial;
            _initialTweened = initial;

            _flags = 0;
            CaptureInitialOnStartPlaying = false;
            TargetIsRelative = targetIsRelative;
        }

        public void Update(float normalizedTime, bool hasStartedPlaying, ref float3 value)
        {
            if (hasStartedPlaying)
            {
                if (CaptureInitialOnStartPlaying)
                {
                    _initial = value;
                    Debug.Log($"initial {value}");
                }

                if (TargetIsRelative)
                {
                    _initialTweened = default;
                }
            }

            float3 tweenedValue = math.lerp(_initialTweened, Target, EasingUtilities.CalculateEasing(normalizedTime, Easing));
            if (TargetIsRelative)
            {
                value = _initial + tweenedValue;
            }
            else
            {
                value = tweenedValue;
            }
        }
    }

    public struct TweenerFloat4
    {
        private const int CaptureInitialOnStartPlayingBitPosition = 0;
        private const int TargetIsRelativeBitPosition = 1;

        public EasingType Easing;
        public float4 Target;

        public byte _flags;
        public float4 _initial;
        public float4 _initialTweened;

        private bool CaptureInitialOnStartPlaying { get { return BitUtilities.GetBit(_flags, CaptureInitialOnStartPlayingBitPosition); } set { BitUtilities.SetBit(value, ref _flags, CaptureInitialOnStartPlayingBitPosition); } }
        private bool TargetIsRelative { get { return BitUtilities.GetBit(_flags, TargetIsRelativeBitPosition); } set { BitUtilities.SetBit(value, ref _flags, TargetIsRelativeBitPosition); } }

        public TweenerFloat4(
            float4 target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = default;
            _initialTweened = default;

            _flags = 0;
            CaptureInitialOnStartPlaying = true;
            TargetIsRelative = targetIsRelative;
        }

        public TweenerFloat4(
            float4 initial,
            float4 target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = initial;
            _initialTweened = initial;

            _flags = 0;
            CaptureInitialOnStartPlaying = false;
            TargetIsRelative = targetIsRelative;
        }

        public void Update(float normalizedTime, bool hasStartedPlaying, ref float4 value)
        {
            if (hasStartedPlaying)
            {
                if (CaptureInitialOnStartPlaying)
                {
                    _initial = value;
                }

                if (TargetIsRelative)
                {
                    _initialTweened = default;
                }
            }

            float4 tweenedValue = math.lerp(_initialTweened, Target, EasingUtilities.CalculateEasing(normalizedTime, Easing));
            if (TargetIsRelative)
            {
                value = _initial + tweenedValue;
            }
            else
            {
                value = tweenedValue;
            }
        }
    }

    public struct TweenerQuaternion
    {
        private const int CaptureInitialOnStartPlayingBitPosition = 0;
        private const int TargetIsRelativeBitPosition = 1;

        public EasingType Easing;
        public quaternion Target;

        public byte _flags;
        public quaternion _initial;
        public quaternion _initialTweened;

        private bool CaptureInitialOnStartPlaying { get { return BitUtilities.GetBit(_flags, CaptureInitialOnStartPlayingBitPosition); } set { BitUtilities.SetBit(value, ref _flags, CaptureInitialOnStartPlayingBitPosition); } }
        private bool TargetIsRelative { get { return BitUtilities.GetBit(_flags, TargetIsRelativeBitPosition); } set { BitUtilities.SetBit(value, ref _flags, TargetIsRelativeBitPosition); } }

        public TweenerQuaternion(
            quaternion target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = default;
            _initialTweened = default;

            _flags = 0;
            CaptureInitialOnStartPlaying = true;
            TargetIsRelative = targetIsRelative;
        }

        public TweenerQuaternion(
            quaternion initial,
            quaternion target,
            bool targetIsRelative,
            EasingType easing = EasingType.Linear)
        {
            Easing = easing;
            Target = target;

            _initial = initial;
            _initialTweened = initial;

            _flags = 0;
            CaptureInitialOnStartPlaying = false;
            TargetIsRelative = targetIsRelative;
        }

        public void Update(float normalizedTime, bool hasStartedPlaying, ref quaternion value)
        {
            if (hasStartedPlaying)
            {
                if (CaptureInitialOnStartPlaying)
                {
                    _initial = value;
                }

                if (TargetIsRelative)
                {
                    _initialTweened = default;
                }
            }

            quaternion tweenedValue = math.slerp(_initialTweened, Target, EasingUtilities.CalculateEasing(normalizedTime, Easing));
            if (TargetIsRelative)
            {
                value = math.mul(tweenedValue, _initial);
            }
            else
            {
                value = tweenedValue;
            }
        }
    }
}