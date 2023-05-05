using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Trove
{
    public class CurveDrawerElement : VisualElement
    {
        public Func<float, float> CurveEvaluator;
        public CurveGraphProperties Properties = CurveGraphProperties.GetDefault();

        public new class UxmlFactory : UxmlFactory<CurveDrawerElement, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlStringAttributeDescription m_String = new UxmlStringAttributeDescription { name = "string-attr", defaultValue = "default_value" };
            UxmlIntAttributeDescription m_Int = new UxmlIntAttributeDescription { name = "int-attr", defaultValue = 2 };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var ate = ve as CurveDrawerElement;

                ate.stringAttr = m_String.GetValueFromBag(bag, cc);
                ate.intAttr = m_Int.GetValueFromBag(bag, cc);
            }
        }

        public string stringAttr { get; set; }
        public int intAttr { get; set; }

        public CurveDrawerElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        public void ApplyProperties()
        {
            this.style.backgroundColor = Properties.BackgroundColor;
            if (Properties.GraphWidth < 0f)
            {
                this.style.flexGrow = 1f;
            }
            else
            {
                this.style.width = Properties.GraphWidth;
            }
            this.style.height = math.clamp(Properties.GraphHeight, 0f, float.MaxValue);
        }

        void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (CurveEvaluator != null)
            {
                Painter2D paint2D = ctx.painter2D;
                paint2D.lineJoin = LineJoin.Round;
                paint2D.lineCap = LineCap.Round;

                // Secondary grid
                {
                    paint2D.strokeColor = Properties.MinorGridColor;
                    paint2D.lineWidth = Properties.MinorGridLineWidth;

                    // Horizontal
                    float gridCounter = math.floor(Properties.Min.y * (1f / Properties.MinorGridIncrements)) * Properties.MinorGridIncrements;
                    while (gridCounter < Properties.Max.y)
                    {
                        if (GetPixelCoordOfGraphCoord(new float2(Properties.Min.x, gridCounter), out float2 x0) &&
                            GetPixelCoordOfGraphCoord(new float2(Properties.Max.x, gridCounter), out float2 x1))
                        {
                            paint2D.BeginPath();
                            paint2D.MoveTo(x0);
                            paint2D.LineTo(x1);
                            paint2D.Stroke();
                        }

                        gridCounter += Properties.MinorGridIncrements;
                    }

                    // Vertical
                    gridCounter = math.floor(Properties.Min.x * (1f / Properties.MinorGridIncrements)) * Properties.MinorGridIncrements;
                    while (gridCounter < Properties.Max.x)
                    {
                        if (GetPixelCoordOfGraphCoord(new float2(gridCounter, Properties.Min.y), out float2 y0) &&
                            GetPixelCoordOfGraphCoord(new float2(gridCounter, Properties.Max.y), out float2 y1))
                        {
                            paint2D.BeginPath();
                            paint2D.MoveTo(y0);
                            paint2D.LineTo(y1);
                            paint2D.Stroke();
                        }

                        gridCounter += Properties.MinorGridIncrements;
                    }
                }

                // Primary grid
                {
                    paint2D.strokeColor = Properties.MajorGridColor;
                    paint2D.lineWidth = Properties.MajorGridLineWidth;

                    // Horizontal
                    float gridCounter = math.floor(Properties.Min.y * (1f / Properties.MajorGridIncrements)) * Properties.MajorGridIncrements;
                    while (gridCounter < Properties.Max.y)
                    {
                        if (GetPixelCoordOfGraphCoord(new float2(Properties.Min.x, gridCounter), out float2 x0) &&
                            GetPixelCoordOfGraphCoord(new float2(Properties.Max.x, gridCounter), out float2 x1))
                        {
                            paint2D.BeginPath();
                            paint2D.MoveTo(x0);
                            paint2D.LineTo(x1);
                            paint2D.Stroke();
                        }

                        gridCounter += Properties.MajorGridIncrements;
                    }

                    // Vertical
                    gridCounter = math.floor(Properties.Min.x * (1f / Properties.MajorGridIncrements)) * Properties.MajorGridIncrements;
                    while (gridCounter < Properties.Max.x)
                    {
                        if (GetPixelCoordOfGraphCoord(new float2(gridCounter, Properties.Min.y), out float2 y0) &&
                            GetPixelCoordOfGraphCoord(new float2(gridCounter, Properties.Max.y), out float2 y1))
                        {
                            paint2D.BeginPath();
                            paint2D.MoveTo(y0);
                            paint2D.LineTo(y1);
                            paint2D.Stroke();
                        }

                        gridCounter += Properties.MajorGridIncrements;
                    }
                }

                // Main axis
                {
                    paint2D.strokeColor = Properties.MainAxisColor;
                    paint2D.lineWidth = Properties.MainAxisLineWidth;

                    // Horizontal
                    if (GetPixelCoordOfGraphCoord(new float2(Properties.Min.x, 0f), out float2 x0) &&
                        GetPixelCoordOfGraphCoord(new float2(Properties.Max.x, 0f), out float2 x1))
                    {
                        paint2D.BeginPath();
                        paint2D.MoveTo(x0);
                        paint2D.LineTo(x1);
                        paint2D.Stroke();
                    }

                    // Vertical
                    if (GetPixelCoordOfGraphCoord(new float2(0f, Properties.Min.y), out float2 y0) &&
                        GetPixelCoordOfGraphCoord(new float2(0f, Properties.Max.y), out float2 y1))
                    {
                        paint2D.BeginPath();
                        paint2D.MoveTo(y0);
                        paint2D.LineTo(y1);
                        paint2D.Stroke();
                    }
                }

                // Curve
                {
                    paint2D.strokeColor = Properties.CurveColor;
                    paint2D.lineWidth = Properties.CurveLineWidth;

                    paint2D.BeginPath();
                    paint2D.MoveTo(new Vector2(0f, EvaluateCurveForPixel(0f)));

                    int pixelWidth = (int)this.resolvedStyle.width;
                    for (int i = 0; i <= pixelWidth; i++)
                    {
                        Vector2 pixelCoord = new Vector2(i, EvaluateCurveForPixel(i));
                        GetGraphCoordOfPixelCoord(pixelCoord, out float2 graphCoord);
                        paint2D.LineTo(pixelCoord);
                    }

                    paint2D.LineTo(new Vector2(this.resolvedStyle.width, EvaluateCurveForPixel(this.resolvedStyle.width)));
                    paint2D.Stroke();
                }
            }
        }

        bool GetPixelCoordOfGraphCoord(float2 graphCoord, out float2 pixelCoord)
        {
            float coordWidth = Properties.Max.x - Properties.Min.x;
            float coordHeight = Properties.Max.y - Properties.Min.y;
            float pixelWidth = this.resolvedStyle.width;
            float pixelHeight = this.resolvedStyle.height;

            float2 coordRatioInVisible = new float2
            {
                x = (graphCoord.x - Properties.Min.x) / coordWidth,
                y = (graphCoord.y - Properties.Min.y) / coordHeight,
            };

            pixelCoord = new float2
            {
                x = coordRatioInVisible.x * pixelWidth,
                y = pixelHeight - (coordRatioInVisible.y * pixelHeight),
            };

            pixelCoord.x = math.clamp(pixelCoord.x, 0f, pixelWidth);
            pixelCoord.y = math.clamp(pixelCoord.y, 0f, pixelHeight);

            return true;
        }

        bool GetGraphCoordOfPixelCoord(float2 pixelCoord, out float2 graphCoord)
        {
            float coordWidth = Properties.Max.x - Properties.Min.x;
            float coordHeight = Properties.Max.y - Properties.Min.y;
            float pixelWidth = this.resolvedStyle.width;
            float pixelHeight = this.resolvedStyle.height;

            pixelCoord.y = pixelHeight - pixelCoord.y;

            float2 coordRatioInVisible = new float2
            {
                x = pixelCoord.x / pixelWidth,
                y = pixelCoord.y / pixelHeight,
            };

            graphCoord = new float2
            {
                x = Properties.Min.x + (coordRatioInVisible.x * coordWidth),
                y = Properties.Min.y + (coordRatioInVisible.y * coordHeight),
            };

            return true;
        }

        float EvaluateCurveForPixel(float pixelX)
        {
            if (GetGraphCoordOfPixelCoord(new float2(pixelX, 0f), out float2 graphCoord))
            {
                graphCoord.y = CurveEvaluator.Invoke(graphCoord.x);
                if (GetPixelCoordOfGraphCoord(graphCoord, out float2 pixelCoord))
                {
                    return pixelCoord.y;
                }
            }

            return 0f;
        }
    }
}