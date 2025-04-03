using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Trove;
using Trove.Tweens;
using Unity.Mathematics;
using Unity.Collections;
using System;

public class EaseCurveVisualizer : MonoBehaviour
{
    public EasingType EasingType;
    public GenericCurve curve;

    public float FloatA = 1f;
    public float FloatB = 1f;
    public float FloatC = 1f;
    public float FloatD = 1f;

    void OnValidate()
    {
        curve = GetComponent<GenericCurve>();
        curve.CurveEvaluator = (x) =>
        {
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)DateTime.Now.Millisecond);
            FixedList32Bytes<float> randomSlopes = new FixedList32Bytes<float>();
            NoiseUtilities.InitRandomSlopes(ref random, ref randomSlopes);

            //return EasingUtilities.CalculateEasing(x, EasingType);
            //return noise.cnoise(new float2(x * FloatA, x * FloatB));

            return NoiseUtilities.Perlin1D(x * FloatA, randomSlopes);
            //return FloatD + (0.5f * ((FloatB * math.sin((x * FloatA) + FloatC)) + (FloatB * math.sin((math.PI * x * FloatA) + FloatC))));
        };
    }
}
