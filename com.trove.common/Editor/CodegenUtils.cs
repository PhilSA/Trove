using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Trove
{
    public static class CodegenUtils
    {
        public enum CheckDuplicatesAndInvalidNamesAction
        {
            Identify,
            Clear,
            Remove,
        }

        public static bool IsValidName(string n)
        {
            CodeDomProvider provider = CodeDomProvider.CreateProvider("C#");
            return provider.IsValidIdentifier(n);
        }

        public static List<string> CheckDuplicatesAndInvalidNames(List<string> inList, CheckDuplicatesAndInvalidNamesAction action)
        {
            List<string> removedItems = new List<string>();

            for (int i = inList.Count - 1; i >= 0; i--)
            {
                string name = inList[i];

                if (!CodegenUtils.IsValidName(name))
                {
                    removedItems.Add(name);
                    if (action == CheckDuplicatesAndInvalidNamesAction.Clear)
                    {
                        inList[i] = String.Empty;
                    }
                    else if (action == CheckDuplicatesAndInvalidNamesAction.Remove)
                    {
                        inList.RemoveAt(i);
                    }
                }
                else
                {
                    bool foundDuplicate = false;
                    for (int j = 0; j < i; j++)
                    {
                        if (inList[j] == name)
                        {
                            foundDuplicate = true;
                        }
                    }

                    if(foundDuplicate)
                    {
                        removedItems.Add(name);
                        if (action == CheckDuplicatesAndInvalidNamesAction.Clear)
                        {
                            inList[i] = String.Empty;
                        }
                        else if (action == CheckDuplicatesAndInvalidNamesAction.Remove)
                        {
                            inList.RemoveAt(i);
                        }
                    }
                }
            }

            return removedItems;
        }

        public static string GetDefaultGeneratedFilesFolderPath()
        {
            string folderPath = Application.dataPath + "/";
            Directory.CreateDirectory(folderPath);
            return folderPath;
        }

        public static List<Type> ScanInterfaceTypesWithAttributes(Type attributeType)
        {
            var types = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] allAssemblyTypes;
                try
                {
                    allAssemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    allAssemblyTypes = e.Types;
                }
                var myTypes = allAssemblyTypes.Where(t => t.IsInterface && Attribute.IsDefined(t, attributeType, true));
                types.AddRange(myTypes);
            }
            return types;
        }

        public static List<Type> ScanStructTypesImplementingInterface(Type interfaceType)
        {
            var types = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] allAssemblyTypes;
                try
                {
                    allAssemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    allAssemblyTypes = e.Types;
                }

                var myTypes = allAssemblyTypes.Where(t => t.IsValueType && interfaceType.IsAssignableFrom(t));
                types.AddRange(myTypes);
            }
            return types;
        }

        public static void GetUniqueFieldTypesRecursive(Type t, ref List<Type> fieldTypes)
        {
            var fields = t.GetFields();
            foreach (FieldInfo f in fields)
            {
                if (!fieldTypes.Contains(f.FieldType))
                {
                    fieldTypes.Add(f.FieldType);
                    GetUniqueFieldTypesRecursive(f.FieldType, ref fieldTypes);
                }
            }
        }

        public static bool IsByRef(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.IsByRef && !parameterInfo.IsOut && !parameterInfo.IsIn;
        }

        public static string GetParameterRefKeyword(ParameterInfo parameterInfo)
        {
            string s = "";
            if (parameterInfo.ParameterType.IsByRef)
            {
                if (parameterInfo.IsOut)
                {
                    s += "out ";
                }
                else if (parameterInfo.IsIn)
                {
                    s += "in ";
                }
                else
                {
                    s += "ref ";
                }
            }

            return s;
        }

        public static string GetTypeName(Type t)
        {
            return t.ToString().Replace("&", "").Replace("`1", "").Replace("[", "<").Replace("]", ">").Replace("+", ".");
        }
    }
}