using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Trove
{
    internal static class TemplatesCreator
    {
        // GUIDs are in the .meta file
        internal static readonly string EventSystemTemplate = "e98fd4e2740b355458fd8b32eb3ea2e4";

        [MenuItem("Assets/Create/ECS/EventSystem")]
        internal static void NewComponent()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(EventSystemTemplate);
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewEventSystem.cs");
        }
    }
}