using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Trove
{
    internal static class TemplatesCreator
    {
        // GUIDs are in the .meta file
        internal static readonly string StreamEventSystemTemplate = "0f784fb30c5220f489b898dee096501c";

        [MenuItem("Assets/Create/Trove/StreamEventSystem")]
        internal static void NewStreamEventSystem()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(StreamEventSystemTemplate);
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewStreamEventSystem.cs");
        }
    }
}