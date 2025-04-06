using UnityEditor;
using UnityEngine;

namespace Trove.EventSystems
{
    static class ScriptTemplates
    {
        public const string ScriptTemplatePath = "Packages/com.trove.eventsystems/Editor/ScriptTemplates/";

        [MenuItem("Assets/Create/Trove/EventSystems/Global Event", priority = 1)]
        static void NewGlobalEvent()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}GlobalEventTemplate.txt", "NewEvent.cs");
        }

        [MenuItem("Assets/Create/Trove/EventSystems/Entity Event", priority = 2)]
        static void NewEntityEvent()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}EntityEventTemplate.txt", "NewEvent.cs");
        }

#if HAS_TROVE_POLYMORPHICSTRUCTS
        [MenuItem("Assets/Create/Trove/EventSystems/Global PolyByteArray Event", priority = 3)]
        static void NewGlobalPolyByteArrayEvent()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}GlobalPolyByteArrayEventTemplate.txt", "NewEvent.cs");
        }

        [MenuItem("Assets/Create/Trove/EventSystems/Entity PolyByteArray Event", priority = 4)]
        static void NewEntityPolyByteArrayEvent()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}EntityPolyByteArrayEventTemplate.txt", "NewEvent.cs");
        }
#endif
    }
}