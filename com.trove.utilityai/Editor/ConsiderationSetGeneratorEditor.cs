using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Trove.UtilityAI
{
    [CustomEditor(typeof(ConsiderationSetGenerator))]
    public class ConsiderationSetGeneratorEditor : Editor
    {
        const float SpaceHeight = 15f;
        const string ConsiderationSet = "ConsiderationSet";
        const string ConsiderationSetNoCap = "considerationSet";
        const string DataSuffix = "Data";
        const string AuthoringSuffix = "Authoring";
        const string DefinitionsSuffix = "Definitions";
        const string ConsiderationDefinition = "ConsiderationDefinition";
        const string ConsiderationDefinitionAuthoring = "ConsiderationDefinitionAuthoring";

        public override VisualElement CreateInspectorGUI()
        {
            ConsiderationSetGenerator considerationSetGenerator = (target as ConsiderationSetGenerator);

            VisualElement myInspector = new VisualElement();
            InspectorElement.FillDefaultInspector(myInspector, serializedObject, this);

            // Space
            VisualElement space1 = new VisualElement();
            space1.style.height = SpaceHeight;
            myInspector.Add(space1);

            // Generate preview
            VisualElement infoBox = new VisualElement();
            infoBox.style.backgroundColor = Color.black;
            infoBox.style.paddingTop = infoBox.style.paddingBottom = infoBox.style.paddingRight = infoBox.style.paddingLeft = 10f;
            Label infoLabel = new Label();
            infoBox.Add(infoLabel);
            myInspector.Add(infoBox);

            // Space
            VisualElement space2 = new VisualElement();
            space2.style.height = SpaceHeight;
            myInspector.Add(space2);

            // Generate button
            Button generateButton = new Button(() =>
            {
                bool allowGenerate = true;

                // Validate Namespace
                if (considerationSetGenerator.Namespace != String.Empty && !CodegenUtils.IsValidName(considerationSetGenerator.Namespace))
                {
                    allowGenerate = false;
                    Debug.LogWarning($"{ConsiderationSet}Generator warning: namespace \"{considerationSetGenerator.Namespace}\" is not a valid name");
                }

                // Validate Name
                if (!CodegenUtils.IsValidName(considerationSetGenerator.ConsiderationSetName) || considerationSetGenerator.ConsiderationSetName == $"{ConsiderationSet}" || considerationSetGenerator.ConsiderationSetName == "")
                {
                    allowGenerate = false;
                    Debug.LogWarning($"{ConsiderationSet}Generator warning: namespace \"{considerationSetGenerator.ConsiderationSetName}\" is not a valid name");
                }

                // Validate Considerations
                List<string> removedConsiderations = CodegenUtils.CheckDuplicatesAndInvalidNames(considerationSetGenerator.Considerations, CodegenUtils.CheckDuplicatesAndInvalidNamesAction.Remove);
                if (removedConsiderations.Count > 0)
                {
                    string removed = string.Empty;
                    foreach (var item in removedConsiderations)
                    {
                        removed += ((removed == string.Empty) ? " " : ", ") + item;
                    }
                    Debug.LogWarning($"{ConsiderationSet}Generator warning: the following duplicate or invalid Consideration names were removed: {removed}");
                }

                // Generate
                if (allowGenerate)
                {
                    GenerateFiles(considerationSetGenerator);
                }
                else
                {
                    Debug.LogError($"{ConsiderationSet}Generator error: generation aborted due to invalid fields, check warnings for more details");
                }
            });
            Label generateButtonLabel = new Label("Generate");
            generateButtonLabel.style.fontSize = 18f;
            generateButton.Add(generateButtonLabel);
            myInspector.Add(generateButton);

            // Redraw preview on data change
            PropertyField nameField = myInspector.Q<PropertyField>($"PropertyField:{ConsiderationSet}Name");
            PropertyField folderPathField = myInspector.Q<PropertyField>("PropertyField:FolderPathRelativeToAssets");
            nameField.RegisterValueChangeCallback((x) =>
            {
                RedrawInfoLabel(infoLabel, considerationSetGenerator);
            });
            folderPathField.RegisterValueChangeCallback((x) =>
            {
                RedrawInfoLabel(infoLabel, considerationSetGenerator);
            });

            return myInspector;
        }

        private void RedrawInfoLabel(Label infoLabel, ConsiderationSetGenerator considerationSetGenerator)
        {
            infoLabel.text = "Generated files preview:";
            infoLabel.text += "\n - Assets\\" + Path.Combine(considerationSetGenerator.FolderPathRelativeToAssets, GetDataFileName(considerationSetGenerator));
            infoLabel.text += "\n - Assets\\" + Path.Combine(considerationSetGenerator.FolderPathRelativeToAssets, GetAuthoringFileName(considerationSetGenerator));
            infoLabel.MarkDirtyRepaint();
        }

        private string GetGeneratedFilesFolderPath(ConsiderationSetGenerator generator)
        {
            return Path.Combine(Application.dataPath, generator.FolderPathRelativeToAssets);
        }

        private string GetDataFileName(ConsiderationSetGenerator generator)
        {
            return $"{ generator.ConsiderationSetName}{DataSuffix}.cs";
        }

        private string GetAuthoringFileName(ConsiderationSetGenerator generator)
        {
            return $"{generator.ConsiderationSetName}{AuthoringSuffix}.cs";
        }

        private void GenerateFiles(ConsiderationSetGenerator r)
        {
            // ===========================================================
            // DATA
            // ===========================================================
            {
                FileWriter dataWriter = new FileWriter(Path.Combine(Application.dataPath, GetGeneratedFilesFolderPath(r)), GetDataFileName(r));
                dataWriter.WriteLine($"using UnityEngine;");
                dataWriter.WriteLine($"using Unity.Entities;");
                dataWriter.WriteLine($"using Unity.Mathematics;");
                dataWriter.WriteLine($"using Unity.Collections;");
                dataWriter.WriteLine($"using System;");
                dataWriter.WriteLine($"using System.Collections.Generic;");
                dataWriter.WriteLine($"using Trove;"); ;
                dataWriter.WriteLine($"using Trove.UtilityAI;");
                dataWriter.WriteLine($"using Action = Trove.UtilityAI.Action;");
                dataWriter.WriteLine($"");
                dataWriter.WriteInNamespace(r.Namespace, () =>
                {
                    // Component referencing definitions
                    dataWriter.WriteLine($"[Serializable]");
                    dataWriter.WriteLine($"public struct {r.ConsiderationSetName} : IComponentData");
                    dataWriter.WriteInScope(() =>
                    {
                        foreach (var consideration in r.Considerations)
                        {
                            dataWriter.WriteLine($"public BlobAssetReference<{ConsiderationDefinition}> {consideration};");
                        }
                    });
                    dataWriter.WriteLine($"");
                    // Data scriptableObject
                    dataWriter.WriteLine($"[CreateAssetMenu(menuName = \"Trove/UtilityAI/{ConsiderationSet}s/{r.ConsiderationSetName}{DataSuffix}\", fileName = \"{r.ConsiderationSetName}{DataSuffix}\")]");
                    dataWriter.WriteLine($"public class {r.ConsiderationSetName}{DataSuffix} : ScriptableObject");
                    dataWriter.WriteInScope(() =>
                    {
                        // Consideration definitions
                        dataWriter.WriteLine($"[Header(\"Consideration Definitions\")]");
                        foreach (var consideration in r.Considerations)
                        {
                            dataWriter.WriteLine($"public {ConsiderationDefinitionAuthoring} {consideration}  = {ConsiderationDefinitionAuthoring}.GetDefault(0f, 1f);");
                        }
                        dataWriter.WriteLine($"");
                        // BeginBake
                        dataWriter.WriteLine($"public void Bake(IBaker baker, out {r.ConsiderationSetName} considerationSetComponent)");
                        dataWriter.WriteInScope(() =>
                        {
                            dataWriter.WriteLine($"considerationSetComponent = new {r.ConsiderationSetName}();");
                            foreach (var consideration in r.Considerations)
                            {
                                dataWriter.WriteLine($"considerationSetComponent.{consideration} = {consideration}.ToConsiderationDefinition(baker);");
                            }
                            dataWriter.WriteLine($"baker.AddComponent(baker.GetEntity(TransformUsageFlags.None), considerationSetComponent);");
                        });
                        dataWriter.WriteLine($"");
                    });
                });
                dataWriter.Finish(false);
            }

            AssetDatabase.Refresh();
        }
    }
}