using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolymorphicElementsSourceGenerators
{
    [Generator]
    public class PESourceGenerator : ISourceGenerator
    {
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
            foreach (InterfaceDeclarationSyntax groupInterface in syntaxReceiver.PolymorphicElementsGroupInterfaces)
            {
                GroupInterfaceData groupData = new GroupInterfaceData();
                groupData.Name = groupInterface.Identifier.Text;
                groupData.Namespace = SourceGenUtils.GetNamespace(groupInterface);

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
                foreach (var usingElem in groupInterface.SyntaxTree.GetCompilationUnitRoot(context.CancellationToken).Usings)
                {
                    groupData.Usings.Add(usingElem.Name.ToString());
                }

                // Functions
                foreach (MemberDeclarationSyntax function in groupInterface.Members)
                {
                    if (function.IsKind(SyntaxKind.MethodDeclaration) && function is MethodDeclarationSyntax methodSyntax)
                    {
                        FunctionData functionData = new FunctionData();
                        functionData.Name = methodSyntax.Identifier.Text;
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
                    if (SourceGenUtils.ImplementsInterface(elementStruct, groupInterface.Identifier.Text))
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
                    // Handler
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

                        // Union struct of all elements
                        writer.WriteLine($"[StructLayout(LayoutKind.Explicit)]");
                        writer.WriteLine($"public struct {UnionElement}");
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
                                writer.WriteLine($"public {UnionElement}({elementData.Type} e)");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"TypeId = {elementData.Id};");
                                    writer.WriteLine($"Data = default;");
                                    writer.WriteLine($"Data.{elementData.Type} = e;");
                                });
                                writer.WriteLine($"");
                            }
                            
                            // Executes
                            foreach (FunctionData functionData in groupData.FunctionDatas)
                            {
                                functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation, false);
                                
                                writer.WriteLine($"public bool Execute_{functionData.Name}({parametersStringDeclaration})");
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
                                                writer.WriteLine($"Data.{elementData.Type}.{functionData.Name}({parametersStringInvocation});");
                                                writer.WriteLine($"return true;");
                                            });
                                        }
                                    });
                                    writer.WriteLine($"return false;");
                                });
                                writer.WriteLine($"");
                            }
                        });

                        writer.WriteLine($"");

                        // Add functions
                        foreach (ElementData elementData in groupData.ElementDatas)
                        {
                            GenerateAddFunction(writer, "NativeStream.Writer", "streamWriter", elementData.Type, elementData.Id, false, false, "");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "S", "streamWriter", elementData.Type, elementData.Id, false, true, "where S : unmanaged, IPolymorphicStreamWriter");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "DynamicBuffer<byte>", "buffer", elementData.Type, elementData.Id, true, false, "");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "NativeList<byte>", "list", elementData.Type, elementData.Id, true, false, "");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "UnsafeList<byte>", "list", elementData.Type, elementData.Id, true, false, "");
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "L", "list", elementData.Type, elementData.Id, true, true, "where L : unmanaged, IPolymorphicList");
                            writer.WriteLine($"");
                        }

                        // Execute functions
                        foreach (FunctionData functionData in groupData.FunctionDatas)
                        {
                            functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation, true);

                            GenerateExecuteFunction(writer, groupData, functionData.Name, functionData.WriteBackType, "NativeStream.Reader", "streamReader", false, parametersStringDeclaration, parametersStringInvocation, false, "");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData.Name, functionData.WriteBackType, "S", "streamReader", false, parametersStringDeclaration, parametersStringInvocation, true, "where S : unmanaged, IPolymorphicStreamReader");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData.Name, functionData.WriteBackType, "DynamicBuffer<byte>", "buffer", true, parametersStringDeclaration, parametersStringInvocation, false, "");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData.Name, functionData.WriteBackType, "NativeList<byte>", "list", true, parametersStringDeclaration, parametersStringInvocation, false, "");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData.Name, functionData.WriteBackType, "UnsafeList<byte>", "list", true, parametersStringDeclaration, parametersStringInvocation, false, "");
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData.Name, functionData.WriteBackType, "L", "list", true, parametersStringDeclaration, parametersStringInvocation, true, "where L : unmanaged, IPolymorphicList");
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
            string elementType, 
            ushort elementId, 
            bool supportElementAccess,
            bool collectionTypeIsGeneric,
            string collectionGenericTypeConstraint)
        {
            string methodName = supportElementAccess ? AddElement : AppendElement;
            string genericType = collectionTypeIsGeneric ? $"<{collectionType}>" : "";
            string genericConstraint = collectionTypeIsGeneric ? $" {collectionGenericTypeConstraint}" : "";
            writer.WriteLine($"public static {(supportElementAccess ? $"{PolymorphicElementMetaData}" : "void")} {methodName}{genericType}(ref {collectionType} {collectionName}, {elementType} element){genericConstraint}");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"{(supportElementAccess ? $"return " : "")}{PolymorphicElementsUtility}.{methodName}(ref {collectionName}, {elementId}, element);");
            });
        }

        private void GenerateExecuteFunction(
            FileWriter writer, 
            GroupInterfaceData groupData, 
            string functionName, 
            MethodWriteBackType writeBackType,
            string collectionType, 
            string collectionName, 
            bool supportIndexing, 
            string parametersDeclaration, 
            string parametersInvocation,
            bool collectionTypeIsGeneric,
            string collectionGenericTypeConstraint)
        {
            string genericType = collectionTypeIsGeneric ? $"<{collectionType}>" : "";
            string genericConstraint = collectionTypeIsGeneric ? $" {collectionGenericTypeConstraint}" : "";
            writer.WriteLine($"public static bool {ExecuteFunction}_{functionName}{genericType}(ref {collectionType} {collectionName}{(supportIndexing ? ", int startByteIndex, out int newStartByteIndex" : "")}{parametersDeclaration}){genericConstraint}");
            writer.WriteInScope(() =>
            {
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
                                if(supportIndexing && writeBackType == MethodWriteBackType.RefModify)
                                {
                                    writer.WriteLine($"ref {elementData.Type} e = ref {PolymorphicElementsUtility}.{InternalUse}.{ReadAnyAsRef}<{elementData.Type}{(collectionTypeIsGeneric ? $", {collectionType}" : "")}>(ref {collectionName}, {(supportIndexing ? "startByteIndex, out newStartByteIndex," : "")} out bool success);");
                                    writer.WriteLine($"if(success)");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"e.{functionName}({parametersInvocation});");
                                        writer.WriteLine($"return true;");
                                    });
                                }
                                else
                                {
                                    writer.WriteLine($"if ({PolymorphicElementsUtility}.{InternalUse}.{ReadAny}(ref {collectionName}, {(supportIndexing ? "startByteIndex, out newStartByteIndex," : "")} out {elementData.Type} e))");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"e.{functionName}({parametersInvocation});");
                                        if(supportIndexing && writeBackType == MethodWriteBackType.Write)
                                        {
                                            writer.WriteLine($"{PolymorphicElementsUtility}.{InternalUse}.{WriteAny}(ref {collectionName}, startByteIndex, e);");
                                        }
                                        writer.WriteLine($"return true;");
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
                writer.WriteLine($"return false;");
            });
        }
    }
}