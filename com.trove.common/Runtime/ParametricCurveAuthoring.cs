using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Trove
{
    [Serializable]
    public class ParametricCurveAuthoring
    {
        public ParametricCurve ParametricCurve = ParametricCurve.GetDefault(ParametricCurveType.Linear);
        public CurveGraphProperties GraphProperties = CurveGraphProperties.GetDefault();
        public float DefaultMinY = float.MinValue;
        public float DefaultMaxY = float.MinValue;

        public static ParametricCurveAuthoring GetDefault(float minY = float.MinValue, float maxY = float.MaxValue)
        {
            return new ParametricCurveAuthoring
            {
                DefaultMinY = minY,
                DefaultMaxY = maxY,
                ParametricCurve = ParametricCurve.GetDefault(ParametricCurveType.Linear, minY, maxY),
                GraphProperties = CurveGraphProperties.GetDefault(),
            };
        }
    }
}