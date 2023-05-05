using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Trove
{
    [CustomEditor(typeof(GenericCurve))]
    public class GenericCurveDrawer : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var container = new VisualElement();

            GenericCurve genericCurve = (target as GenericCurve);
            if (genericCurve.CurveEvaluator != null)
            {
                Color color = Color.black;
                color.a = 0.25f;
                container.style.backgroundColor = color;
                container.style.marginBottom = container.style.marginTop = container.style.marginRight = container.style.marginLeft = 5f;
                container.style.paddingBottom = container.style.paddingTop = container.style.paddingRight = container.style.paddingLeft = 10f;

                // Graph
                CurveDrawerElement curveDrawer = new CurveDrawerElement();
                curveDrawer.CurveEvaluator = genericCurve.CurveEvaluator;
                curveDrawer.Properties = genericCurve.GraphProperties;
                curveDrawer.ApplyProperties();
                container.Add(curveDrawer);

                // Graph Properties
                SerializedProperty graphPropertiesProperty = serializedObject.FindProperty("GraphProperties");
                PropertyField graphProperiesField = new PropertyField(graphPropertiesProperty);
                container.Add(graphProperiesField);

                genericCurve.OnValidateData += () =>
                {
                    curveDrawer.Properties = genericCurve.GraphProperties;
                    curveDrawer.ApplyProperties();
                    curveDrawer.MarkDirtyRepaint();
                };
            }

            return container;
        }
    }
}