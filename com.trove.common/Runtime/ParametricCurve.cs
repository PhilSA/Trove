using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Trove
{
    public enum ParametricCurveType : ushort
    {
        Bypass,
        Step,
        Linear,
        Exponential,
        Sine,
        Logistic,
        Logit,
    }

    [Serializable]
    public struct ParametricCurve
    {
        public ParametricCurveType CurveType;
        public float Shape;
        public float Slope;
        public float VerticalShift;
        public float HorizontalShift;
        public float MinY;
        public float MaxY;

        public float Evaluate(float x)
        {
            switch (CurveType)
            {
                case ParametricCurveType.Bypass:
                    {
                        return 0f;
                    }
                case ParametricCurveType.Step:
                    {
                        if(x >= HorizontalShift)
                        {
                            return FinalizeValue(Slope + VerticalShift);
                        }
                        else
                        {
                            return FinalizeValue(VerticalShift);
                        }
                    }
                case ParametricCurveType.Linear:
                    {
                        return FinalizeValue((Slope * (x - HorizontalShift)) + VerticalShift);
                    }
                case ParametricCurveType.Exponential:
                    {
                        return FinalizeValue((Slope * math.pow(x - HorizontalShift, Shape)) + VerticalShift);
                    }
                case ParametricCurveType.Sine:
                    {
                        return FinalizeValue((Slope * math.cos((x * math.PI * Shape) + (HorizontalShift * math.PI))) + VerticalShift);
                    }
                case ParametricCurveType.Logistic:
                    {
                        return FinalizeValue((Slope / (1f + math.exp(-10f * Shape * (x - 0.5f - HorizontalShift)))) + VerticalShift);
                    }
                case ParametricCurveType.Logit:
                    {
                        return FinalizeValue(Slope * math.log((x - HorizontalShift) / (1f - (x - HorizontalShift))) / 5f + 0.5f + VerticalShift);
                    }
            }

            return 0f;
        }

        private float FinalizeValue(float x)
        {
            if (float.IsNaN(x))
            {
                x = 0f;
            }

            return math.clamp(x, MinY, MaxY);
        }

        public static ParametricCurve GetDefault(ParametricCurveType curveType, float minY = float.MinValue, float maxY = float.MaxValue)
        {
            ParametricCurve newCurve = new ParametricCurve();
            newCurve.CurveType = curveType;
            newCurve.MinY = minY;
            newCurve.MaxY = maxY;

            switch (curveType)
            {
                case ParametricCurveType.Step:
                    newCurve.Shape = 0f;
                    newCurve.Slope = 1f;
                    newCurve.VerticalShift = 0f;
                    newCurve.HorizontalShift = 0.5f;
                    break;
                case ParametricCurveType.Linear:
                    newCurve.Shape = 0f;
                    newCurve.Slope = 1f;
                    newCurve.VerticalShift = 0f;
                    newCurve.HorizontalShift = 0f;
                    break;
                case ParametricCurveType.Exponential:
                    newCurve.Shape = 2f;
                    newCurve.Slope = 1f;
                    newCurve.VerticalShift = 0f;
                    newCurve.HorizontalShift = 0f;
                    break;
                case ParametricCurveType.Sine:
                    newCurve.Shape = 2f;
                    newCurve.Slope = 0.5f;
                    newCurve.VerticalShift = 0.5f;
                    newCurve.HorizontalShift = 0f;
                    break;
                case ParametricCurveType.Logistic:
                    newCurve.Shape = 1f;
                    newCurve.Slope = 1f;
                    newCurve.VerticalShift = 0f;
                    newCurve.HorizontalShift = 0f;
                    break;
                case ParametricCurveType.Logit:
                    newCurve.Shape = 0f;
                    newCurve.Slope = 0.4f;
                    newCurve.VerticalShift = 0f;
                    newCurve.HorizontalShift = 0f;
                    break;
            }

            return newCurve;
        }
    }
}