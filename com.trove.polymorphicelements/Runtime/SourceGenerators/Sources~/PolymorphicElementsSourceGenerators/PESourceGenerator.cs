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
        private const string PolymorphicElementsUtility = "PolymorphicElementsUtility";
        private const string ElementTypeEnumName = "ElementType";
        private const string PolymorphicElementMetaData = "PolymorphicElementMetaData";
        private const string GetElementType = "GetElementType";
        private const string GetElementSizeWithoutTypeId = "GetElementSizeWithoutTypeId";
        private const string GetLargestElementSizeWithTypeId = "GetLargestElementSizeWithTypeId";
        private const string SizeOfElementTypeId = "SizeOfElementTypeId";
        private const string GetNextElementMetaData = "GetNextElementMetaData";
        private const string AddElement = "AddElement";
        private const string ExecuteFunction = "Execute";
        private const string ReferenceOf = "ReferenceOf";
        private const string UnionElement = "UnionElement";
        private const string InternalUse = "InternalUse";
        private const string ReadAny = "ReadAny";
        private const string ReadAnyAsRef = "ReadAnyAsRef";

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
                            GenerateAddFunction(writer, "NativeStream.Writer", "streamWriter", elementData.Type, elementData.Id, false);
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "DynamicBuffer<byte>", "buffer", elementData.Type, elementData.Id, true);
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, "NativeList<byte>", "list", elementData.Type, elementData.Id, true);
                            writer.WriteLine($"");
                        }

                        // Execute functions
                        foreach (FunctionData functionData in groupData.FunctionDatas)
                        {
                            functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation, true);

                            GenerateExecuteFunction(writer, groupData, functionData.Name, "NativeStream.Reader", "streamReader", false, false, parametersStringDeclaration, parametersStringInvocation);
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData.Name, "DynamicBuffer<byte>", "buffer", true, true, parametersStringDeclaration, parametersStringInvocation);
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData.Name, "NativeList<byte>", "list", true, true, parametersStringDeclaration, parametersStringInvocation);
                            writer.WriteLine($"");
                        }

                        // Misc functions
                        {
                            // GetElementType
                            writer.WriteLine($"public static {ElementTypeEnumName} {GetElementType}(ushort elementId)");
                            writer.WriteInScope(() => 
                            { 
                                writer.WriteLine($"return ({ElementTypeEnumName})elementId;"); 
                            });

                            writer.WriteLine($"");
                        }
                    });
                });

                context.AddSource(groupData.GetGeneratedGroupName(), SourceText.From(writer.FileContents, Encoding.UTF8));
            }
        }

        private void GenerateAddFunction(FileWriter writer, string collectionType, string collectionName, string elementType, ushort elementId, bool supportElementAccess)
        {
            writer.WriteLine($"public static {(supportElementAccess ? $"ref {elementType}" : "void")} {AddElement}(ref {collectionType} {collectionName}, {elementType} element{(supportElementAccess ? $", out {PolymorphicElementMetaData} metaData" : "")})");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"{(supportElementAccess ? $"return ref " : "")}{PolymorphicElementsUtility}.{AddElement}(ref {collectionName}, {elementId}, element{(supportElementAccess ? $", out metaData" : "")});");
            });
        }

        private void GenerateExecuteFunction(FileWriter writer, GroupInterfaceData groupData, string functionName, string collectionType, string collectionName, bool requiresIndex, bool useReadRef, string parametersDeclaration, string parametersInvocation)
        {
            writer.WriteLine($"public static bool {ExecuteFunction}_{functionName}(ref {collectionType} {collectionName}{(requiresIndex ? ", int startByteIndex, out int newStartByteIndex" : "")}{parametersDeclaration})");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"if ({PolymorphicElementsUtility}.{InternalUse}.{ReadAny}(ref {collectionName}, {(requiresIndex ? "startByteIndex, out startByteIndex," : "")} out ushort elementId))");
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
                                if(useReadRef)
                                {
                                    writer.WriteLine($"ref {elementData.Type} e = ref {PolymorphicElementsUtility}.{InternalUse}.{ReadAnyAsRef}<{elementData.Type}>(ref {collectionName}, {(requiresIndex ? "startByteIndex, out newStartByteIndex," : "")} out bool success);");
                                    writer.WriteLine($"if(success)");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"e.{functionName}({parametersInvocation});");
                                        writer.WriteLine($"return true;");
                                    });
                                }
                                else
                                {
                                    writer.WriteLine($"if ({PolymorphicElementsUtility}.{InternalUse}.{ReadAny}(ref {collectionName}, {(requiresIndex ? "startByteIndex, out newStarByteIndex," : "")} out {elementData.Type} e))");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"e.{functionName}({parametersInvocation});");
                                        writer.WriteLine($"return true;");
                                    });
                                }
                                writer.WriteLine($"break;");
                            });
                        }
                    });
                });
                if(requiresIndex)
                {
                    writer.WriteLine($"newStartByteIndex = default;");
                }
                writer.WriteLine($"return false;");
            });
        }
    }
}