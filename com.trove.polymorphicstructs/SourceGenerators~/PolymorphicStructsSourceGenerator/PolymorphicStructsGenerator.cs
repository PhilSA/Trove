
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PolymorphicStructsSourceGenerators
{
    public class CompiledStructsForInterfaceData
    {
        public PolyInterfaceModel PolyInterfaceModel;
        public List<PolyStructModel> PolyStructModels;
    }

    public struct PolyInterfaceModel : IEquatable<PolyInterfaceModel>
    {
        public int ValueHash;

        public string MetaDataName;
        public StructModel TargetStructModel;
        public List<MethodModel> InterfaceMethodModels;

        // public List<string> Errors;

        public PolyInterfaceModel(
            string metaDataName,
            StructModel targetStructModel,
            List<MethodModel> interfaceMethodModels,
            List<string> errors)
        {
            MetaDataName = metaDataName;
            TargetStructModel = targetStructModel;
            InterfaceMethodModels = interfaceMethodModels;
            // Errors = errors;

            ValueHash = 0;
            RecomputeValueHash();
        }

        public void RecomputeValueHash()
        {
            string valuesString = $"{MetaDataName}{TargetStructModel.ValueHash}{InterfaceMethodModels.Count}";
            ValueHash = valuesString.GetHashCode();
        }

        public bool Equals(PolyInterfaceModel other)
        {
            return ValueHash == other.ValueHash;
        }
    }

    public struct PolyStructModel : IEquatable<PolyStructModel>
    {
        public int ValueHash;

        public StructModel StructModel;
        public List<string> InterfaceMetaDataNames;

        public PolyStructModel(StructModel structModel, List<string> interfaceMetaDataNames)
        {
            StructModel = structModel;
            InterfaceMetaDataNames = interfaceMetaDataNames;

            ValueHash = 0;
            RecomputeValueHash();
        }

        public void RecomputeValueHash()
        {
            string valuesString = $"{StructModel.ValueHash}{InterfaceMetaDataNames.Count}";
            for (int i = 0; InterfaceMetaDataNames.Count > i; i++)
            {
                valuesString += InterfaceMetaDataNames[i];
            }
            ValueHash = valuesString.GetHashCode();
        }

        public bool Equals(PolyStructModel other)
        {
            return ValueHash == other.ValueHash;
        }
    }

    public struct MethodModel : IEquatable<MethodModel>
    {
        public int ValueHash;

        public string Name;
        public bool HasNonVoidReturnType;
        public string ReturnTypeMetaDataName;
        public string MethodGenericTypesDeclaration;
        public string MethodGenericTypesConstraint;
        public string MethodParametersDefinition;
        public string MethodParametersInvoke;
        public bool HasWriteBack;

        public MethodModel(
            string name,
            bool hasNonVoidReturnType,
            string returnTypeMetaDataName,
            string methodGenericTypesDeclaration,
            string methodGenericTypesConstraint,
            string methodParametersDefinition,
            string methodParametersInvoke,
            bool hasWriteBack)
        {
            Name = name;
            HasNonVoidReturnType = hasNonVoidReturnType;
            ReturnTypeMetaDataName = returnTypeMetaDataName;
            MethodGenericTypesDeclaration = methodGenericTypesDeclaration;
            MethodGenericTypesConstraint = methodGenericTypesConstraint;
            MethodParametersDefinition = methodParametersDefinition;
            MethodParametersInvoke = methodParametersInvoke;
            HasWriteBack = hasWriteBack;

            ValueHash = 0;
            RecomputeValueHash();
        }

        public void RecomputeValueHash()
        {
            string valuesString = $"{Name}{HasNonVoidReturnType}{ReturnTypeMetaDataName}{MethodGenericTypesDeclaration}{MethodGenericTypesConstraint}{MethodParametersDefinition}{MethodParametersInvoke}{HasWriteBack}";
            ValueHash = valuesString.GetHashCode();
        }

        public bool Equals(MethodModel other)
        {
            return ValueHash == other.ValueHash;
        }
    }

    public struct StructModel : IEquatable<StructModel>
    {
        public int ValueHash;

        public string Name;
        public string Namespace;
        public string MetaDataName;

        public StructModel(
            string name,
            string _namespace,
            string metaDataName)
        {
            Name = name;
            Namespace = _namespace;
            MetaDataName = metaDataName;

            ValueHash = 0;
            RecomputeValueHash();
        }

        public void RecomputeValueHash()
        {
            string valuesString = $"{Name}{Namespace}{MetaDataName}";
            ValueHash = valuesString.GetHashCode();
        }

        public bool Equals(StructModel other)
        {
            return ValueHash == other.ValueHash;
        }
    }

    [Generator]
    public class PolymorphicStructsGenerator : IIncrementalGenerator
    {
        public const string NamespaceName_Trove = "Trove";
        public const string NamespaceName_PolymorphicStructs = NamespaceName_Trove + ".PolymorphicStructs";
        public const string NamespaceName_Generated = NamespaceName_PolymorphicStructs + ".Generated";

        public const string TypeName_Void = "void";
        public const string TypeName_TypeId = "ushort";
        public const string TypeName_PolymorphicTypeManagerAttribute = "PolymorphicTypeManagerInterfaceAttribute";
        public const string TypeName_PolymorphicUnionStructAttribute = "PolymorphicUnionStructInterfaceAttribute";
        public const string TypeName_PolymorphicStructAttribute = "PolymorphicStructAttribute";
        public const string TypeName_WriteBackStructDataAttribute = "WriteBackStructData";
        public const string TypeName_UnsafeUtility = "UnsafeUtility";
        public const string TypeName_ByteArrayUtilities = "ByteArrayUtilities";
        public const string TypeName_NativeStream_Writer = "NativeStream.Writer";
        public const string TypeName_UnsafeStream_Writer = "UnsafeStream.Writer";
        public const string TypeName_NativeStream_Reader = "NativeStream.Reader";
        public const string TypeName_UnsafeStream_Reader = "UnsafeStream.Reader";
        public const string TypeName_NativeList_Byte = "NativeList<byte>";
        public const string TypeName_UnsafeList_Byte = "UnsafeList<byte>";
        public const string TypeName_DynamicBuffer_Byte = "DynamicBuffer<byte>";
        public const string TypeName_FieldOffset = "FieldOffset";

        public const string MetaDataName_PolymorphicTypeManagerAttribute = NamespaceName_Generated + "." + TypeName_PolymorphicTypeManagerAttribute;
        public const string MetaDataName_PolymorphicUnionStructAttribute = NamespaceName_Generated + "." + TypeName_PolymorphicUnionStructAttribute;
        public const string MetaDataName_PolymorphicStructAttribute = NamespaceName_Generated + "." + TypeName_PolymorphicStructAttribute;
        public const string MetaDataName_WriteBackStructDataAttribute = NamespaceName_Generated + "." + TypeName_WriteBackStructDataAttribute;

        public const string FileName_Errors = "PolymorphicStructErrors";
        public const string FileName_GeneratedSuffixAndFileType = ".generated.cs";
        public const string FileName_PolymorphicTypeManagerAttribute = TypeName_PolymorphicTypeManagerAttribute + FileName_GeneratedSuffixAndFileType;
        public const string FileName_PolymorphiUnionStructAttribute = TypeName_PolymorphicUnionStructAttribute + FileName_GeneratedSuffixAndFileType;
        public const string FileName_PolymorphicStructAttribute = TypeName_PolymorphicStructAttribute + FileName_GeneratedSuffixAndFileType;
        public const string FileName_WriteBackStructData = TypeName_WriteBackStructDataAttribute + FileName_GeneratedSuffixAndFileType;

        public const string Decorator_InitializeOnLoadMethod = "[UnityEditor.InitializeOnLoadMethod]";
        public const string Decorator_MethodImpl_AggressiveInlining = "[MethodImpl(MethodImplOptions.AggressiveInlining)]";
        public const string Decorator_StructLayout_Explicit = "[StructLayout(LayoutKind.Explicit)]";

        public const string Name_ErrorIntro = "PolymorphicStructs source generator error:";
        public const string Name_Method_GetSizeForTypeId = "GetSizeForTypeId";
        public const string Name_Method_GetTotalWrittenSize = "GetTotalWrittenSize";
        public const string Name_Method_Write = "Write";
        public const string Name_Method_Add = "Add";
        public const string Name_Enum_TypeId = "TypeId";
        public const string Name_StreamWriter = "streamWriter";
        public const string Name_StreamReader = "streamReader";
        public const string Name_ByteList = "byteList";
        public const string Name_ByteBuffer = "byteBuffer";
        public const string Name_ByteIndex = "byteIndex";
        public const string Name_DataSize = "dataSize";
        public const string Name_ByteArrayPtr = "byteArrayPtr";
        public const string Name_ByteArrayLength = "byteArrayLength";

        public const string SizeOf_TypeId = "2";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Generate attributes used for marking
            GenerateAttributes(context);

            // Create the values provider for poly interfaces and structs
            IncrementalValuesProvider<PolyInterfaceModel> polyTypeManagerValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                MetaDataName_PolymorphicTypeManagerAttribute,
                PolyTypeManagerValuesProviderPredicate,
                PolyTypeManagerInterfaceValuesProviderTransform);
            IncrementalValuesProvider<PolyInterfaceModel> polyUnionStructValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                MetaDataName_PolymorphicUnionStructAttribute,
                PolyUnionStructInterfaceValuesProviderPredicate,
                PolyUnionStructInterfaceValuesProviderTransform);
            IncrementalValuesProvider<PolyStructModel> polyStructValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                MetaDataName_PolymorphicStructAttribute,
                PolyStructValuesProviderPredicate,
                PolyStructValuesProviderTransform);

            // Collect poly structs into an array, and create combined value providers of (PolyInterface, PolyStructsArray)
            IncrementalValueProvider<ImmutableArray<PolyInterfaceModel>> typeManagerInterfacesValueArrayProvider = polyTypeManagerValuesProvider.Collect();
            IncrementalValueProvider<ImmutableArray<PolyInterfaceModel>> unionStructInterfacesValueArrayProvider = polyUnionStructValuesProvider.Collect();
            IncrementalValueProvider<ImmutableArray<PolyStructModel>> polyStructsValueArrayProvider = polyStructValuesProvider.Collect();

            // Create the combimed interfaces + structs value providers
            IncrementalValueProvider<(ImmutableArray<PolyInterfaceModel> Left, ImmutableArray<PolyStructModel> Right)> polyTypeManagerAndStructsValuesProvider = typeManagerInterfacesValueArrayProvider.Combine(polyStructsValueArrayProvider);
            IncrementalValueProvider<(ImmutableArray<PolyInterfaceModel> Left, ImmutableArray<PolyStructModel> Right)> polyUnionStructAndStructsValuesProvider = unionStructInterfacesValueArrayProvider.Combine(polyStructsValueArrayProvider);

            // For each element matching this pipeline, handle output
            context.RegisterSourceOutput(polyTypeManagerAndStructsValuesProvider, TypeManagerSourceOutputter);
            context.RegisterSourceOutput(polyUnionStructAndStructsValuesProvider, UnionStructSourceOutputter);
        }

        private void GenerateAttributes(IncrementalGeneratorInitializationContext context)
        {
            // Generate attributes used in codegen
            context.RegisterPostInitializationOutput(i =>
            {
                FileWriter writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_Generated, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Interface)]");
                    writer.WriteLine($"internal class {TypeName_PolymorphicTypeManagerAttribute} : System.Attribute {{}}");
                });
                i.AddSource(FileName_PolymorphicTypeManagerAttribute, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_Generated, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Interface)]");
                    writer.WriteLine($"internal class {TypeName_PolymorphicUnionStructAttribute} : System.Attribute {{}}");
                });
                i.AddSource(FileName_PolymorphiUnionStructAttribute, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_Generated, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Struct)]");
                    writer.WriteLine($"internal class {TypeName_PolymorphicStructAttribute}: System.Attribute {{}}");
                });
                i.AddSource(FileName_PolymorphicStructAttribute, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_Generated, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Method)]");
                    writer.WriteLine($"internal class {TypeName_WriteBackStructDataAttribute}: System.Attribute {{}}");
                });
                i.AddSource(FileName_WriteBackStructData, writer.FileContents);
            });
        }

        private static bool PolyTypeManagerValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is InterfaceDeclarationSyntax;
        }

        private static PolyInterfaceModel PolyTypeManagerInterfaceValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return CommonInterfaceValuesProviderTransform(generatorAttributeSyntaxContext, "TypeManager", false);

        }

        private static bool PolyUnionStructInterfaceValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is InterfaceDeclarationSyntax;
        }

        private static PolyInterfaceModel PolyUnionStructInterfaceValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return CommonInterfaceValuesProviderTransform(generatorAttributeSyntaxContext, "UnionStruct", true);
        }

        private static bool PolyStructValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is StructDeclarationSyntax;
        }

        private static PolyStructModel PolyStructValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ITypeSymbol structTypeSymbol = (ITypeSymbol)generatorAttributeSyntaxContext.TargetSymbol;

            // TODO: support interface hierarchies?
            List<string> interfaceMetaDataNames = new List<string>();
            foreach (INamedTypeSymbol structInterface in structTypeSymbol.Interfaces)
            {
                interfaceMetaDataNames.Add(structInterface.MetadataName);
            }

            // TODO: deal with global or nested namespaces
            StructModel structModel = new StructModel(structTypeSymbol.Name, structTypeSymbol.ContainingNamespace.MetadataName, structTypeSymbol.MetadataName);
            return new PolyStructModel(structModel, interfaceMetaDataNames);
        }

        private static PolyInterfaceModel CommonInterfaceValuesProviderTransform(
            GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext,
            string targetStructNamePrefix,
            bool requireTargetStructHasNoFields)
        {
            List<string> errors = new List<string>();

            ITypeSymbol interfaceTypeSymbol = (ITypeSymbol)generatorAttributeSyntaxContext.TargetSymbol;

            string interfaceNamespaceMetadataName = SourceGenUtils.GetNamespaceMetaDataName(interfaceTypeSymbol);

            // Target struct
            string targetStructName = $"{targetStructNamePrefix}_{interfaceTypeSymbol.Name}";
            StructModel targetStructModel = new StructModel(targetStructName, interfaceNamespaceMetadataName, $"{interfaceNamespaceMetadataName}.{targetStructName}");

            // Get method infos
            List<MethodModel> interfaceMethodModels = new List<MethodModel>();
            foreach (ISymbol memberSymbol in interfaceTypeSymbol.GetMembers())
            {
                if (memberSymbol.Kind == SymbolKind.Method && memberSymbol is IMethodSymbol methodSymbol)
                {
                    MethodModel methodModel = new MethodModel();

                    methodModel.Name = methodSymbol.Name;
                    methodModel.HasNonVoidReturnType = methodSymbol.ReturnType.ToString() != TypeName_Void;
                    methodModel.ReturnTypeMetaDataName = methodSymbol.ReturnType.MetadataName;

                    // Generics
                    ImmutableArray<ITypeParameterSymbol> genericTypeParameters = methodSymbol.TypeParameters;
                    if (methodSymbol.IsGenericMethod && genericTypeParameters.Length > 0)
                    {
                        methodModel.MethodGenericTypesDeclaration = "";
                        methodModel.MethodGenericTypesConstraint = "";

                        string genericTypeName = "";
                        int genericTypesCounter = 0;
                        int genericTypeConstraintsCounterForType = 0;

                        foreach (ITypeParameterSymbol genericTypeParam in genericTypeParameters)
                        {
                            genericTypeName = $"T{genericTypesCounter}";

                            // Generic types declaration
                            if (genericTypesCounter == 0)
                            {
                                methodModel.MethodGenericTypesDeclaration += $"<";
                            }
                            else
                            {
                                methodModel.MethodGenericTypesDeclaration += $",";
                            }
                            methodModel.MethodGenericTypesDeclaration += $"{genericTypeName}";

                            // Generic type constraints
                            {
                                if (genericTypeParam.HasUnmanagedTypeConstraint)
                                {
                                    if (genericTypeConstraintsCounterForType == 0)
                                    {
                                        methodModel.MethodGenericTypesConstraint += $"where {genericTypeName} : ";
                                    }
                                    else
                                    {
                                        methodModel.MethodGenericTypesConstraint += $", ";
                                    }
                                    methodModel.MethodGenericTypesConstraint += $"unmanaged";
                                    genericTypeConstraintsCounterForType++;
                                }
                                else if (genericTypeParam.HasValueTypeConstraint)
                                {
                                    if (genericTypeConstraintsCounterForType == 0)
                                    {
                                        methodModel.MethodGenericTypesConstraint += $"where {genericTypeName} : ";
                                    }
                                    else
                                    {
                                        methodModel.MethodGenericTypesConstraint += $", ";
                                    }
                                    methodModel.MethodGenericTypesConstraint += $"struct";
                                    genericTypeConstraintsCounterForType++;
                                }
                                foreach (ITypeSymbol constraintType in genericTypeParam.ConstraintTypes)
                                {
                                    if (genericTypeConstraintsCounterForType == 0)
                                    {
                                        methodModel.MethodGenericTypesConstraint += $"where {genericTypeName} : ";
                                    }
                                    else
                                    {
                                        methodModel.MethodGenericTypesConstraint += $", ";
                                    }
                                    methodModel.MethodGenericTypesConstraint += $"{constraintType}";
                                    genericTypeConstraintsCounterForType++;
                                }
                            }

                            genericTypesCounter++;
                        }
                    }
                    else
                    {
                        methodModel.MethodGenericTypesDeclaration = "";
                        methodModel.MethodGenericTypesConstraint = "";
                    }

                    // Writeback
                    ImmutableArray<AttributeData> methodAttributes = memberSymbol.GetAttributes();
                    for (int i = 0; i < methodAttributes.Length; i++)
                    {
                        AttributeData attribute = methodAttributes[i];
                        if (attribute.AttributeClass.Name == TypeName_WriteBackStructDataAttribute)
                        {
                            methodModel.HasWriteBack = true;
                            break;
                        }
                    }

                    // Parameters
                    {
                        methodModel.MethodParametersDefinition = $"";
                        methodModel.MethodParametersInvoke = $"";
                        int parametersCounter = 0;
                        foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
                        {
                            if (parametersCounter > 0)
                            {
                                methodModel.MethodParametersDefinition += $", ";
                                methodModel.MethodParametersInvoke += $", ";
                            }

                            string refKindString = SourceGenUtils.RefKindToString(parameterSymbol.RefKind);
                            methodModel.MethodParametersDefinition += $"{refKindString} ";
                            methodModel.MethodParametersInvoke += $"{refKindString} ";

                            methodModel.MethodParametersDefinition += $"{parameterSymbol.Type} ";

                            methodModel.MethodParametersDefinition += $"{parameterSymbol.Name}";
                            methodModel.MethodParametersInvoke += $"{parameterSymbol.Name}";

                            parametersCounter++;
                        }
                    }

                    methodModel.RecomputeValueHash();
                    interfaceMethodModels.Add(methodModel);
                }
            }

            return new PolyInterfaceModel(interfaceTypeSymbol.MetadataName, targetStructModel, interfaceMethodModels, errors);
        }

        private static CompiledStructsForInterfaceData CreateCompiledStructsForInterfaceData(PolyInterfaceModel polyInterfaceModel, ImmutableArray<PolyStructModel> polyStructModels)
        {
            CompiledStructsForInterfaceData compiledStructsForInterfaceData = new CompiledStructsForInterfaceData();
            compiledStructsForInterfaceData.PolyInterfaceModel = polyInterfaceModel;
            compiledStructsForInterfaceData.PolyStructModels = new List<PolyStructModel>();

            // TODO: Sanity checks
            // - no 2 structs with same name (structModel.Name)

            // Add poly structs implementing this poly interface to a list
            ImmutableArray<PolyStructModel>.Enumerator polyStructModelsEnumerator = polyStructModels.GetEnumerator();
            while (polyStructModelsEnumerator.MoveNext())
            {
                List<string> structInterfaces = polyStructModelsEnumerator.Current.InterfaceMetaDataNames;
                for (int i = 0; i < structInterfaces.Count; i++)
                {
                    if (structInterfaces[i] == compiledStructsForInterfaceData.PolyInterfaceModel.MetaDataName)
                    {
                        compiledStructsForInterfaceData.PolyStructModels.Add(polyStructModelsEnumerator.Current);
                        break;
                    }
                }
            }

            return compiledStructsForInterfaceData;
        }

        private static void TypeManagerSourceOutputter(SourceProductionContext sourceProductionContext, (ImmutableArray<PolyInterfaceModel> Left, ImmutableArray<PolyStructModel> Right) source)
        {
            sourceProductionContext.CancellationToken.ThrowIfCancellationRequested();

            if (source.Left.Length > 0)
            {
                for (int a = 0; a < source.Left.Length; a++)
                {
                    CompiledStructsForInterfaceData compiledCodeData = CreateCompiledStructsForInterfaceData(source.Left[a], source.Right);
                    PolyInterfaceModel polyInterfaceModel = compiledCodeData.PolyInterfaceModel;

                    FileWriter writer = new FileWriter();

                    // Usings
                    writer.WriteUsingsAndRemoveDuplicates(GetCommonUsings());

                    writer.WriteLine($"");

                    writer.WriteInNamespace(polyInterfaceModel.TargetStructModel.Namespace, () =>
                    {
                        writer.WriteLine($"public unsafe partial struct {polyInterfaceModel.TargetStructModel.Name}");
                        writer.WriteInScope(() =>
                        {
                            // Types enum
                            GenerateTypeIdEnum(writer, compiledCodeData.PolyStructModels);

                            writer.WriteLine($"");

                            // GetSizeForTypeId 
                            writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                            writer.WriteLine($"public int {Name_Method_GetSizeForTypeId}({TypeName_TypeId} typeId)");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"switch (({Name_Enum_TypeId})typeId)");
                                writer.WriteInScope(() =>
                                {
                                    for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                                    {
                                        PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];

                                        writer.WriteLine($"case {Name_Enum_TypeId}.{polyStructModel.StructModel.Name}:");
                                        writer.WriteInScope(() =>
                                        {
                                            writer.WriteLine($"return {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                        });
                                    }
                                });

                                writer.WriteLine($"return 0;");
                            });

                            writer.WriteLine($"");

                            // GetTotalWrittenSize
                            for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                            {
                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];

                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static int {Name_Method_GetTotalWrittenSize}(in {polyStructModel.StructModel.MetaDataName} s)");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"return {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                });

                                writer.WriteLine($"");
                            }

                            // Write
                            for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                            {
                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];

                                // To ptr
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static void {Name_Method_Write}(byte* {Name_ByteArrayPtr}, int {Name_ByteIndex}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_DataSize})");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"{Name_DataSize} = {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                    writer.WriteLine($"{TypeName_ByteArrayUtilities}.WriteValues({Name_ByteArrayPtr}, {Name_ByteIndex}, ({TypeName_TypeId}){Name_Enum_TypeId}.{polyStructModel.StructModel.Name}, s);");
                                });

                                writer.WriteLine($"");

                                // To NativeStream
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static void {Name_Method_Add}(ref {TypeName_NativeStream_Writer} {Name_StreamWriter}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_DataSize})");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"{Name_DataSize} = {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                    writer.WriteLine($"{TypeName_ByteArrayUtilities}.AddValues(ref {Name_StreamWriter}, ({TypeName_TypeId}){Name_Enum_TypeId}.{polyStructModel.StructModel.Name}, s);");
                                });

                                writer.WriteLine($"");

                                // To UnsafeStream
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static void {Name_Method_Add}(ref {TypeName_UnsafeStream_Writer} {Name_StreamWriter}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_DataSize})");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"{Name_DataSize} = {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                    writer.WriteLine($"{TypeName_ByteArrayUtilities}.AddValues(ref {Name_StreamWriter}, ({TypeName_TypeId}){Name_Enum_TypeId}.{polyStructModel.StructModel.Name}, s);");
                                });

                                writer.WriteLine($"");

                                // To NativeList<byte>
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static void {Name_Method_Add}(ref {TypeName_NativeList_Byte} {Name_ByteList}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_ByteIndex}, out int {Name_DataSize})");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"{Name_ByteIndex} = {Name_ByteList}.Length;");
                                    writer.WriteLine($"{Name_DataSize} = {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                    writer.WriteLine($"{TypeName_ByteArrayUtilities}.AddValues(ref {Name_ByteList}, ({TypeName_TypeId}){Name_Enum_TypeId}.{polyStructModel.StructModel.Name}, s);");
                                });

                                writer.WriteLine($"");

                                // To UnsafeList<byte>
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static void {Name_Method_Add}(ref {TypeName_UnsafeList_Byte} {Name_ByteList}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_ByteIndex}, out int {Name_DataSize})");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"{Name_ByteIndex} = {Name_ByteList}.Length;");
                                    writer.WriteLine($"{Name_DataSize} = {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                    writer.WriteLine($"{TypeName_ByteArrayUtilities}.AddValues(ref {Name_ByteList}, ({TypeName_TypeId}){Name_Enum_TypeId}.{polyStructModel.StructModel.Name}, s);");
                                });

                                writer.WriteLine($"");

                                // To DynamicBuffer<byte>
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static void {Name_Method_Add}(ref {TypeName_DynamicBuffer_Byte} {Name_ByteBuffer}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_ByteIndex}, out int {Name_DataSize})");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"{Name_ByteIndex} = {Name_ByteBuffer}.Length;");
                                    writer.WriteLine($"{Name_DataSize} = {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                    writer.WriteLine($"{TypeName_ByteArrayUtilities}.AddValues(ref {Name_ByteBuffer}, ({TypeName_TypeId}){Name_Enum_TypeId}.{polyStructModel.StructModel.Name}, s);");
                                });
                            }

                            writer.WriteLine($"");

                            // Method executors
                            for (int i = 0; i < polyInterfaceModel.InterfaceMethodModels.Count; i++)
                            {
                                MethodModel methodModel = polyInterfaceModel.InterfaceMethodModels[i];

                                // From Ptr
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}(byte* {Name_ByteArrayPtr}, int {Name_ByteArrayLength}, ref int {Name_ByteIndex}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                                writer.WriteInScope(() =>
                                {

                                    writer.WriteLine($"int startByteIndex = {Name_ByteIndex};");
                                    writer.WriteLine($"");

                                    // If can read typeId
                                    writer.WriteLine($"if ({TypeName_ByteArrayUtilities}.CanReadValue({Name_ByteArrayLength}, {Name_ByteIndex}, {SizeOf_TypeId}))");
                                    writer.WriteInScope(() =>
                                    {

                                        // Read typeId
                                        writer.WriteLine($"{TypeName_ByteArrayUtilities}.ReadValue({Name_ByteArrayPtr}, ref {Name_ByteIndex}, out {TypeName_TypeId} typeId);");

                                        writer.WriteLine($"");

                                        // Switch over typeId
                                        writer.WriteLine($"switch (({Name_Enum_TypeId})typeId)");
                                        writer.WriteInScope(() =>
                                        {
                                            for (int t = 0; t < compiledCodeData.PolyStructModels.Count; t++)
                                            {
                                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[t];

                                                // Case
                                                writer.WriteLine($"case {Name_Enum_TypeId}.{polyStructModel.StructModel.Name}:");
                                                writer.WriteInScope(() =>
                                                {
                                                    // If can read struct
                                                    writer.WriteLine($"if ({TypeName_ByteArrayUtilities}.CanReadValue<{polyStructModel.StructModel.MetaDataName}>({Name_ByteArrayLength}, {Name_ByteIndex}))");
                                                    writer.WriteInScope(() =>
                                                    {
                                                        // Read struct
                                                        writer.WriteLine($"{TypeName_ByteArrayUtilities}.ReadValue({Name_ByteArrayPtr}, ref {Name_ByteIndex}, out {polyStructModel.StructModel.MetaDataName} __s);");

                                                        // Invoke method on struct
                                                        writer.WriteLine($"__s.{methodModel.Name}({methodModel.MethodParametersInvoke});");

                                                        // Handle struct writeback
                                                        if (methodModel.HasWriteBack)
                                                        {
                                                            writer.WriteLine($"{TypeName_ByteArrayUtilities}.WriteValue({Name_ByteArrayPtr}, startByteIndex, __s);");
                                                        }

                                                        writer.WriteLine($"return true;");
                                                    });
                                                    writer.WriteLine($"return false;");
                                                });
                                            }
                                        });
                                    });

                                    writer.WriteLine($"");

                                    writer.WriteLine($"return false;");
                                });

                                writer.WriteLine($"");

                                // From NativeStream
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}(ref {TypeName_NativeStream_Reader} {Name_StreamReader}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                                writer.WriteInScope(() =>
                                {
                                    // If can read typeId and struct
                                    writer.WriteLine($"if ({Name_StreamReader}.RemainingItemCount > 0)");
                                    writer.WriteInScope(() =>
                                    {
                                        // Read typeId
                                        writer.WriteLine($"{TypeName_TypeId} typeId = {Name_StreamReader}.Read<{TypeName_TypeId}>();");

                                        writer.WriteLine($"");

                                        // Switch over typeId
                                        writer.WriteLine($"switch (({Name_Enum_TypeId})typeId)");
                                        writer.WriteInScope(() =>
                                        {
                                            for (int t = 0; t < compiledCodeData.PolyStructModels.Count; t++)
                                            {
                                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[t];

                                                // Case
                                                writer.WriteLine($"case {Name_Enum_TypeId}.{polyStructModel.StructModel.Name}:");
                                                writer.WriteInScope(() =>
                                                {
                                                    // Read struct
                                                    writer.WriteLine($"{polyStructModel.StructModel.MetaDataName} __s = {Name_StreamReader}.Read<{polyStructModel.StructModel.MetaDataName}>();");

                                                    // Invoke method on struct
                                                    writer.WriteLine($"__s.{methodModel.Name}({methodModel.MethodParametersInvoke});");

                                                    writer.WriteLine($"return true;");
                                                });
                                            }
                                        });
                                    });

                                    writer.WriteLine($"");

                                    writer.WriteLine($"return false;");
                                });

                                writer.WriteLine($"");

                                // From UnsafeStream
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}(ref {TypeName_UnsafeStream_Reader} {Name_StreamReader}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                                writer.WriteInScope(() =>
                                {
                                    // If can read typeId and struct
                                    writer.WriteLine($"if ({Name_StreamReader}.RemainingItemCount > 0)");
                                    writer.WriteInScope(() =>
                                    {
                                        // Read typeId
                                        writer.WriteLine($"{TypeName_TypeId} typeId = {Name_StreamReader}.Read<{TypeName_TypeId}>();");

                                        writer.WriteLine($"");

                                        // Switch over typeId
                                        writer.WriteLine($"switch (({Name_Enum_TypeId})typeId)");
                                        writer.WriteInScope(() =>
                                        {
                                            for (int t = 0; t < compiledCodeData.PolyStructModels.Count; t++)
                                            {
                                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[t];

                                                // Case
                                                writer.WriteLine($"case {Name_Enum_TypeId}.{polyStructModel.StructModel.Name}:");
                                                writer.WriteInScope(() =>
                                                {
                                                    // Read struct
                                                    writer.WriteLine($"{polyStructModel.StructModel.MetaDataName} __s = {Name_StreamReader}.Read<{polyStructModel.StructModel.MetaDataName}>();");

                                                    // Invoke method on struct
                                                    writer.WriteLine($"__s.{methodModel.Name}({methodModel.MethodParametersInvoke});");

                                                    writer.WriteLine($"return true;");
                                                });
                                            }
                                        });
                                    });

                                    writer.WriteLine($"");

                                    writer.WriteLine($"return false;");
                                });

                                writer.WriteLine($"");

                                // From NativeList
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}({TypeName_NativeList_Byte} {Name_ByteList}, ref int {Name_ByteIndex}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"return {methodModel.Name}({Name_ByteList}.GetUnsafePtr(), {Name_ByteList}.Length, ref {Name_ByteIndex}, {methodModel.MethodParametersInvoke});");
                                });

                                writer.WriteLine($"");

                                // From UnsafeList
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}({TypeName_UnsafeList_Byte} {Name_ByteList}, ref int {Name_ByteIndex}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"return {methodModel.Name}({Name_ByteList}.Ptr, {Name_ByteList}.Length, ref {Name_ByteIndex}, {methodModel.MethodParametersInvoke});");
                                });

                                writer.WriteLine($"");

                                // From DynamicBuffer
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}({TypeName_DynamicBuffer_Byte} {Name_ByteBuffer}, ref int {Name_ByteIndex}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"return {methodModel.Name}((byte*){Name_ByteBuffer}.GetUnsafePtr(), {Name_ByteBuffer}.Length, ref {Name_ByteIndex}, {methodModel.MethodParametersInvoke});");
                                });

                                writer.WriteLine($"");
                            }
                        });
                    });
                    writer.WriteLine($"");

                    SourceText sourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
                    sourceProductionContext.AddSource($"{polyInterfaceModel.TargetStructModel.Name}{FileName_GeneratedSuffixAndFileType}", sourceText);
                }

                //DebugOutputter(sourceProductionContext, debug);
            }
        }

        private static void UnionStructSourceOutputter(SourceProductionContext sourceProductionContext, (ImmutableArray<PolyInterfaceModel> Left, ImmutableArray<PolyStructModel> Right) source)
        {
            sourceProductionContext.CancellationToken.ThrowIfCancellationRequested();

            if (source.Left.Length > 0)
            {
                for (int a = 0; a < source.Left.Length; a++)
                {
                    CompiledStructsForInterfaceData compiledCodeData = CreateCompiledStructsForInterfaceData(source.Left[a], source.Right);
                    PolyInterfaceModel polyInterfaceModel = compiledCodeData.PolyInterfaceModel;

                    FileWriter writer = new FileWriter();

                    // Usings
                    writer.WriteUsingsAndRemoveDuplicates(GetCommonUsings());

                    writer.WriteLine($"");

                    writer.WriteInNamespace(polyInterfaceModel.TargetStructModel.Namespace, () =>
                    {
                        writer.WriteLine($"{Decorator_StructLayout_Explicit}");
                        writer.WriteLine($"public unsafe partial struct {polyInterfaceModel.TargetStructModel.Name}");
                        writer.WriteInScope(() =>
                        {
                            // Types enum
                            GenerateTypeIdEnum(writer, compiledCodeData.PolyStructModels);

                            writer.WriteLine($"");

                            // Union fields
                            writer.WriteLine($"[{TypeName_FieldOffset}(0)]");
                            writer.WriteLine($"public {Name_Enum_TypeId} CurrentTypeId;");
                            for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                            {
                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];
                                writer.WriteLine($"[{TypeName_FieldOffset}({SizeOf_TypeId})]");
                                writer.WriteLine($"public {polyStructModel.StructModel.MetaDataName} Field_{polyStructModel.StructModel.Name};");
                            }

                            writer.WriteLine($"");

                            // Implicit casts
                            for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                            {
                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];

                                // Cast struct to union struct
                                writer.WriteLine($"public static implicit operator {polyInterfaceModel.TargetStructModel.Name}({polyStructModel.StructModel.MetaDataName} s)");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"return new {polyInterfaceModel.TargetStructModel.Name}");
                                    writer.WriteInScope(() =>
                                    {
                                        writer.WriteLine($"CurrentTypeId = {Name_Enum_TypeId}.{polyStructModel.StructModel.Name},");
                                        writer.WriteLine($"Field_{polyStructModel.StructModel.Name} = s,");
                                    }, ";");
                                });

                                writer.WriteLine($"");

                                // Cast union struct to struct
                                writer.WriteLine($"public static implicit operator {polyStructModel.StructModel.MetaDataName}({polyInterfaceModel.TargetStructModel.Name} s)");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"return s.Field_{polyStructModel.StructModel.Name};");
                                });

                                writer.WriteLine($"");
                            }

                            writer.WriteLine($"");

                            // Methods
                            for (int i = 0; i < polyInterfaceModel.InterfaceMethodModels.Count; i++)
                            {
                                MethodModel methodModel = polyInterfaceModel.InterfaceMethodModels[i];

                                string curatedReturnType = methodModel.HasNonVoidReturnType ? methodModel.ReturnTypeMetaDataName : "void";

                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public {curatedReturnType} {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}({methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                                writer.WriteInScope(() =>
                                {
                                    // Switch over typeId
                                    writer.WriteLine($"switch (CurrentTypeId)");
                                    writer.WriteInScope(() =>
                                    {
                                        for (int t = 0; t < compiledCodeData.PolyStructModels.Count; t++)
                                        {
                                            PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[t];

                                            // Case
                                            writer.WriteLine($"case {Name_Enum_TypeId}.{polyStructModel.StructModel.Name}:");
                                            writer.WriteInScope(() =>
                                            {
                                                // Invoke method on struct
                                                if (methodModel.HasNonVoidReturnType)
                                                {
                                                    writer.WriteLine($"return Field_{polyStructModel.StructModel.Name}.{methodModel.Name}({methodModel.MethodParametersInvoke});");
                                                }
                                                else
                                                {
                                                    writer.WriteLine($"Field_{polyStructModel.StructModel.Name}.{methodModel.Name}({methodModel.MethodParametersInvoke});");
                                                    writer.WriteLine($"break;");
                                                }
                                            });
                                        }
                                    });

                                    if (methodModel.HasNonVoidReturnType)
                                    {
                                        writer.WriteLine($"return default;");
                                    }
                                });

                                writer.WriteLine($"");
                            }
                        });
                    });
                    writer.WriteLine($"");

                    SourceText sourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
                    sourceProductionContext.AddSource($"{polyInterfaceModel.TargetStructModel.Name}{FileName_GeneratedSuffixAndFileType}", sourceText);
                }
            }
        }

        private static void GenerateTypeIdEnum(FileWriter writer, List<PolyStructModel> polyStructModels)
        {
            writer.WriteLine($"public enum {Name_Enum_TypeId} : {TypeName_TypeId}");
            writer.WriteInScope(() =>
            {
                for (int i = 0; i < polyStructModels.Count; i++)
                {
                    PolyStructModel polyStructModel = polyStructModels[i];
                    writer.WriteLine($"{polyStructModel.StructModel.Name},");
                }
            });
        }

        private static void DebugOutputter(SourceProductionContext sourceProductionContext, string debug)
        {
            sourceProductionContext.CancellationToken.ThrowIfCancellationRequested();

            FileWriter writer = new FileWriter();
            writer.WriteLine($"internal static class SourceGenDebugOutputter");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"{Decorator_InitializeOnLoadMethod}");
                writer.WriteLine($"public static void DebugOutput()");
                writer.WriteInScope(() =>
                {
                    writer.WriteLine($"UnityEngine.Debug.Log($\"SourceGenDebugOutputter - {{typeof(SourceGenDebugOutputter).Assembly}} \\n\\n{debug}\");");
                });
            });

            SourceText sourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
            sourceProductionContext.AddSource($"SourceGenDebugOutputter{FileName_GeneratedSuffixAndFileType}", sourceText);
        }

        private static List<string> GetCommonUsings()
        {
            return new List<string>
            {
                $"System",
                $"{NamespaceName_Trove}",
                $"Unity.Entities",
                $"Unity.Collections",
                $"Unity.Collections.LowLevel.Unsafe",
                $"System.Runtime.InteropServices",
                $"System.Runtime.CompilerServices",
            };
        }
    }
}