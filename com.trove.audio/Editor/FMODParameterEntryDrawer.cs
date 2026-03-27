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

            SerializedProperty eventPathProperty = property.FindPropertyRelative("EventPath");
            SerializedProperty nameProperty = property.FindPropertyRelative("Name");
            SerializedProperty valueProperty = property.FindPropertyRelative("Value");
            
            // Get all paramRefs for this event
            List<EditorParamRef> paramRefs = new List<EditorParamRef>();
            if (eventPathProperty != null && !string.IsNullOrEmpty(eventPathProperty.stringValue))
            {
                EditorEventRef eventRef = EventManager.EventFromPath(eventPathProperty.stringValue);
                if (eventRef != null)
                {
                    paramRefs = eventRef.Parameters;
                }
            }
            
            // Row 1: readonly name field + search button
            VisualElement selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;

            Label nameField = new Label("Parameter");
            nameField.style.flexGrow = 1;
            nameField.text = (nameProperty.stringValue);
            nameField.TrackPropertyValue(nameProperty);

            Button searchButton = new Button { text = "..." };
            searchButton.style.width = 24;

            selectorRow.Add(nameField);
            selectorRow.Add(searchButton);
            container.Add(selectorRow);

            // Row 2: value control 
            VisualElement valueContainer = new VisualElement();
            container.Add(valueContainer);

            RefreshValueControl(valueContainer,
                valueProperty,
                nameProperty,
                paramRefs);
            
            searchButton.clicked += () =>
            {
                var dropdown = new ParameterDropdown(
                    new AdvancedDropdownState(),
                    paramRefs,
                    selected =>
                    {
                        nameProperty.stringValue = selected.Name;
                        valueProperty.floatValue = Mathf.Clamp(valueProperty.floatValue, selected.Min, selected.Max);
                        nameField.text = selected.Name;
                        RefreshValueControl(valueContainer,
                            valueProperty,
                            nameProperty,
                            paramRefs);
                        nameProperty.serializedObject.ApplyModifiedProperties();
                    });
                dropdown.Show(searchButton.worldBound);
            };

            return container;
        }

        private static void RefreshValueControl(
            VisualElement valueContainer, 
            SerializedProperty valueProperty, 
            SerializedProperty nameProperty,
            List<EditorParamRef> paramRefs)
        {
            valueContainer.Clear();
            EditorParamRef paramRef = paramRefs.Find(p => p.Name == nameProperty.stringValue);
            if (paramRef == null) 
                return;

            VisualElement valueControlElement;
            switch (paramRef.Type)
            {
                case ParameterType.Continuous:
                    Slider floatSlider = new Slider("Value", paramRef.Min, paramRef.Max);
                    floatSlider.showInputField = true;
                    floatSlider.SetValueWithoutNotify(valueProperty.floatValue);
                    floatSlider.RegisterValueChangedCallback(evt =>
                    {
                        valueProperty.floatValue = evt.newValue;
                        valueProperty.serializedObject.ApplyModifiedProperties();
                    });
                    valueControlElement = floatSlider;
                    break;
                case ParameterType.Discrete:
                    SliderInt slider = new SliderInt("Value", (int)paramRef.Min, (int)paramRef.Max);
                    slider.showInputField = true;
                    slider.SetValueWithoutNotify((int)valueProperty.floatValue);
                    slider.RegisterValueChangedCallback(evt =>
                    {
                        valueProperty.floatValue = evt.newValue;
                        valueProperty.serializedObject.ApplyModifiedProperties();
                    });
                    valueControlElement = slider;
                    break;
                case ParameterType.Labeled:
                    DropdownField dropdown = new DropdownField("Value", new List<string>(paramRef.Labels), (int)valueProperty.floatValue);
                    dropdown.RegisterValueChangedCallback(_ =>
                    {
                        valueProperty.floatValue = dropdown.index;
                        valueProperty.serializedObject.ApplyModifiedProperties();
                    });
                    valueControlElement = dropdown;
                    break;
                default:
                    valueControlElement = new VisualElement();
                    break;
            }

            valueContainer.Add(valueControlElement);
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
