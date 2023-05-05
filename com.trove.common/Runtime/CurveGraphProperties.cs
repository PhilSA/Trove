using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Trove
{
    [Serializable]
    public struct CurveGraphProperties
    {
        [Header("General")]
        public float GraphHeight;
        public float GraphWidth;
        public Color BackgroundColor;
        public Vector2 Min;
        public Vector2 Max;

        [Header("Curve")]
        public Color CurveColor;
        public float CurveLineWidth;

        [Header("Main Axis")]
        public Color MainAxisColor;
        public float MainAxisLineWidth;

        [Header("Grid")]
        public Color MajorGridColor;
        public Color MinorGridColor;
        public float MajorGridIncrements;
        public float MinorGridIncrements;
        public float MajorGridLineWidth;
        public float MinorGridLineWidth;

        public static CurveGraphProperties GetDefault()
        {
            return new CurveGraphProperties
            {
                GraphHeight = 200f,
                GraphWidth = 200f,
                BackgroundColor = new Color(0.12f, 0.12f, 0.12f),
                Min = new Vector2(0f, 0f),
                Max = new Vector2(1f, 1f),

                CurveColor = new Color(0f, 0.6f, 0.25f),
                CurveLineWidth = 2f,

                MainAxisColor = new Color(1f, 1f, 1f),
                MainAxisLineWidth = 0.5f,

                MajorGridColor = new Color(0.5f, 0.5f, 0.5f),
                MinorGridColor = new Color(0.25f, 0.25f, 0.25f),
                MajorGridIncrements = 0.5f,
                MinorGridIncrements = 0.1f,
                MajorGridLineWidth = 0.5f,
                MinorGridLineWidth = 0.5f,
            };
        }
    }
}