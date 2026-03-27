using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DOTSFMOD.Editor
{
    // TODO: clean this up
    [CustomPropertyDrawer(typeof(FMODEmitterAuthoring.FMODParameterEntry))]
    public class FMODParameterEntryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement container = new VisualElement();

            SerializedProperty nameProperty = property.FindPropertyRelative("Name");
            SerializedProperty valueProperty = property.FindPropertyRelative("Value");

            // Row 1: readonly name field + search button
            VisualElement selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;

            Label nameField = new Label("Parameter");
            nameField.style.flexGrow = 1;
            nameField.text = (nameProperty.stringValue);

            Button searchButton = new Button { text = "..." };
            searchButton.style.width = 24;

            selectorRow.Add(nameField);
            selectorRow.Add(searchButton);
            container.Add(selectorRow);

            // Row 2: value control (rebuilt whenever a new param is selected)
            VisualElement valueContainer = new VisualElement();
            container.Add(valueContainer);
            
            searchButton.clicked += () =>
            {
                List<EditorParamRef> paramRefs = GetEventRefsList(property);
                var dropdown = new ParameterDropdown(
                    new AdvancedDropdownState(),
                    paramRefs,
                    selected =>
                    {
                        nameProperty.stringValue = selected.Name;
                        valueProperty.floatValue = Mathf.Clamp(valueProperty.floatValue, selected.Min, selected.Max);
                        nameField.text = selected.Name;
                        RefreshValueControl(valueContainer,
                            property,
                            valueProperty,
                            nameProperty);
                        nameProperty.serializedObject.ApplyModifiedProperties();
                        
                        nameProperty = property.FindPropertyRelative("Name");
                        Debug.Log($"new name: {nameProperty.stringValue}");
                        
                        container.MarkDirtyRepaint();
                    });
                dropdown.Show(searchButton.worldBound);
            };

            RefreshValueControl(valueContainer,
                property,
                valueProperty,
                nameProperty);

            return container;
        }

        private static void RefreshValueControl(VisualElement valueContainer, 
            SerializedProperty property, 
            SerializedProperty valueProperty, 
            SerializedProperty nameProperty)
        {
            valueContainer.Clear();
            List<EditorParamRef> paramRefs = GetEventRefsList(property);
            EditorParamRef paramRef = paramRefs.Find(p => p.Name == nameProperty.stringValue);
            if (paramRef == null) return;

            valueContainer.Add(CreateValueControl(valueProperty, paramRef));
        }

        private static VisualElement CreateValueControl(SerializedProperty valueProperty, EditorParamRef paramRef)
        {
            if (paramRef.Type == ParameterType.Labeled)
            {
                var dropdown = new DropdownField("Value", new List<string>(paramRef.Labels), (int)valueProperty.floatValue);
                dropdown.RegisterValueChangedCallback(_ =>
                {
                    valueProperty.floatValue = dropdown.index;
                    valueProperty.serializedObject.ApplyModifiedProperties();
                });
                return dropdown;
            }

            if (paramRef.Type == ParameterType.Discrete)
            {
                var slider = new SliderInt("Value", (int)paramRef.Min, (int)paramRef.Max);
                slider.showInputField = true;
                slider.SetValueWithoutNotify((int)valueProperty.floatValue);
                slider.RegisterValueChangedCallback(evt =>
                {
                    valueProperty.floatValue = evt.newValue;
                    valueProperty.serializedObject.ApplyModifiedProperties();
                });
                return slider;
            }

            // Continuous
            var floatSlider = new Slider("Value", paramRef.Min, paramRef.Max);
            floatSlider.showInputField = true;
            floatSlider.SetValueWithoutNotify(valueProperty.floatValue);
            floatSlider.RegisterValueChangedCallback(evt =>
            {
                valueProperty.floatValue = evt.newValue;
                valueProperty.serializedObject.ApplyModifiedProperties();
            });
            return floatSlider;
        }

        private static List<EditorParamRef> GetEventRefsList(SerializedProperty property)
        {
            SerializedProperty eventRefProp = property.serializedObject.FindProperty("EventReference");
            if (eventRefProp != null)
            {
                SerializedProperty pathProp = eventRefProp.FindPropertyRelative("Path");
                if (pathProp != null && !string.IsNullOrEmpty(pathProp.stringValue))
                {
                    EditorEventRef eventRef = EventManager.EventFromPath(pathProp.stringValue);
                    if (eventRef != null)
                    {
                        List<EditorParamRef> eventRefs = new List<EditorParamRef>();
                        foreach (EditorParamRef param in eventRef.Parameters)
                        {
                            eventRefs.Add(param);
                        }
                        return eventRefs;
                    }
                }
            }

            // Fallback: return all available global parameters
            return new List<EditorParamRef>(EventManager.Parameters);
        }

        private class ParameterDropdown : AdvancedDropdown
        {
            private readonly List<EditorParamRef> paramRefs;
            private readonly Action<EditorParamRef> onSelected;

            public ParameterDropdown(AdvancedDropdownState state, List<EditorParamRef> paramRefs, Action<EditorParamRef> onSelected)
                : base(state)
            {
                this.paramRefs = paramRefs;
                this.onSelected = onSelected;
                minimumSize = new Vector2(200, 300);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Parameters");
                foreach (EditorParamRef paramRef in paramRefs)
                {
                    root.AddChild(new ParameterDropdownItem(paramRef));
                }
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is ParameterDropdownItem paramItem)
                {
                    onSelected(paramItem.ParamRef);
                }
            }
        }

        private class ParameterDropdownItem : AdvancedDropdownItem
        {
            public EditorParamRef ParamRef { get; }

            public ParameterDropdownItem(EditorParamRef paramRef) : base(paramRef.Name)
            {
                ParamRef = paramRef;
            }
        }
    }
}
