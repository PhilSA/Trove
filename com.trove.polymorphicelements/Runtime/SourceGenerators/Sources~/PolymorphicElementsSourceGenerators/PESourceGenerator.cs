using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace PolymorphicElementsSourceGenerators
{
    [Generator]
    public class PESourceGenerator : ISourceGenerator
    {
        public const string VoidType = "void";
        public const string GeneratedGroupSuffix = "Manager";
        private const string MethodWriteBackAttributeName = "AllowElementModification";
        private const string MethodRefWriteBackAttributeName = "AllowElementModificationByRefUnsafe";
        private const string PolymorphicElementsUtility = "PolymorphicElementsUtility";
        private const string ElementTypeEnumName = "ElementType";
        private const string PolymorphicElementMetaData = "PolymorphicElementMetaData";
        private const string GetElementType = "GetElementType";
        private const string GetElementSizeWithoutTypeId = "GetElementSizeWithoutTypeId";
        private const string GetLargestElementSizeWithTypeId = "GetLargestElementSizeWithTypeId";
        private const string SizeOfElementTypeId = "SizeOfElementTypeId";
        private const string GetNextElementMetaData = "GetNextElementMetaData";
        private const string AppendElement = "AppendElement";
        private const string AddElement = "AddElement";
        private const string ExecuteFunction = "Execute";
        private const string ReferenceOf = "ReferenceOf";
        private const string UnionElement = "UnionElement";
        private const string InternalUse = "InternalUse";
        private const string ReadAny = "ReadAny";
        private const string ReadAnyAsRef = "ReadAnyAsRef";
        private const string WriteAny = "WriteAny";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new PESyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            CodeData codeData = new CodeData();
            PESyntaxReceiver syntaxReceiver = (PESyntaxReceiver)context.SyntaxReceiver;

            BuildCodeData(codeData, syntaxReceiver, context);
            GenerateCode(codeData, context);
        }

        private void BuildCodeData(CodeData codeData, PESyntaxReceiver syntaxReceiver, GeneratorExecutionContext context)
        {
            // Groups
            foreach (InterfaceDeclarationSyntax groupInterfaceSyntax in syntaxReceiver.PolymorphicElementsGroupInterfaces)
            {
                GroupInterfaceData groupData = new GroupInterfaceData();
                groupData.Name = groupInterfaceSyntax.Identifier.Text;
                groupData.Namespace = SourceGenUtils.GetNamespace(groupInterfaceSyntax);

                // Usings
                groupData.Usings = new List<string>()
                {
                    "System",
                    "System.Runtime.InteropServices",
                    "Unity.Entities",
                    "Unity.Mathematics",
                    "Unity.Collections",
                    "Unity.Collections.LowLevel.Unsafe",
                };
                foreach (var usingElem in groupInterfaceSyntax.SyntaxTree.GetCompilationUnitRoot(context.CancellationToken).Usings)
                {
                    groupData.Usings.Add(usingElem.Name.ToString());
                }

                foreach (MemberDeclarationSyntax memberSyntax in groupInterfaceSyntax.Members)
                {
                    // Functions
                    if (memberSyntax.IsKind(SyntaxKind.MethodDeclaration) && memberSyntax is MethodDeclarationSyntax methodSyntax)
                    {
                        FunctionData functionData = new FunctionData();
                        functionData.Name = methodSyntax.Identifier.Text;
                        functionData.ReturnType = methodSyntax.ReturnType.ToString();
                        functionData.ReturnTypeIsVoid = functionData.ReturnType == VoidType;

                        functionData.WriteBackType = MethodWriteBackType.None;
                        if(SourceGenUtils.HasAttribute(methodSyntax, MethodRefWriteBackAttributeName))
                        {
                            functionData.WriteBackType = MethodWriteBackType.RefModify;
                        }
                        else if(SourceGenUtils.HasAttribute(methodSyntax, MethodWriteBackAttributeName))
                        {
                            functionData.WriteBackType = MethodWriteBackType.Write;
                        }

                        // Parameters
                        foreach (ParameterSyntax parameter in methodSyntax.ParameterList.Parameters)
                        {
                            ParameterData parameterData = new ParameterData();
                            parameterData.RefType = parameter.Modifiers.ToString();
                            parameterData.Type = parameter.Type.ToString();
                            parameterData.Name = parameter.Identifier.Text;
                            functionData.ParameterDatas.Add(parameterData);
                        }
                        
                        groupData.FunctionDatas.Add(functionData);
                    }
                }

                // Elements
                ushort idCounter = 0;
                foreach (StructDeclarationSyntax elementStruct in syntaxReceiver.PolymorphicElementStructs)
                {
                    if (SourceGenUtils.ImplementsInterface(elementStruct, groupInterfaceSyntax.Identifier.Text))
                    {
                        ElementData elementData = new ElementData();
                        elementData.Id = idCounter;
                        elementData.Type = elementStruct.Identifier.Text;
                        groupData.ElementDatas.Add(elementData);

                        idCounter++;

                        // Usings
                        string elementNamespace = SourceGenUtils.GetNamespace(elementStruct);
                        if (groupData.Namespace != elementNamespace)
                        {
                            groupData.Usings.Add(elementNamespace);
                        }
                        foreach (var usingElem in elementStruct.SyntaxTree.GetCompilationUnitRoot(context.CancellationToken).Usings)
                        {
                            groupData.Usings.Add(usingElem.Name.ToString());
                        }
                    }
                }

                codeData.GroupDatas.Add(groupData);
            }
        }

        private void GenerateCode(CodeData codeData, GeneratorExecutionContext context)
        {
            // Group class
            foreach (GroupInterfaceData groupData in codeData.GroupDatas)
            {
                FileWriter writer = new FileWriter();

                // Usings
                writer.WriteUsingsAndRemoveDuplicates(groupData.Usings);
                writer.WriteLine($"");

                // Namespace
                writer.WriteInNamespace(groupData.Namespace, () =>
                {

                    // Union struct of all elements
                    writer.WriteLine($"[StructLayout(LayoutKind.Explicit)]");
                    writer.WriteLine($"public struct {groupData.Name}{UnionElement}");
                    writer.WriteInScope(() =>
                    {
                        // Data payload
                        writer.WriteLine($"[StructLayout(LayoutKind.Explicit)]");
                        writer.WriteLine($"public struct DataPayload");
                        writer.WriteInScope(() =>
                        {
                            foreach (ElementData elementData in groupData.ElementDatas)
                            {
                                writer.WriteLine($"[FieldOffset(0)]");
                                writer.WriteLine($"public {elementData.Type} {elementData.Type};");
                            }
                        });
                        
                        writer.WriteLine($"");
                        
                        writer.WriteLine($"[FieldOffset(0)]");
                        writer.WriteLine($"public ushort TypeId;");
                        writer.WriteLine($"[FieldOffset({PolymorphicElementsUtility}.{SizeOfElementTypeId})]");
                        writer.WriteLine($"public DataPayload Data;");
                        
                        writer.WriteLine($"");
                        
                        // Constructors
                        foreach (ElementData elementData in groupData.ElementDatas)
                        {
                            writer.WriteLine($"public {groupData.Name}{UnionElement}({elementData.Type} e)");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"TypeId = {elementData.Id};");
                                writer.WriteLine($"Data = default;");
                                writer.WriteLine($"Data.{elementData.Type} = e;");
                            });
                            writer.WriteLine($"");
                        }
                        
                        // Functions
                        foreach (FunctionData functionData in groupData.FunctionDatas)
                        {
                            functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation, false);
                            
                            writer.WriteLine($"public {functionData.ReturnType} {functionData.Name}({parametersStringDeclaration})");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"switch (TypeId)");
                                writer.WriteInScope(() =>
                                {
                                    foreach (ElementData elementData in groupData.ElementDatas)
                                    {
                                        writer.WriteLine($"case {elementData.Id}:");
                                        writer.WriteInScope(() =>
                                        {
                                            writer.WriteLine($"{(functionData.ReturnTypeIsVoid ? "" : "return ")}Data.{elementData.Type}.{functionData.Name}({parametersStringInvocation});");
                                            if(functionData.ReturnTypeIsVoid)
                                            {
                                                writer.WriteLine($"break;");
                                            }
                                        });
                                    }
                                });
                                if(!functionData.ReturnTypeIsVoid)
                                {
                                    writer.WriteLine($"return default;");
                                }
                            });
                            writer.WriteLine($"");
                        }
                    });

                    writer.WriteLine($"");

                    // Manager
                    writer.WriteLine($"public static class {groupData.GetGeneratedGroupName()}");
                    writer.WriteInScope(() =>
                    {
                        // Type enum
                        writer.WriteLine($"public enum {ElementTypeEnumName} : ushort");
                        writer.WriteInScope(() =>
                        {
                            foreach (ElementData elementData in groupData.ElementDatas)
                            {
                                writer.WriteLine($"{elementData.Type},");
                            }
                        });

                        writer.WriteLine($"");

                        // Add functions
                        foreach (ElementData elementData in groupData.ElementDatas)
                        {
                            GenerateAddFunction(writer, "NativeStream.Writer", "streamWriter", elementData, false, false, "");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "S", "streamWriter", elementData, false, true, "where S : unmanaged, IPolymorphicStreamWriter");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "DynamicBuffer<byte>", "buffer", elementData, true, false, "");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "NativeList<byte>", "list", elementData, true, false, "");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "UnsafeList<byte>", "list", elementData, true, false, "");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "L", "list", elementData, true, true, "where L : unmanaged, IPolymorphicList");
                            writer.WriteLine($"");
                        }

                        // Execute functions
                        foreach (FunctionData functionData in groupData.FunctionDatas)
                        {
                            GenerateExecuteFunction(writer, groupData, functionData, "NativeStream.Reader", "streamReader", false, false, "");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "S", "streamReader", false, true, "where S : unmanaged, IPolymorphicStreamReader");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "DynamicBuffer<byte>", "buffer", true, false, "");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "NativeList<byte>", "list", true, false, "");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "UnsafeList<byte>", "list", true, false, "");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "L", "list", true, true, "where L : unmanaged, IPolymorphicList");
                            writer.WriteLine($"");
                        }

                        // GetElementType
                        writer.WriteLine($"public static {ElementTypeEnumName} {GetElementType}(ushort typeId)");
                        writer.WriteInScope(() => 
                        { 
                            writer.WriteLine($"return ({ElementTypeEnumName})typeId;"); 
                        });
                    });
                });

                context.AddSource(groupData.GetGeneratedGroupName(), SourceText.From(writer.FileContents, Encoding.UTF8));
            }
        }

        private void GenerateAddFunction(
            FileWriter writer, 
            string collectionType, 
            string collectionName, 
            ElementData elementData, 
            bool supportElementAccess,
            bool collectionTypeIsGeneric,
            string collectionGenericTypeConstraint)
        {
            string methodName = supportElementAccess ? AddElement : AppendElement;
            string genericType = collectionTypeIsGeneric ? $"<{collectionType}>" : "";
            string genericConstraint = collectionTypeIsGeneric ? $" {collectionGenericTypeConstraint}" : "";
            writer.WriteLine($"public static {(supportElementAccess ? $"{PolymorphicElementMetaData}" : "void")} {methodName}{genericType}(ref {collectionType} {collectionName}, {elementData.Type} element){genericConstraint}");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"{(supportElementAccess ? $"return " : "")}{PolymorphicElementsUtility}.{methodName}(ref {collectionName}, {elementData.Id}, element);");
            });
        }

        private void GenerateExecuteFunction(
            FileWriter writer, 
            GroupInterfaceData groupData, 
            FunctionData functionData, 
            string collectionType, 
            string collectionName, 
            bool supportIndexing, 
            bool collectionTypeIsGeneric,
            string collectionGenericTypeConstraint)
        {
            functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation, true);

            string genericType = collectionTypeIsGeneric ? $"<{collectionType}>" : "";
            string genericConstraint = collectionTypeIsGeneric ? $" {collectionGenericTypeConstraint}" : "";
            writer.WriteLine($"public static {functionData.ReturnType} {functionData.Name}{genericType}(ref {collectionType} {collectionName}{(supportIndexing ? ", int startByteIndex, out int newStartByteIndex" : "")}{parametersStringDeclaration}, out bool success){genericConstraint}");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"success = false;");
                writer.WriteLine($"if ({PolymorphicElementsUtility}.{InternalUse}.{ReadAny}(ref {collectionName}, {(supportIndexing ? "startByteIndex, out startByteIndex," : "")} out ushort elementId))");
                writer.WriteInScope(() =>
                {
                    writer.WriteLine($"switch (elementId)");
                    writer.WriteInScope(() =>
                    {
                        foreach (ElementData elementData in groupData.ElementDatas)
                        {
                            writer.WriteLine($"case {elementData.Id}:");
                            writer.WriteInScope(() =>
                            {
                                if(supportIndexing && functionData.WriteBackType == MethodWriteBackType.RefModify)
                                {
                                    writer.WriteLine($"ref {elementData.Type} e = ref {PolymorphicElementsUtility}.{InternalUse}.{ReadAnyAsRef}<{elementData.Type}{(collectionTypeIsGeneric ? $", {collectionType}" : "")}>(ref {collectionName}, {(supportIndexing ? "startByteIndex, out newStartByteIndex," : "")} out success);");
                                    writer.WriteLine($"if(success)");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"{(functionData.ReturnTypeIsVoid ? "" : "return ")}e.{functionData.Name}({parametersStringInvocation});");
                                    });
                                }
                                else
                                {
                                    writer.WriteLine($"if ({PolymorphicElementsUtility}.{InternalUse}.{ReadAny}(ref {collectionName}, {(supportIndexing ? "startByteIndex, out newStartByteIndex," : "")} out {elementData.Type} e))");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"{(functionData.ReturnTypeIsVoid ? "" : $"{functionData.ReturnType} returnValue = ")}e.{functionData.Name}({parametersStringInvocation});");
                                        if(supportIndexing && functionData.WriteBackType == MethodWriteBackType.Write)
                                        {
                                            writer.WriteLine($"{PolymorphicElementsUtility}.{InternalUse}.{WriteAny}(ref {collectionName}, startByteIndex, e);");
                                        }
                                        writer.WriteLine($"success = true;");
                                        if(!functionData.ReturnTypeIsVoid)
                                        {
                                            writer.WriteLine($"return returnValue;");
                                        }
                                    });
                                }
                                writer.WriteLine($"break;");
                            });
                        }
                    });
                });                        
                if(supportIndexing)
                {
                    writer.WriteLine($"newStartByteIndex = startByteIndex;");
                }
                if(!functionData.ReturnTypeIsVoid)
                {
                    writer.WriteLine($"return default;");
                }
            });
        }
    }
}