using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Trove.UtilityAI
{ 
    [CreateAssetMenu(menuName = "Trove/UtilityAI/ConsiderationSetGenerator", fileName = "NewConsiderationSetGenerator")]
    public class ConsiderationSetGenerator : ScriptableObject
    {
        public string Namespace = "";
        public string ConsiderationSetName = "NewConsiderationSet";
        public string FolderPathRelativeToAssets = "_GENERATED";
        public List<string> Considerations = new List<string>();
    }
}