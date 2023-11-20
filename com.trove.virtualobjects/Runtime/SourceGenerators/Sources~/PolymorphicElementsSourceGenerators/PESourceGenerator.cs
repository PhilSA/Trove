﻿using System.Collections.Generic;
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
        private const string GetElementTypeId = "GetElementTypeId";
        private const string GetElementTotalSize = "GetElementTotalSize";
        private const string SizeOfElementTypeId = "SizeOfElementTypeId";
        private const string AppendElement = "AppendElement";
        private const string AddElement = "AddElement";
        private const string InsertElement = "InsertElement";
        private const string UnionElement = "UnionElement";
        private const string ByteCollectionUtility = "ByteCollectionUtility";
        private const string Read = "Read";
        private const string ReadAsRef = "ReadAsRef";
        private const string WriteNoResize = "WriteNoResize";
        private const string StartByteIndex = "startByteIndex";
        private const string NextStartByteIndex = "nextStartByteIndex";
        private const string StartByteIndexOfElementValue = "startByteIndexOfElementValue";
        private const string IPolymorphicElementWriter = "IPolymorphicElementWriter";
        private const string PolymorphicElementPtr = "PolymorphicElementPtr";
        

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
                SemanticModel interfaceSemanticModel = context.Compilation.GetSemanticModel(groupInterfaceSyntax.SyntaxTree);
                INamedTypeSymbol groupInterfaceSymbol = interfaceSemanticModel.GetDeclaredSymbol(groupInterfaceSyntax, context.CancellationToken);

                GroupInterfaceData groupData = new GroupInterfaceData();
                groupData.Name = groupInterfaceSyntax.Identifier.Text;
                groupData.Namespace = SourceGenUtils.GetNamespace(groupInterfaceSyntax);

                // Usings
                groupData.Usings = new List<string>()
                {
                    "System",
                    "System.Runtime.InteropServices",
                    "System.Runtime.CompilerServices",
                    "Unity.Entities",
                    "Unity.Mathematics",
                    "Unity.Collections",
                    "Unity.Collections.LowLevel.Unsafe",
                };
                foreach (var usingElem in groupInterfaceSyntax.SyntaxTree.GetCompilationUnitRoot(context.CancellationToken).Usings)
                {
                    groupData.Usings.Add(usingElem.Name.ToString());
                }

                List<INamedTypeSymbol> interfacesHierarchySymbols = new List<INamedTypeSymbol>();
                interfacesHierarchySymbols.Add(groupInterfaceSymbol);
                interfacesHierarchySymbols.AddRange(groupInterfaceSymbol.AllInterfaces);
                foreach (INamedTypeSymbol interfaceSymbol in interfacesHierarchySymbols)
                {
                    foreach (ISymbol memberSymbol in interfaceSymbol.GetMembers())
                    {
                        // Functions
                        if (memberSymbol.Kind == SymbolKind.Method && memberSymbol is IMethodSymbol methodSymbol)
                        {
                            FunctionData functionData = new FunctionData();
                            functionData.Name = methodSymbol.Name;
                            functionData.ReturnType = methodSymbol.ReturnType.ToString();
                            functionData.ReturnTypeIsVoid = functionData.ReturnType == VoidType;

                            functionData.WriteBackType = MethodWriteBackType.None;
                            if (SourceGenUtils.HasAttribute(methodSymbol, MethodRefWriteBackAttributeName))
                            {
                                functionData.WriteBackType = MethodWriteBackType.RefModify;
                            }
                            else if (SourceGenUtils.HasAttribute(methodSymbol, MethodWriteBackAttributeName))
                            {
                                functionData.WriteBackType = MethodWriteBackType.Write;
                            }

                            functionData.GenericTypeDatas = new List<GenericTypeData>();     
                            if (methodSymbol.IsGenericMethod)
                            {                           
                                foreach (ITypeParameterSymbol typeParam in methodSymbol.TypeParameters)
                                {
                                    GenericTypeData genericTypeData = new GenericTypeData();
                                    genericTypeData.Type = $"{typeParam}";
                                    genericTypeData.TypeConstraints = new List<string>();

                                    if (typeParam.HasUnmanagedTypeConstraint)
                                    {
                                        genericTypeData.TypeConstraints.Add($"unmanaged");
                                    }
                                    else if (typeParam.HasValueTypeConstraint)
                                    {
                                        genericTypeData.TypeConstraints.Add($"struct");
                                    }
                                    foreach (ITypeSymbol constraintType in typeParam.ConstraintTypes)
                                    {
                                        genericTypeData.TypeConstraints.Add($"{constraintType}");
                                    }

                                    functionData.GenericTypeDatas.Add(genericTypeData);
                                }
                                SourceGenUtils.GetGenericTypesStrings(functionData.GenericTypeDatas, out functionData.GenericTypesString, out functionData.GenericTypeConstraintsString);
                            }

                            // Parameters
                            foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
                            {
                                ParameterData parameterData = new ParameterData();
                                parameterData.RefType = SourceGenUtils.RefKindToString(parameterSymbol.RefKind);
                                parameterData.Type = $"{parameterSymbol.Type}";
                                parameterData.Name = parameterSymbol.Name;
                                functionData.ParameterDatas.Add(parameterData);
                            }

                            groupData.FunctionDatas.Add(functionData);
                        }
                    }
                }

                // Elements
                ushort idCounter = 0;
                foreach (StructDeclarationSyntax elementStructSyntax in syntaxReceiver.PolymorphicElementStructs)
                {
                    if (SourceGenUtils.ImplementsInterface(elementStructSyntax, groupInterfaceSyntax.Identifier.Text))
                    {
                        ElementData elementData = new ElementData();
                        elementData.Id = idCounter;
                        elementData.Type = elementStructSyntax.Identifier.Text;

                        bool isPublic = false;
                        bool isPartial = false;
                        foreach (var modifier in elementStructSyntax.Modifiers)
                        {
                            if(modifier.IsKind(SyntaxKind.PublicKeyword))
                            {
                                isPublic = true;
                            }
                            if(modifier.IsKind(SyntaxKind.PartialKeyword))
                            {
                                isPartial = true;
                            }
                        }
                        elementData.IsPublicPartial = isPublic && isPartial;

                        idCounter++;

                        // Usings
                        string elementNamespace = SourceGenUtils.GetNamespace(elementStructSyntax);
                        if (groupData.Namespace != elementNamespace)
                        {
                            groupData.Usings.Add(elementNamespace);
                        }
                        foreach (var usingElem in elementStructSyntax.SyntaxTree.GetCompilationUnitRoot(context.CancellationToken).Usings)
                        {
                            groupData.Usings.Add(usingElem.Name.ToString());
                        }
                        
                        groupData.ElementDatas.Add(elementData);
                    }
                }

                codeData.GroupDatas.Add(groupData);
            }
        }

        private void GenerateCode(CodeData codeData, GeneratorExecutionContext context)
        {
            foreach (GroupInterfaceData groupData in codeData.GroupDatas)
            {
                FileWriter writer = new FileWriter();

                // Usings
                writer.WriteUsingsAndRemoveDuplicates(groupData.Usings);
                writer.WriteLine($"");

                // Namespace
                writer.WriteInNamespace(groupData.Namespace, () =>
                {
                    WriteManager(writer, groupData);
                    writer.WriteLine($"");
                    WriteUnionStruct(writer, groupData);
                    writer.WriteLine($"");
                    foreach (var elementData in groupData.ElementDatas)
                    {
                        WritePartialStruct(writer, elementData);
                    }
                });

                context.AddSource(groupData.GetGeneratedGroupName(), SourceText.From(writer.FileContents, Encoding.UTF8));
            }
        }

        private void WriteManager(FileWriter writer, GroupInterfaceData groupData)
        {
            // Manager
            writer.WriteLine($"public static unsafe class {groupData.GetGeneratedGroupName()}");
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

                // GetElementTypeId
                writer.WriteLine($"public static {ElementTypeEnumName} {GetElementTypeId}(ushort typeId)");
                writer.WriteInScope(() => 
                { 
                    writer.WriteLine($"return ({ElementTypeEnumName})typeId;"); 
                });

                writer.WriteLine($"");

                // GetElementSize
                writer.WriteLine($"public static int {GetElementTotalSize}(ushort typeId)");
                writer.WriteInScope(() => 
                { 
                    writer.WriteLine($"switch (typeId)");
                    writer.WriteInScope(() =>
                    {
                        foreach (ElementData elementData in groupData.ElementDatas)
                        {
                            writer.WriteLine($"case {elementData.Id}:");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"return sizeof({elementData.Type});");
                            });
                        }
                    });
                    writer.WriteLine($"return 0;");
                });

                writer.WriteLine($"");

                foreach (ElementData elementData in groupData.ElementDatas)
                {
                    // Write
                    {
                        writer.WriteLine($"[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                        writer.WriteLine($"public static void WriteElement({PolymorphicElementPtr} ptr, {elementData.Type} e)");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"*(ushort*)ptr.Ptr = {elementData.Id};");
                            writer.WriteLine($"ptr.Ptr += (long)sizeof(ushort);");
                            writer.WriteLine($"*({elementData.Type}*)ptr.Ptr = e;");
                        });
                    }

                    writer.WriteLine($"");
                }

                // Execute functions
                foreach (FunctionData functionData in groupData.FunctionDatas)
                {
                    functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation);

                    List<GenericTypeData> allGenericTypeDatas = new List<GenericTypeData>();
                    if(functionData.GenericTypeDatas != null)
                    {
                        allGenericTypeDatas.AddRange(functionData.GenericTypeDatas);
                    }
                    SourceGenUtils.GetGenericTypesStrings(allGenericTypeDatas, out string allGenericTypes, out string allGenericTypeConstraints);

                    {
                        writer.WriteLine($"[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                        writer.WriteLine($"public static {functionData.ReturnType} {functionData.Name}{allGenericTypes}({PolymorphicElementPtr} ptr, out int readSize{(functionData.ParameterDatas.Count > 0 ? $", {parametersStringDeclaration}" : "")}){allGenericTypeConstraints}");
                        writer.WriteInScope(() =>
                        {         
                            writer.WriteLine($"readSize = sizeof(ushort);");
                            writer.WriteLine($"ushort typeId = *(ushort*)ptr.Ptr;");
                            writer.WriteLine($"ptr.Ptr += (long)sizeof(ushort);");
                            writer.WriteLine($"switch (typeId)");
                            writer.WriteInScope(() =>
                            {
                                foreach (ElementData elementData in groupData.ElementDatas)
                                {
                                    writer.WriteLine($"case {elementData.Id}:");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"readSize += sizeof({elementData.Type});");
                                        if(functionData.WriteBackType == MethodWriteBackType.RefModify)
                                        {
                                            writer.WriteLine($"(({elementData.Type}*)ptr.Ptr)->{functionData.Name}({parametersStringInvocation});");
                                        }
                                        else if (functionData.WriteBackType == MethodWriteBackType.Write)
                                        {
                                            writer.WriteLine($"{elementData.Type} e = *({elementData.Type}*)ptr.Ptr;");
                                            writer.WriteLine($"{(functionData.ReturnTypeIsVoid ? "" : $"{functionData.ReturnType} returnValue = ")}e.{functionData.Name}({parametersStringInvocation});");
                                            writer.WriteLine($"*({elementData.Type}*)ptr.Ptr = e;");
                                            if(!functionData.ReturnTypeIsVoid)
                                            {
                                                writer.WriteLine($"return returnValue;");
                                            }
                                        }
                                        else
                                        {
                                            writer.WriteLine($"{(functionData.ReturnTypeIsVoid ? "" : "return ")} (*({elementData.Type}*)ptr.Ptr).{functionData.Name}({parametersStringInvocation});");
                                        }
                                        writer.WriteLine($"break;");
                                    });
                                }
                            });

                            if(!functionData.ReturnTypeIsVoid)
                            {
                                writer.WriteLine($"return default;");
                            }
                        });
                    }

                    writer.WriteLine($"");
                }
            });
        }

        private void WriteUnionStruct(FileWriter writer, GroupInterfaceData groupData)
        {
            // Union struct of all elements
            writer.WriteLine($"[StructLayout(LayoutKind.Explicit)]");
            writer.WriteLine($"public unsafe struct {groupData.Name}{UnionElement} : {groupData.Name}");
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
                writer.WriteLine($"[FieldOffset(sizeof(ushort))]");
                writer.WriteLine($"public DataPayload Data;");
                
                writer.WriteLine($"");
                
                // Constructors
                {
                    // From element structs
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
                }

                // Implicit casts from elem
                foreach (ElementData elementData in groupData.ElementDatas)
                {
                    writer.WriteLine($"public static implicit operator {groupData.Name}{UnionElement}({elementData.Type} e) => new {groupData.Name}{UnionElement}(e);");
                }

                writer.WriteLine($"");                      
                
                // Interface Functions
                foreach (FunctionData functionData in groupData.FunctionDatas)
                {
                    functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation);

                    writer.WriteLine($"public {functionData.ReturnType} {functionData.Name}{functionData.GenericTypesString}({parametersStringDeclaration}){functionData.GenericTypeConstraintsString}");
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
                                    if (functionData.ReturnTypeIsVoid)
                                    {
                                        writer.WriteLine($"break;");
                                    }
                                });
                            }
                        });
                        if (!functionData.ReturnTypeIsVoid)
                        {
                            writer.WriteLine($"return default;");
                        }
                    });
                    writer.WriteLine($"");
                }
            });
        }

        private void WritePartialStruct(FileWriter writer, ElementData elementData)
        {
            if(elementData.IsPublicPartial)
            {
                writer.WriteLine($"public partial struct {elementData.Type} : {IPolymorphicElementWriter}");
                writer.WriteInScope(() =>
                {
                    // Get type id
                    writer.WriteLine($"[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                    writer.WriteLine($"public ushort GetTypeId()");
                    writer.WriteInScope(() =>
                    {
                        writer.WriteLine($"return {elementData.Id};");
                    });

                    writer.WriteLine($"");

                    // Get size
                    writer.WriteLine($"[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                    writer.WriteLine($"public unsafe int GetTotalSize()");
                    writer.WriteInScope(() =>
                    {
                        writer.WriteLine($"return sizeof(ushort) + sizeof({elementData.Type});");
                    });

                    writer.WriteLine($"");

                    // Write
                    {
                        writer.WriteLine($"[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                        writer.WriteLine($"public unsafe void Write({PolymorphicElementPtr} ptr)");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"*(ushort*)ptr.Ptr = {elementData.Id};");
                            writer.WriteLine($"ptr.Ptr += (long)sizeof(ushort);");
                            writer.WriteLine($"*({elementData.Type}*)ptr.Ptr = this;");
                        });
                    }
                    
                    writer.WriteLine($"");
                });
                writer.WriteLine($"");
            }
        }
    }
}