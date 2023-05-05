using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ScoresCalculatorApproaches : MonoBehaviour
{
    public enum Method
    {
        None,
        ImaginaryAverages,
        DePowerify,
        InverseWeightedAverage,
        Official,
        ImaginaryMaxes,
    }

    public Method method;

    public List<float> C1 = new List<float>();
    public List<float> C2 = new List<float>();

    public float C1Score;
    public float C2Score;

    void OnValidate()
    {
        int diffCount = Math.Max(C1.Count, C2.Count) - Math.Min(C1.Count, C2.Count);

        switch (method)
        {
            case Method.None:
                C1Score = 1f;
                C2Score = 1f;

                foreach (var item in C1)
                {
                    C1Score *= item;
                }

                foreach (var item in C2)
                {
                    C2Score *= item;
                }
                break;
            case Method.ImaginaryAverages:
                {
                    C1Score = 1f;
                    C2Score = 1f;

                    float avg1 = 0f;
                    foreach (var item in C1)
                    {
                        C1Score *= item;
                        avg1 += item;
                    }

                    float avg2 = 0f;
                    foreach (var item in C2)
                    {
                        C2Score *= item;
                        avg2 += item;
                    }

                    avg1 /= (float)C1.Count;
                    avg2 /= (float)C2.Count;

                    if(C1.Count < C2.Count)
                    {
                        C1Score *= math.pow(avg1, diffCount);
                    }
                    else if (C2.Count < C1.Count)
                    {
                        C2Score *= math.pow(avg2, diffCount);
                    }
                }
                break;
            case Method.DePowerify:
                {
                    C1Score = 1f;
                    C2Score = 1f;

                    foreach (var item in C1)
                    {
                        C1Score *= item;
                    }

                    foreach (var item in C2)
                    {
                        C2Score *= item;
                    }

                    C1Score = math.pow(C1Score, 1f / C1.Count);
                    C2Score = math.pow(C2Score, 1f / C2.Count);
                }
                break;
            case Method.InverseWeightedAverage:
                {
                    C1Score = 1f;
                    C2Score = 1f;

                    float a = 1f;
                    float b = 3f;

                    float avg1 = 0f;
                    float count1 = 0f;
                    foreach (var item in C1)
                    {
                        float addedCount = a / math.pow(item, b);
                        avg1 += (item * addedCount);
                        count1 += addedCount;
                    }

                    float avg2 = 0f;
                    float count2 = 0f;
                    foreach (var item in C2)
                    {
                        float addedCount = a / math.pow(item, b);
                        avg2 += (item * addedCount);
                        count2 += addedCount;
                    }

                    avg1 /= count1;
                    avg2 /= count2;

                    C1Score = avg1;
                    C2Score = avg2;
                }
                break;
            case Method.Official:

                break;
            case Method.ImaginaryMaxes:
                C1Score = 1f;
                C2Score = 1f;

                float max1 = float.MinValue;
                foreach (var item in C1)
                {
                    max1 = math.max(max1, item);
                    C1Score *= item;
                }

                float max2 = float.MinValue;
                foreach (var item in C2)
                {
                    max2 = math.max(max2, item);
                    C2Score *= item;
                }

                if (C1.Count < C2.Count)
                {
                    C1Score *= math.pow(max1, diffCount);
                }
                else if (C2.Count < C1.Count)
                {
                    C2Score *= math.pow(max2, diffCount);
                }
                break;
        }
    }
}
