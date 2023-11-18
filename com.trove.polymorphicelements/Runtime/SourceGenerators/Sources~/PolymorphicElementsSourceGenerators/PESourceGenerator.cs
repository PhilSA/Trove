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
        private const string IPolymorphicUnionElement = "IPolymorphicUnionElement";

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
                    writer.WriteLine($"public unsafe struct {groupData.Name}{UnionElement} : {groupData.Name}, {IPolymorphicUnionElement}");
                    writer.WriteInScope(() =>
                    {
                        // Data payload
                        writer.WriteLine($"[StructLayout(LayoutKind.Explicit)]");
                        writer.WriteLine($"public struct TypeIdAndDataPayload");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"[FieldOffset(0)]");
                            writer.WriteLine($"public ushort TypeId;");
                            writer.WriteLine($"[FieldOffset(sizeof(ushort))]");
                            writer.WriteLine($"public DataPayload Data;");
                        });

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
                        writer.WriteLine($"public int ElementSize;");
                        writer.WriteLine($"[FieldOffset(sizeof(int))]");
                        writer.WriteLine($"public TypeIdAndDataPayload TypeIdAndData;");
                        
                        writer.WriteLine($"");
                        
                        // Constructors
                        {
                            // From pointer
                            writer.WriteLine($"public {groupData.Name}{UnionElement}(byte* ptr, int size)");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"ElementSize = size;");
                                writer.WriteLine($"TypeIdAndData = *(TypeIdAndDataPayload*)ptr;");
                            });
                        
                            // From element structs
                            foreach (ElementData elementData in groupData.ElementDatas)
                            {
                                writer.WriteLine($"public {groupData.Name}{UnionElement}({elementData.Type} e)");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"ElementSize = sizeof(ushort) + sizeof({elementData.Type});");

                                    writer.WriteLine($"TypeIdAndData = default;");
                                    writer.WriteLine($"TypeIdAndData.TypeId = {elementData.Id};");
                                    writer.WriteLine($"TypeIdAndData.Data = default;");
                                    writer.WriteLine($"TypeIdAndData.Data.{elementData.Type} = e;");
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

                        // Add 
                        GenerateAddFunction(writer, true, groupData, null, "NativeStream.Writer", "streamWriter", false, null);
                        writer.WriteLine($"");         
                        GenerateAddFunction(writer, true, groupData, null, "UnsafeStream.Writer", "streamWriter", false, null);
                        writer.WriteLine($"");                            
                        GenerateAddFunction(writer, true, groupData, null, "S", "streamWriter", false, new List<GenericTypeData>() { new GenericTypeData { Type = "S", TypeConstraints = new List<string>() { "unmanaged", "IStreamWriter" }}});
                        writer.WriteLine($"");
                        GenerateAddFunction(writer, true, groupData, null, "DynamicBuffer<byte>", "list", true, null);
                        writer.WriteLine($"");
                        GenerateAddFunction(writer, true, groupData, null, "DynamicBuffer<B>", "list", true, new List<GenericTypeData>() { new GenericTypeData { Type = "B", TypeConstraints = new List<string>() { "unmanaged", "IBufferElementData", "IByteBufferElement" }}});
                        writer.WriteLine($"");
                        GenerateAddFunction(writer, true, groupData, null, "NativeList<byte>", "list", true, null);
                        writer.WriteLine($"");
                        GenerateAddFunction(writer, true, groupData, null, "UnsafeList<byte>", "list", true, null);
                        writer.WriteLine($"");                            
                        GenerateAddFunction(writer, true, groupData, null, "L", "list", true, new List<GenericTypeData>() { new GenericTypeData { Type = "L", TypeConstraints = new List<string>() { "unmanaged", "IByteList" }}});
                        writer.WriteLine($"");

                        // Insert
                        GenerateInsertFunction(writer, true, groupData, null, "DynamicBuffer<byte>", "list", null);
                        writer.WriteLine($"");
                        GenerateInsertFunction(writer, true, groupData, null, "DynamicBuffer<B>", "list", new List<GenericTypeData>() { new GenericTypeData { Type = "B", TypeConstraints = new List<string>() { "unmanaged", "IBufferElementData", "IByteBufferElement" }}});
                        writer.WriteLine($"");
                        GenerateInsertFunction(writer, true, groupData, null, "NativeList<byte>", "list", null);
                        writer.WriteLine($"");
                        GenerateInsertFunction(writer, true, groupData, null, "UnsafeList<byte>", "list", null);
                        writer.WriteLine($"");                            
                        GenerateInsertFunction(writer, true, groupData, null, "L", "list", new List<GenericTypeData>() { new GenericTypeData { Type = "L", TypeConstraints = new List<string>() { "unmanaged", "IByteList" }}});
                        writer.WriteLine($"");                        
                        
                        // Interface Functions
                        foreach (FunctionData functionData in groupData.FunctionDatas)
                        {
                            functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation, false);

                            writer.WriteLine($"public {functionData.ReturnType} {functionData.Name}{functionData.GenericTypesString}({parametersStringDeclaration}){functionData.GenericTypeConstraintsString}");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"switch (TypeIdAndData.TypeId)");
                                writer.WriteInScope(() =>
                                {
                                    foreach (ElementData elementData in groupData.ElementDatas)
                                    {
                                        writer.WriteLine($"case {elementData.Id}:");
                                        writer.WriteInScope(() =>
                                        {
                                            writer.WriteLine($"{(functionData.ReturnTypeIsVoid ? "" : "return ")}TypeIdAndData.Data.{elementData.Type}.{functionData.Name}({parametersStringInvocation});");
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
                        
                        foreach (ElementData elementData in groupData.ElementDatas)
                        {
                            // Add 
                            GenerateAddFunction(writer, false, groupData, elementData, "NativeStream.Writer", "streamWriter", false, null);
                            writer.WriteLine($"");                            
                            GenerateAddFunction(writer, false, groupData, elementData, "UnsafeStream.Writer", "streamWriter", false, null);
                            writer.WriteLine($"");                            
                            GenerateAddFunction(writer, false, groupData, elementData, "S", "streamWriter", false, new List<GenericTypeData>() { new GenericTypeData { Type = "S", TypeConstraints = new List<string>() { "unmanaged", "IStreamWriter" }}});
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, false, groupData, elementData, "DynamicBuffer<byte>", "buffer", true, null);
                            writer.WriteLine($"");    
                            GenerateAddFunction(writer, false, groupData, elementData, "DynamicBuffer<B>", "buffer", true, new List<GenericTypeData>() { new GenericTypeData { Type = "B", TypeConstraints = new List<string>() { "unmanaged", "IBufferElementData", "IByteBufferElement" }}});
                            writer.WriteLine($"");    
                            GenerateAddFunction(writer, false, groupData, elementData, "NativeList<byte>", "list", true, null);
                            writer.WriteLine($"");
                            GenerateAddFunction(writer, false, groupData, elementData, "UnsafeList<byte>", "list", true, null);
                            writer.WriteLine($"");                            
                            GenerateAddFunction(writer, false, groupData, elementData, "L", "list", true, new List<GenericTypeData>() { new GenericTypeData { Type = "L", TypeConstraints = new List<string>() { "unmanaged", "IByteList" }}});
                            writer.WriteLine($"");

                            // Insert
                            GenerateInsertFunction(writer, false, groupData, elementData, "DynamicBuffer<byte>", "buffer", null);
                            writer.WriteLine($"");       
                            GenerateInsertFunction(writer, false, groupData, elementData, "DynamicBuffer<B>", "buffer", new List<GenericTypeData>() { new GenericTypeData { Type = "B", TypeConstraints = new List<string>() { "unmanaged", "IBufferElementData", "IByteBufferElement" }}});
                            writer.WriteLine($"");        
                            GenerateInsertFunction(writer, false, groupData, elementData, "NativeList<byte>", "list", null);
                            writer.WriteLine($"");
                            GenerateInsertFunction(writer, false, groupData, elementData, "UnsafeList<byte>", "list", null);
                            writer.WriteLine($"");                            
                            GenerateInsertFunction(writer, false, groupData, elementData, "L", "list", new List<GenericTypeData>() { new GenericTypeData { Type = "L", TypeConstraints = new List<string>() { "unmanaged", "IByteList" }}});
                        }

                        writer.WriteLine($"");

                        // Execute functions
                        foreach (FunctionData functionData in groupData.FunctionDatas)
                        {
                            GenerateExecuteFunction(writer, groupData, functionData, "NativeStream.Reader", "streamReader", false, null);
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "UnsafeStream.Reader", "streamReader", false, null);
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "S", "streamReader", false, new List<GenericTypeData>() { new GenericTypeData { Type = "S", TypeConstraints = new List<string>() { "unmanaged", "IStreamReader" }}});
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "DynamicBuffer<byte>", "buffer", true, null);
                            writer.WriteLine($""); 
                            GenerateExecuteFunction(writer, groupData, functionData, "DynamicBuffer<B>", "buffer", true, new List<GenericTypeData>() { new GenericTypeData { Type = "B", TypeConstraints = new List<string>() { "unmanaged", "IBufferElementData", "IByteBufferElement" }}});
                            writer.WriteLine($""); 
                            GenerateExecuteFunction(writer, groupData, functionData, "NativeList<byte>", "list", true, null);
                            writer.WriteLine($"");
                            GenerateExecuteFunction(writer, groupData, functionData, "UnsafeList<byte>", "list", true, null);
                            writer.WriteLine($"");                                
                            GenerateExecuteFunction(writer, groupData, functionData, "L", "list", true, new List<GenericTypeData>() { new GenericTypeData { Type = "L", TypeConstraints = new List<string>() { "unmanaged", "IByteList" }}});
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
            bool forUnionElement,
            GroupInterfaceData groupData,
            ElementData elementData,
            string collectionType,
            string collectionName,
            bool supportElementAccess,
            List<GenericTypeData> collectionGenericTypes)
        {
            string methodName = supportElementAccess ? AddElement : AppendElement;
            SourceGenUtils.GetGenericTypesStrings(collectionGenericTypes, out string allGenericTypes, out string allGenericTypeConstraints);

            if(forUnionElement)
            {
                writer.WriteLine($"public {(supportElementAccess ? $"{PolymorphicElementMetaData}" : "void")} {methodName}VariableSized{allGenericTypes}(ref {collectionType} {collectionName}){allGenericTypeConstraints}");
                writer.WriteInScope(() =>
                {
                    writer.WriteLine($"switch (TypeIdAndData.TypeId)");
                    writer.WriteInScope(() =>
                    {
                        foreach (ElementData tmpElementData in groupData.ElementDatas)
                        {
                            writer.WriteLine($"case {tmpElementData.Id}:");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"{(supportElementAccess ? $"return " : "")}{PolymorphicElementsUtility}.{methodName}(ref {collectionName}, {tmpElementData.Id}, TypeIdAndData.Data.{tmpElementData.Type});");
                                if(!supportElementAccess)
                                {
                                    writer.WriteLine($"break;");
                                }
                            });
                        }
                    });
                    if(supportElementAccess)
                    {
                        writer.WriteLine($"return default;");
                    }
                });
            }
            else
            {
                writer.WriteLine($"public static {(supportElementAccess ? $"{PolymorphicElementMetaData}" : "void")} {methodName}{allGenericTypes}(ref {collectionType} {collectionName}, {elementData.Type} e){allGenericTypeConstraints}");
                writer.WriteInScope(() =>
                {
                    writer.WriteLine($"{(supportElementAccess ? $"return " : "")}{PolymorphicElementsUtility}.{methodName}(ref {collectionName}, {elementData.Id}, e);");
                });
            }
        }

        private void GenerateInsertFunction(
            FileWriter writer, 
            bool forUnionElement,
            GroupInterfaceData groupData,
            ElementData elementData,
            string collectionType, 
            string collectionName,
            List<GenericTypeData> collectionGenericTypes)
        {
            SourceGenUtils.GetGenericTypesStrings(collectionGenericTypes, out string allGenericTypes, out string allGenericTypeConstraints);
            
            if(forUnionElement)
            {
                writer.WriteLine($"public {PolymorphicElementMetaData} {InsertElement}VariableSized{allGenericTypes}(ref {collectionType} {collectionName}, int atByteIndex){allGenericTypeConstraints}");
                writer.WriteInScope(() =>
                {
                    writer.WriteLine($"switch (TypeIdAndData.TypeId)");
                    writer.WriteInScope(() =>
                    {
                        foreach (ElementData tmpElementData in groupData.ElementDatas)
                        {
                            writer.WriteLine($"case {tmpElementData.Id}:");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"return {PolymorphicElementsUtility}.{InsertElement}(ref {collectionName}, atByteIndex, {tmpElementData.Id}, TypeIdAndData.Data.{tmpElementData.Type});");
                            });
                        }
                    });
                    writer.WriteLine($"return default;");
                });
            }
            else
            {
                writer.WriteLine($"public static {PolymorphicElementMetaData} {InsertElement}{allGenericTypes}(ref {collectionType} {collectionName}, int atByteIndex, {elementData.Type} e){allGenericTypeConstraints}");
                writer.WriteInScope(() =>
                {
                    writer.WriteLine($"return {PolymorphicElementsUtility}.{InsertElement}(ref {collectionName}, atByteIndex, {elementData.Id}, e);");
                });
            }
        }

        private void GenerateExecuteFunction(
            FileWriter writer, 
            GroupInterfaceData groupData, 
            FunctionData functionData, 
            string collectionType, 
            string collectionName, 
            bool supportIndexing, 
            List<GenericTypeData> collectionGenericTypes)
        {
            functionData.GetParameterStrings(out string parametersStringDeclaration, out string parametersStringInvocation, true);

            List<GenericTypeData> allGenericTypeDatas = new List<GenericTypeData>();
            if(collectionGenericTypes != null)
            {
                allGenericTypeDatas.AddRange(collectionGenericTypes);
            }
            if(functionData.GenericTypeDatas != null)
            {
                allGenericTypeDatas.AddRange(functionData.GenericTypeDatas);
            }
            SourceGenUtils.GetGenericTypesStrings(allGenericTypeDatas, out string allGenericTypes, out string allGenericTypeConstraints);

            writer.WriteLine($"public static {functionData.ReturnType} {functionData.Name}{allGenericTypes}(ref {collectionType} {collectionName}{(supportIndexing ? $", int {StartByteIndex}, out int {NextStartByteIndex}" : "")}{parametersStringDeclaration}, out bool success){allGenericTypeConstraints}");
            writer.WriteInScope((System.Action)(() =>
            {         
                writer.WriteLine($"success = false;");
                writer.WriteLine($"if ({ByteCollectionUtility}.{Read}(ref {collectionName}, {(supportIndexing ? $"{StartByteIndex}, out {NextStartByteIndex}," : "")} out ushort elementId))");
                writer.WriteInScope((System.Action)(() =>
                {
                    writer.WriteLine($"switch (elementId)");
                    writer.WriteInScope((System.Action)(() =>
                    {
                        foreach (ElementData elementData in groupData.ElementDatas)
                        {
                            writer.WriteLine($"case {elementData.Id}:");
                            writer.WriteInScope((System.Action)(() =>
                            {
                                List<GenericTypeData> readGenericTypeDatas = new List<GenericTypeData>();
                                readGenericTypeDatas.Add(new GenericTypeData
                                {
                                    Type = elementData.Type,
                                    TypeConstraints = new List<string>(),
                                });
                                if(collectionGenericTypes != null)
                                {
                                    readGenericTypeDatas.AddRange(collectionGenericTypes);
                                }
                                SourceGenUtils.GetGenericTypesStrings(readGenericTypeDatas, out string readGenericTypes, out _);

                                if(supportIndexing && functionData.WriteBackType == MethodWriteBackType.RefModify)
                                {
                                    writer.WriteLine($"ref {elementData.Type} e = ref {ByteCollectionUtility}.{ReadAsRef}{readGenericTypes}(ref {collectionName}, {(supportIndexing ? $"{NextStartByteIndex}, out {NextStartByteIndex}," : "")} out success);");
                                    writer.WriteLine($"if(success)");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"{(functionData.ReturnTypeIsVoid ? "" : "return ")}e.{functionData.Name}({parametersStringInvocation});");
                                    });
                                }
                                else
                                {
                                    if(supportIndexing)                                    
                                    {
                                    writer.WriteLine($"int {StartByteIndexOfElementValue} = {NextStartByteIndex};");
                                    }
                                    writer.WriteLine($"if ({ByteCollectionUtility}.{Read}(ref {collectionName}, {(supportIndexing ? $"{StartByteIndexOfElementValue}, out {NextStartByteIndex}," : "")} out {elementData.Type} e))");
                                    writer.WriteInScope((System.Action)(() =>
                                    {
                                        writer.WriteLine($"{(functionData.ReturnTypeIsVoid ? "" : $"{functionData.ReturnType} returnValue = ")}e.{functionData.Name}({parametersStringInvocation});");
                                        if(supportIndexing && functionData.WriteBackType == MethodWriteBackType.Write)
                                        {
                                            writer.WriteLine($"{ByteCollectionUtility}.{PESourceGenerator.WriteNoResize}(ref {collectionName}, {StartByteIndexOfElementValue}, e);");
                                        }
                                        writer.WriteLine($"success = true;");
                                        if(!functionData.ReturnTypeIsVoid)
                                        {
                                            writer.WriteLine($"return returnValue;");
                                        }
                                    }));
                                }
                                writer.WriteLine($"break;");
                            }));
                        }
                    }));
                }));    
                if(!functionData.ReturnTypeIsVoid)
                {
                    writer.WriteLine($"return default;");
                }
            }));
        }
    }
}