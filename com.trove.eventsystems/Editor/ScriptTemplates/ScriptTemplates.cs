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

        [MenuItem("Assets/Create/Trove/EventSystems/Global Polymorphic Event", priority = 3)]
        static void NewGlobalPolymorphicEvent()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}GlobalPolymorphicEventTemplate.txt", "NewEvent.cs");
        }

        [MenuItem("Assets/Create/Trove/EventSystems/Entity Polymorphic Event", priority = 4)]
        static void NewEntityPolymorphicEvent()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}EntityPolymorphicEventTemplate.txt", "NewEvent.cs");
        }
    }
}