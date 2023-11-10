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
        private const string ExecuteElement = "ExecuteElement";
        private const string UnsafeUtility = "UnsafeUtility";
        private const string UnionElement = "UnionElement";

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
            FileWriter writer = new FileWriter();

            // Group class
            foreach (GroupInterfaceData groupData in codeData.GroupDatas)
            {
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
                                int elementCounter = 0;
                                foreach (ElementData elementData in groupData.ElementDatas)
                                {
                                    writer.WriteLine($"[FieldOffset(0)]");
                                    writer.WriteLine($"public {elementData.Type} Element{elementCounter};");
                                    elementCounter++;
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
                                    writer.WriteLine($"{PolymorphicElementsUtility}.WriteUnsafe(e, ref Data);");
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
                                                writer.WriteLine($"{PolymorphicElementsUtility}.ReadAsRef<{elementData.Type}, DataPayload>(ref Data).{functionData.Name}({parametersStringInvocation});");
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

                        // Write functions
                        foreach (ElementData elementData in groupData.ElementDatas)
                        {
                            GenerateWriteFunction(writer, "NativeStream.Writer", "streamWriter", elementData.Type, elementData.Id);
                            writer.WriteLine($"");
                            GenerateWriteFunction(writer, "DynamicBuffer<byte>", "buffer", elementData.Type, elementData.Id);
                            writer.WriteLine($"");
                            GenerateWriteFunction(writer, "NativeList<byte>", "list", elementData.Type, elementData.Id);
                            writer.WriteLine($"");
                        }

                        // Read & Execute functions
                        foreach (FunctionData functionData in groupData.FunctionDatas)
                        {
                            functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation, true);

                            GenerateReadFunction(writer, groupData, functionData.Name, "NativeStream.Reader", "streamReader", false, parametersStringDeclaration, parametersStringInvocation);
                            writer.WriteLine($"");
                            GenerateReadFunction(writer, groupData, functionData.Name, "DynamicBuffer<byte>", "buffer", true, parametersStringDeclaration, parametersStringInvocation);
                            writer.WriteLine($"");
                            GenerateReadFunction(writer, groupData, functionData.Name, "NativeList<byte>", "list", true, parametersStringDeclaration, parametersStringInvocation);
                            writer.WriteLine($"");
                        }

                        // Misc functions
                        {
                            // GetNextElementMetaData
                            {
                                GenerateGetNextElementMetaDataFunction(writer, "DynamicBuffer<byte>", "buffer");
                                writer.WriteLine($"");
                                GenerateGetNextElementMetaDataFunction(writer, "NativeList<byte>", "list");
                                writer.WriteLine($"");
                            }

                            // GetElementType
                            writer.WriteLine($"public static {ElementTypeEnumName} {GetElementType}(ushort elementId)");
                            writer.WriteInScope(() => 
                            { 
                                writer.WriteLine($"return ({ElementTypeEnumName})elementId;"); 
                            });

                            writer.WriteLine($"");

                            // GetElementSizeWithoutTypeId
                            writer.WriteLine($"public static int {GetElementSizeWithoutTypeId}(ushort elementId)");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"switch (elementId)");
                                writer.WriteInScope(() =>
                                {
                                    foreach (ElementData elementData in groupData.ElementDatas)
                                    {
                                        writer.WriteLine($"case {elementData.Id}:");
                                        writer.WriteInScope(() => { writer.WriteLine($"return {UnsafeUtility}.SizeOf<{elementData.Type}>();"); });
                                    }
                                });
                                writer.WriteLine($"return 0;");
                            });

                            writer.WriteLine($"");

                            // Get largest element size
                            writer.WriteLine($"public static int {GetLargestElementSizeWithTypeId}()");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"int largestSize = 0;");
                                foreach (ElementData elementData in groupData.ElementDatas)
                                {
                                    writer.WriteLine($"largestSize = math.max(largestSize, {UnsafeUtility}.SizeOf<{elementData.Type}>());");
                                }
                                writer.WriteLine($"return largestSize + {PolymorphicElementsUtility}.{SizeOfElementTypeId};");
                            });
                        }
                    });
                });

                context.AddSource(groupData.GetGeneratedGroupName(), SourceText.From(writer.FileContents, Encoding.UTF8));
            }
        }

        private void GenerateWriteFunction(FileWriter writer, string collectionType, string collectionName, string elementType, ushort elementId)
        {
            writer.WriteLine($"public static void {AddElement}(ref {collectionType} {collectionName}, {elementType} e)");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"{PolymorphicElementsUtility}.Write((ushort){elementId}, ref {collectionName});");
                writer.WriteLine($"{PolymorphicElementsUtility}.Write(e, ref {collectionName});");
            });
        }

        private void GenerateReadFunction(FileWriter writer, GroupInterfaceData groupData, string functionName, string collectionType, string collectionName, bool requiresIndex, string parametersDeclaration, string parametersInvocation)
        {
            writer.WriteLine($"public static bool {ExecuteElement}_{functionName}(ref {collectionType} {collectionName}{(requiresIndex ? ", ref int index" : "")}{parametersDeclaration})");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"if ({PolymorphicElementsUtility}.Read(ref {collectionName}, {(requiresIndex ? "ref index," : "")} out ushort elementId))");
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
                                writer.WriteLine($"if ({PolymorphicElementsUtility}.Read(ref {collectionName}, {(requiresIndex ? "ref index," : "")} out {elementData.Type} e))");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"e.{functionName}({parametersInvocation});");
                                    writer.WriteLine($"return true;");
                                });
                                writer.WriteLine($"break;");
                            });
                        }
                    });
                });
                writer.WriteLine($"return false;");
            });
        }

        private void GenerateGetNextElementMetaDataFunction(FileWriter writer, string collectionType, string collectionName)
        {
            writer.WriteLine($"public static bool {GetNextElementMetaData}(ref {collectionType} {collectionName}, ref int index, out {PolymorphicElementMetaData} elementMetaData)");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"if ({PolymorphicElementsUtility}.Read(ref {collectionName}, ref index, out ushort elementId))");
                writer.WriteInScope(() =>
                {
                    writer.WriteLine($"elementMetaData = new {PolymorphicElementMetaData}");
                    writer.WriteInScope(() =>
                    {
                        writer.WriteLine($"StartIndex = index,");
                        writer.WriteLine($"Size = {GetElementSizeWithoutTypeId}(elementId) + {PolymorphicElementsUtility}.{SizeOfElementTypeId},");
                    }, ";");
                    writer.WriteLine($"index += elementMetaData.Size;");
                    writer.WriteLine($"return true;");
                });
                writer.WriteLine($"elementMetaData = default;");
                writer.WriteLine($"return false;");
            });
        }
    }
}