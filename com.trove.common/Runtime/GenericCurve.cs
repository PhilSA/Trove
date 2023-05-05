using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Trove
{
    [Serializable]
    [ExecuteInEditMode]
    public class GenericCurve : MonoBehaviour
    {
        public Func<float, float> CurveEvaluator;
        public CurveGraphProperties GraphProperties = CurveGraphProperties.GetDefault();
        public Action OnValidateData;

        private void OnValidate()
        {
            GraphProperties.MajorGridIncrements = math.clamp(GraphProperties.MajorGridIncrements, 0.1f, float.MaxValue);
            GraphProperties.MinorGridIncrements = math.clamp(GraphProperties.MinorGridIncrements, 0.01f, float.MaxValue);
            GraphProperties.GraphWidth = math.clamp(GraphProperties.GraphWidth, 0.1f, 1000f);
            GraphProperties.GraphHeight = math.clamp(GraphProperties.GraphHeight, 0.1f, 1000f);
        }

        void Update()
        {
            if(OnValidateData != null)
            {
                OnValidateData.Invoke();
            }
        }
    }
}