using UnityEditor;
using UnityEngine;

namespace Trove.EventSystems
{
    static class ScriptTemplates
    {
        public const string ScriptTemplatePath = "Packages/com.trove.statemachines/Editor/ScriptTemplates/";

        [MenuItem("Assets/Create/Trove/StateMachines/New State Machine", priority = 1)]
        static void NewGlobalEvent()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplatePath}StateMachineTemplate.txt", "NewStateMachine.cs");
        }
    }
}