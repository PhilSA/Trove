using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Trove
{
    internal static class TemplatesCreator
    {
        // GUIDs are in the .meta file
        internal static readonly string ComponentTemplate = "c6ae1b97fcfea5f44b65dd7c16df1747";
        internal static readonly string AuthoringTemplate = "f4abf72bf1e3d8e4bb444cbed495b9f3";
        internal static readonly string SystemTemplate = "f3ae3995ab21bf54cae8cc6e6f99f8a3";

        [MenuItem("Assets/Create/ECS/Component")]
        internal static void NewComponent()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(ComponentTemplate);
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewComponent.cs");
        }

        [MenuItem("Assets/Create/ECS/Authoring")]
        internal static void NewAuthoring()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(AuthoringTemplate);
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewAuthoring.cs");
        }

        [MenuItem("Assets/Create/ECS/System")]
        internal static void NewSystem()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(SystemTemplate);
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewSystem.cs");
        }
    }
}