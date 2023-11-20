﻿using System.Collections.Generic;

namespace PolymorphicElementsSourceGenerators
{
    public class CodeData
    {
        public List<GroupInterfaceData> GroupDatas = new List<GroupInterfaceData>();
    }

    public class GroupInterfaceData
    {
        public string Name;
        public string Namespace;
        public List<string> Usings = new List<string>();
        public List<FunctionData> FunctionDatas = new List<FunctionData>();
        public List<ElementData> ElementDatas = new List<ElementData>();

        public string GetGeneratedGroupName()
        {
            return $"{Name}{PESourceGenerator.GeneratedGroupSuffix}"; 
        }
    }

    public class FunctionData
    {
        public string Name;
        public string ReturnType;
        public List<GenericTypeData> GenericTypeDatas;
        public string GenericTypesString;
        public string GenericTypeConstraintsString;
        public bool ReturnTypeIsVoid;
        public MethodWriteBackType WriteBackType;
        public List<ParameterData> ParameterDatas = new List<ParameterData>();

        public void GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation)
        {
            parametersStringDeclaration = "";
            parametersStringInvocation = "";
            for (int i = 0; i < ParameterDatas.Count; i++)
            {
                if (i > 0)
                {
                    parametersStringDeclaration += ", ";
                    parametersStringInvocation += ", ";
                }

                ParameterData parameterData = ParameterDatas[i];
                parametersStringDeclaration += $"{parameterData.RefType} {parameterData.Type} {parameterData.Name}";
                parametersStringInvocation += $"{parameterData.RefType} {parameterData.Name}";
            }
        }
    }

    public class ParameterData
    {
        public string RefType;
        public string Type;
        public string Name;
    }

    public class ElementData
    {
        public ushort Id;
        public string Type;
        public bool IsPublicPartial;
    }

    public class GenericTypeData
    {
        public string Type;
        public List<string> TypeConstraints;
    }

    public enum MethodWriteBackType
    {
        None,
        Write,
        RefModify,
    }
}