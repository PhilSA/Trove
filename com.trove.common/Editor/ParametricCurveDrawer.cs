using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Trove
{
    [CustomPropertyDrawer(typeof(ParametricCurveAuthoring))]
    public class ParametricCurveDrawer : PropertyDrawer
    {
        private ParametricCurve _parametricCurve = new ParametricCurve();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            Color color = Color.black;
            color.a = 0.25f;
            container.style.backgroundColor = color;
            container.style.marginBottom = container.style.marginTop = container.style.marginRight = container.style.marginLeft = 5f;
            container.style.paddingBottom = container.style.paddingTop = container.style.paddingRight = container.style.paddingLeft = 10f;

            // Graph
            CurveDrawerElement curveDrawer = new CurveDrawerElement();
            curveDrawer.CurveEvaluator = (x) => { return _parametricCurve.Evaluate(x); };
            curveDrawer.ApplyProperties();
            container.Add(curveDrawer);

            // Graph properties
            {
                SerializedProperty graphPropertiesProperty = property.FindPropertyRelative("GraphProperties");

                Foldout graphPropertiesFoldout = new Foldout();
                graphPropertiesFoldout.text = "Graph Properties";
                AddGraphFloat2Field("Min coords", "Min", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                {
                    x.x = Mathf.Clamp(x.x, -100f, 100f);
                    x.y = Mathf.Clamp(x.y, -100f, 100f);
                    curveDrawer.Properties.Min = x;
                });
                AddGraphFloat2Field("Max coords", "Max", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                {
                    x.x = Mathf.Clamp(x.x, -100f, 100f);
                    x.y = Mathf.Clamp(x.y, -100f, 100f);
                    curveDrawer.Properties.Max = x;
                });
                AddGraphFloatField("Major grid increments", "MajorGridIncrements", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                {
                    x = Mathf.Clamp(x, 0.1f, 100f); 
                    curveDrawer.Properties.MajorGridIncrements = x;
                });
                AddGraphFloatField("Minor grid increments", "MinorGridIncrements", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                {
                    x = Mathf.Clamp(x, 0.1f, 100f);
                    curveDrawer.Properties.MinorGridIncrements = x;
                });
                //AddGraphColorField("Bachground color", "BackgroundColor", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                //{
                //    curveDrawer.Properties.BackgroundColor = x;
                //});
                //AddGraphColorField("Curve color", "CurveColor", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                //{
                //    curveDrawer.Properties.CurveColor = x;
                //});
                //AddGraphColorField("Main axis color", "MainAxisColor", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                //{
                //    curveDrawer.Properties.MainAxisColor = x;
                //});
                //AddGraphColorField("Major grid color", "MajorGridColor", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                //{
                //    curveDrawer.Properties.MajorGridColor = x;
                //});
                //AddGraphColorField("Minor grid color", "MinorGridColor", graphPropertiesProperty, graphPropertiesFoldout, curveDrawer, (x) =>
                //{
                //    curveDrawer.Properties.MinorGridColor = x;
                //});
                container.Add(graphPropertiesFoldout);
            }

            // Parametric curve
            {
                SerializedProperty parametricCurveProperty = property.FindPropertyRelative("ParametricCurve");
                SerializedProperty defaultMinYProperty = property.FindPropertyRelative("DefaultMinY");
                SerializedProperty defaultMaxYProperty = property.FindPropertyRelative("DefaultMaxY");

                SerializedProperty curveTypeProperty = parametricCurveProperty.FindPropertyRelative("CurveType");
                SerializedProperty shapeProperty = parametricCurveProperty.FindPropertyRelative("Shape");
                SerializedProperty slopeProperty = parametricCurveProperty.FindPropertyRelative("Slope");
                SerializedProperty verticalShiftProperty = parametricCurveProperty.FindPropertyRelative("VerticalShift");
                SerializedProperty horizontalShiftProperty = parametricCurveProperty.FindPropertyRelative("HorizontalShift");
                SerializedProperty minYProperty = parametricCurveProperty.FindPropertyRelative("MinY");
                SerializedProperty maxYProperty = parametricCurveProperty.FindPropertyRelative("MaxY");

                PropertyField curveTypeField = new PropertyField(curveTypeProperty);
                PropertyField shapeField = new PropertyField(shapeProperty);
                PropertyField slopeField = new PropertyField(slopeProperty);
                PropertyField verticalShiftField = new PropertyField(verticalShiftProperty);
                PropertyField horizontalShiftField = new PropertyField(horizontalShiftProperty);
                PropertyField minYField = new PropertyField(minYProperty);
                PropertyField maxYField = new PropertyField(maxYProperty);

                _parametricCurve.CurveType = (ParametricCurveType)curveTypeProperty.intValue;
                _parametricCurve.Shape = shapeProperty.floatValue;
                _parametricCurve.Slope = slopeProperty.floatValue;
                _parametricCurve.VerticalShift = verticalShiftProperty.floatValue;
                _parametricCurve.HorizontalShift = horizontalShiftProperty.floatValue;
                _parametricCurve.MinY = minYProperty.floatValue;
                _parametricCurve.MaxY = maxYProperty.floatValue;

                curveTypeField.RegisterValueChangeCallback(x =>
                {
                    ParametricCurveType newType = (ParametricCurveType)x.changedProperty.intValue;

                    // Handle changing defaults
                    if(newType != _parametricCurve.CurveType)
                    {
                        SerializedProperty defaultMinYProperty = property.FindPropertyRelative("DefaultMinY");
                        SerializedProperty defaultMaxYProperty = property.FindPropertyRelative("DefaultMaxY");

                        _parametricCurve = ParametricCurve.GetDefault(newType, defaultMinYProperty.floatValue, defaultMaxYProperty.floatValue);

                        SerializedProperty curveTypeProperty = parametricCurveProperty.FindPropertyRelative("CurveType");
                        SerializedProperty shapeProperty = parametricCurveProperty.FindPropertyRelative("Shape");
                        SerializedProperty slopeProperty = parametricCurveProperty.FindPropertyRelative("Slope");
                        SerializedProperty verticalShiftProperty = parametricCurveProperty.FindPropertyRelative("VerticalShift");
                        SerializedProperty horizontalShiftProperty = parametricCurveProperty.FindPropertyRelative("HorizontalShift");
                        SerializedProperty minYProperty = parametricCurveProperty.FindPropertyRelative("MinY");
                        SerializedProperty maxYProperty = parametricCurveProperty.FindPropertyRelative("MaxY");

                        curveTypeProperty.intValue = (int)newType;
                        shapeProperty.floatValue = _parametricCurve.Shape;
                        slopeProperty.floatValue = _parametricCurve.Slope;
                        verticalShiftProperty.floatValue = _parametricCurve.VerticalShift;
                        horizontalShiftProperty.floatValue = _parametricCurve.HorizontalShift;
                        minYProperty.floatValue = _parametricCurve.MinY;
                        maxYProperty.floatValue = _parametricCurve.MaxY;

                        shapeProperty.serializedObject.ApplyModifiedProperties();
                        slopeProperty.serializedObject.ApplyModifiedProperties();
                        verticalShiftProperty.serializedObject.ApplyModifiedProperties();
                        horizontalShiftProperty.serializedObject.ApplyModifiedProperties();
                        minYProperty.serializedObject.ApplyModifiedProperties();
                        maxYProperty.serializedObject.ApplyModifiedProperties();
                    }

                    curveDrawer.MarkDirtyRepaint();
                });
                shapeField.RegisterValueChangeCallback(x =>
                {
                    _parametricCurve.Shape = x.changedProperty.floatValue;
                    curveDrawer.MarkDirtyRepaint();
                });
                slopeField.RegisterValueChangeCallback(x =>
                {
                    _parametricCurve.Slope = x.changedProperty.floatValue;
                    curveDrawer.MarkDirtyRepaint();
                });
                verticalShiftField.RegisterValueChangeCallback(x =>
                {
                    _parametricCurve.VerticalShift = x.changedProperty.floatValue;
                    curveDrawer.MarkDirtyRepaint();
                });
                horizontalShiftField.RegisterValueChangeCallback(x =>
                {
                    _parametricCurve.HorizontalShift = x.changedProperty.floatValue;
                    curveDrawer.MarkDirtyRepaint();
                });
                minYField.RegisterValueChangeCallback(x =>
                {
                    _parametricCurve.MinY = x.changedProperty.floatValue;
                    curveDrawer.MarkDirtyRepaint();
                });
                maxYField.RegisterValueChangeCallback(x =>
                {
                    _parametricCurve.MaxY = x.changedProperty.floatValue;
                    curveDrawer.MarkDirtyRepaint();
                });

                container.Add(curveTypeField);
                container.Add(shapeField);
                container.Add(slopeField);
                container.Add(verticalShiftField);
                container.Add(horizontalShiftField);
                container.Add(minYField);
                container.Add(maxYField);
            }

            property.serializedObject.ApplyModifiedProperties();

            return container;
        }

        private void AddGraphFloatField(string displayName, string propName, SerializedProperty parentProp, VisualElement parentElement, CurveDrawerElement curveDrawer, System.Action<float> setter)
        {
            SerializedProperty property = parentProp.FindPropertyRelative(propName);

            FloatField field = new FloatField();
            field.label = displayName;
            field.Bind(property.serializedObject);
            field.value = property.floatValue;
            field.RegisterValueChangedCallback((x) =>
            {
                setter.Invoke(x.newValue);
                property.floatValue = x.newValue;
                property.serializedObject.ApplyModifiedProperties();
                curveDrawer.ApplyProperties();
                curveDrawer.MarkDirtyRepaint();
            });
            parentElement.Add(field);

            setter.Invoke(property.floatValue);
            property.serializedObject.ApplyModifiedProperties();
            curveDrawer.ApplyProperties();
            curveDrawer.MarkDirtyRepaint();
        }

        private void AddGraphFloat2Field(string displayName, string propName, SerializedProperty parentProp, VisualElement parentElement, CurveDrawerElement curveDrawer, System.Action<Vector2> setter)
        {
            SerializedProperty property = parentProp.FindPropertyRelative(propName);

            Vector2Field field = new Vector2Field();
            field.label = displayName;
            field.Bind(property.serializedObject);
            field.value = property.vector2Value;
            field.RegisterValueChangedCallback((x) =>
            {
                setter.Invoke(x.newValue);
                property.vector2Value = x.newValue;
                property.serializedObject.ApplyModifiedProperties();
                curveDrawer.ApplyProperties();
                curveDrawer.MarkDirtyRepaint();
            });
            parentElement.Add(field);

            setter.Invoke(property.vector2Value);
            property.serializedObject.ApplyModifiedProperties();
            curveDrawer.ApplyProperties();
            curveDrawer.MarkDirtyRepaint();
        }

        private void AddGraphColorField(string displayName, string propName, SerializedProperty parentProp, VisualElement parentElement, CurveDrawerElement curveDrawer, System.Action<Color> setter)
        {
            SerializedProperty property = parentProp.FindPropertyRelative(propName);

            ColorField field = new ColorField();
            field.label = displayName;
            field.Bind(property.serializedObject);
            field.value = property.colorValue;
            field.RegisterValueChangedCallback((x) =>
            {
                setter.Invoke(x.newValue);
                property.colorValue = x.newValue;
                property.serializedObject.ApplyModifiedProperties();
                curveDrawer.ApplyProperties();
                curveDrawer.MarkDirtyRepaint();
            });
            parentElement.Add(field);

            setter.Invoke(property.colorValue);
            property.serializedObject.ApplyModifiedProperties();
            curveDrawer.ApplyProperties();
            curveDrawer.MarkDirtyRepaint();
        }
    }
}