
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace PolymorphicStructsSourceGenerators
{
    public struct LogMessage
    {
        public enum MsgType
        {
            Log,
            Error,
        }

        public string Message;
        public MsgType Type;
    }

    public class CompiledStructsForInterfaceData
    {
        public PolyInterfaceModel PolyInterfaceModel;
        public List<PolyStructModel> PolyStructModels;
    }

    public struct PolyInterfaceModel : IEquatable<PolyInterfaceModel>
    {
        public int ValueHash;

        public string Name;
        public string TypeName;
        public StructModel TargetStructModel;
        public bool AllowEntitiesAndBlobs;
        public bool IsMergedFieldsStruct;
        public bool IsGeneric;
        public List<MethodModel> InterfaceMethodModels;
        public List<PropertyModel> InterfacePropertyModels;

        public PolyInterfaceModel(
            string name,
            string typeName,
            string typeNameOLD,
            StructModel targetStructModel,
            bool allowEntitiesAndBlobs,
            bool isMergedFieldsStruct,
            bool isGeneric,
            List<MethodModel> interfaceMethodModels,
            List<PropertyModel> interfacePropertyModels)
        {
            Name = name;
            TypeName = typeName;
            TargetStructModel = targetStructModel;
            AllowEntitiesAndBlobs = allowEntitiesAndBlobs;
            IsMergedFieldsStruct = isMergedFieldsStruct;
            IsGeneric = isGeneric;
            InterfaceMethodModels = interfaceMethodModels;
            InterfacePropertyModels = interfacePropertyModels;

            // Ok to allow entities and blobs in merged fields structs
            if(IsMergedFieldsStruct)
            {
                AllowEntitiesAndBlobs = true;
            }

            ValueHash = 0;
            RecomputeValueHash();
        }

        public void RecomputeValueHash()
        {
            string valuesString = $"{Name}{TypeName}{TargetStructModel.ValueHash}{AllowEntitiesAndBlobs}{IsMergedFieldsStruct}{InterfaceMethodModels.Count}{InterfacePropertyModels.Count}";
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
        public List<string> InterfaceTypeNames;
        public ITypeSymbol StructTypeSymbol;

        public PolyStructModel(StructModel structModel, ITypeSymbol structTypeSymbol, List<string> interfaceTypeNames)
        {
            StructModel = structModel;
            InterfaceTypeNames = interfaceTypeNames;
            StructTypeSymbol = structTypeSymbol;

            ValueHash = 0;
            RecomputeValueHash();
        }

        public void RecomputeValueHash()
        {
            string valuesString = $"{StructModel.ValueHash}{InterfaceTypeNames.Count}";
            for (int i = 0; InterfaceTypeNames.Count > i; i++)
            {
                valuesString += InterfaceTypeNames[i];
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
        public string ReturnTypeName;
        public string MethodGenericTypesDeclaration;
        public string MethodGenericTypesConstraint;
        public string MethodParametersDefinition;
        public string MethodParametersInvoke;
        public string MethodOutParameterNames;
        public Accessibility Accessibility;

        public void RecomputeValueHash()
        {
            string valuesString = $"{Name}{HasNonVoidReturnType}{ReturnTypeName}{MethodGenericTypesDeclaration}{MethodGenericTypesConstraint}{MethodParametersDefinition}{MethodParametersInvoke}";
            ValueHash = valuesString.GetHashCode();
        }

        public bool Equals(MethodModel other)
        {
            return ValueHash == other.ValueHash;
        }
    }

    public struct PropertyModel : IEquatable<MethodModel>
    {
        public int ValueHash;

        public string Name;
        public string TypeName;
        public bool HasGet;
        public bool HasSet;
        public Accessibility Accessibility;

        public void RecomputeValueHash()
        {
            string valuesString = $"{Name}{TypeName}{HasGet}{HasSet}";
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
        public string TypeName;
        public string NamespaceName;

        public StructModel(
            string name,
            string typeName,
            string _namespace)
        {
            Name = name;
            TypeName = typeName;
            NamespaceName = _namespace;

            ValueHash = 0;
            RecomputeValueHash();
        }

        public void RecomputeValueHash()
        {
            string valuesString = $"{Name}{TypeName}{NamespaceName}";
            ValueHash = valuesString.GetHashCode();
        }

        public bool Equals(StructModel other)
        {
            return ValueHash == other.ValueHash;
        }
    }

    public struct MergedFieldModel
    {
        public string FieldTypeName;
        public int TypeCounter;

        public override bool Equals(object obj)
        {
            return obj is MergedFieldModel model &&
                   FieldTypeName == model.FieldTypeName &&
                   TypeCounter == model.TypeCounter;
        }

        public override int GetHashCode()
        {
            int hashCode = 2091989527;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FieldTypeName);
            hashCode = hashCode * -1521134295 + TypeCounter.GetHashCode();
            return hashCode;
        }
    }

    public struct SpecificFieldModel
    {
        public string StructName;
        public string FieldTypeName;
        public string FieldName;

        public override bool Equals(object obj)
        {
            return obj is SpecificFieldModel model &&
                   StructName == model.StructName &&
                   FieldTypeName == model.FieldTypeName &&
                   FieldName == model.FieldName;
        }

        public override int GetHashCode()
        {
            int hashCode = -112254477;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(StructName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FieldTypeName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FieldName);
            return hashCode;
        }
    }

    public class MergedFieldsData
    {
        public List<MergedFieldModel> MergedFieldModels;
        public Dictionary<MergedFieldModel, Dictionary<string, SpecificFieldModel>> MergedFieldToSpecificFieldsMap;
        public Dictionary<SpecificFieldModel, MergedFieldModel> SpecificFieldToMergedFieldMap;
        public Dictionary<MergedFieldModel, int> MergedFieldToMergedFieldIdMap;
        public Dictionary<string, int> StructFieldTypeCounter;

        public MergedFieldsData()
        {
            MergedFieldModels = new List<MergedFieldModel>();
            MergedFieldToSpecificFieldsMap = new Dictionary<MergedFieldModel, Dictionary<string, SpecificFieldModel>>();
            SpecificFieldToMergedFieldMap = new Dictionary<SpecificFieldModel, MergedFieldModel>();
            MergedFieldToMergedFieldIdMap = new Dictionary<MergedFieldModel, int>();
            StructFieldTypeCounter = new Dictionary<string, int>();
        }

        public void Clear()
        {
            MergedFieldModels.Clear();
            MergedFieldToSpecificFieldsMap.Clear();
            SpecificFieldToMergedFieldMap.Clear();
            MergedFieldToMergedFieldIdMap.Clear();
            StructFieldTypeCounter.Clear();
        }

        public string GetMergedFieldName(MergedFieldModel mergedFieldModel)
        {
            return $"Field{MergedFieldToMergedFieldIdMap[mergedFieldModel].ToString()}";
        }
    }

    [Generator]
    public class PolymorphicStructsGenerator : IIncrementalGenerator
    {
        public const bool EnableDebug = false;

        public const string NamespaceName_Trove = "Trove";
        public const string NamespaceName_PolymorphicStructs = NamespaceName_Trove + ".PolymorphicStructs";

        public const string TypeName_Void = "void";
        public const string TypeName_IPolymorphicObject = "IPolymorphicObject";
        public const string TypeName_PolymorphicStructInterfaceAttribute = "PolymorphicStructInterfaceAttribute";
        public const string TypeName_PolymorphicStructAttribute = "PolymorphicStructAttribute";
        public const string TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute = "AllowEntitiesAndBlobsInPolymorphicStructAttribute";
        public const string TypeName_IsMergedFieldsPolymorphicStructAttribute = "IsMergedFieldsPolymorphicStructAttribute";
        public const string TypeName_UnsafeUtility = "UnsafeUtility";
        public const string TypeName_FieldOffset = "FieldOffset";

        public const string FullTypeName_PolymorphicStructInterfaceAttribute = NamespaceName_PolymorphicStructs + "." + TypeName_PolymorphicStructInterfaceAttribute;
        public const string FullTypeName_PolymorphicStructAttribute = NamespaceName_PolymorphicStructs + "." + TypeName_PolymorphicStructAttribute;
        public const string FullTypeName_Entity = "Unity.Entities.Entity";
        public const string FullTypeName_BlobAssetReference = "Unity.Entities.BlobAssetReference";
        public const string FullTypeName_BlobString = "Unity.Entities.BlobString";
        public const string FullTypeName_BlobArray = "Unity.Entities.BlobArray";
        public const string FullTypeName_BlobPtr = "Unity.Entities.BlobPtr";

        public const string FileName_GeneratedSuffixAndFileType = ".generated.cs";

        public const string Decorator_InitializeOnLoadMethod = "[UnityEditor.InitializeOnLoadMethod]";
        public const string Decorator_MethodImpl_AggressiveInlining = "[MethodImpl(MethodImplOptions.AggressiveInlining)]";
        public const string Decorator_StructLayout_Explicit = "[StructLayout(LayoutKind.Explicit)]";

        public const string Name_Enum_TypeId = "TypeId";

        public const string SizeOf_TypeId = "4";

        public static List<LogMessage> LogMessages = new List<LogMessage>();
        public static List<ISymbol> InterfaceMembers = new List<ISymbol>();
        public static List<ITypeSymbol> SymbolsToProcess = new List<ITypeSymbol>();
        public static MergedFieldsData MergedFieldsData = new MergedFieldsData();

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Generate attributes used for marking
            GenerateAttributes(context);

            // Create the values provider for poly interfaces and structs
            IncrementalValuesProvider<PolyInterfaceModel> polyStructInterfaceValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                FullTypeName_PolymorphicStructInterfaceAttribute,
                PolyStructInterfaceValuesProviderPredicate,
                PolyStructInterfaceValuesProviderTransform);
            IncrementalValuesProvider<PolyStructModel> polyStructValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                FullTypeName_PolymorphicStructAttribute,
                PolyStructValuesProviderPredicate,
                PolyStructValuesProviderTransform);

            // Collect poly structs into an array, and create combined value providers of (PolyInterface, PolyStructsArray)
            IncrementalValueProvider<ImmutableArray<PolyInterfaceModel>> polyStructInterfacesValueArrayProvider = polyStructInterfaceValuesProvider.Collect();
            IncrementalValueProvider<ImmutableArray<PolyStructModel>> polyStructsValueArrayProvider = polyStructValuesProvider.Collect();

            // Create the combimed interfaces + structs value providers
            IncrementalValueProvider<(ImmutableArray<PolyInterfaceModel> Left, ImmutableArray<PolyStructModel> Right)> polyStructInterfaceAndStructsValuesProvider = polyStructInterfacesValueArrayProvider.Combine(polyStructsValueArrayProvider);

            // For each element matching this pipeline, handle output
            context.RegisterSourceOutput(polyStructInterfaceAndStructsValuesProvider, PolymorphicStructSourceOutputter);
        }

        private void GenerateAttributes(IncrementalGeneratorInitializationContext context)
        {
            // Generate attributes used in codegen
            context.RegisterPostInitializationOutput(i =>
            {
                FileWriter writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_PolymorphicStructs, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Interface)]");
                    writer.WriteLine($"internal class {TypeName_PolymorphicStructInterfaceAttribute} : System.Attribute {{}}");
                });
                i.AddSource(TypeName_PolymorphicStructInterfaceAttribute + FileName_GeneratedSuffixAndFileType, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_PolymorphicStructs, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Struct)]");
                    writer.WriteLine($"internal class {TypeName_PolymorphicStructAttribute}: System.Attribute {{}}");
                });
                i.AddSource(TypeName_PolymorphicStructAttribute + FileName_GeneratedSuffixAndFileType, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_PolymorphicStructs, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Interface)]");
                    writer.WriteLine($"internal class {TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute}: System.Attribute {{}}");
                });
                i.AddSource(TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute + FileName_GeneratedSuffixAndFileType, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_PolymorphicStructs, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Interface)]");
                    writer.WriteLine($"internal class {TypeName_IsMergedFieldsPolymorphicStructAttribute}: System.Attribute {{}}");
                });
                i.AddSource(TypeName_IsMergedFieldsPolymorphicStructAttribute + FileName_GeneratedSuffixAndFileType, writer.FileContents);
            });
        }

        private static bool PolyStructInterfaceValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is InterfaceDeclarationSyntax;
        }

        private static PolyInterfaceModel PolyStructInterfaceValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ITypeSymbol interfaceTypeSymbol = (ITypeSymbol)generatorAttributeSyntaxContext.TargetSymbol;

            string targetStructName = interfaceTypeSymbol.Name;
            if(targetStructName.StartsWith("I"))
            {
                targetStructName = targetStructName.Substring(1);
            }
            targetStructName = $"Poly{targetStructName}";


            // Create the data of the "target struct"; the poly struct that will be generated
            string interfaceNamespaceName = SourceGenUtils.GetFullNamespaceTypeName(interfaceTypeSymbol);
            string targetStructTypeName = string.IsNullOrEmpty(interfaceNamespaceName) ? targetStructName : $"{interfaceNamespaceName}.{targetStructName}";
            StructModel targetStructModel = new StructModel(targetStructName, targetStructTypeName, interfaceNamespaceName);

            // Check attributes
            bool allowEntitiesAndBlobs = false;
            bool isMergedFieldsStruct = false;
            ImmutableArray<AttributeData> interfaceAttributes = interfaceTypeSymbol.GetAttributes();
            for (int i = 0; i < interfaceAttributes.Length; i++)
            {
                AttributeData attribute = interfaceAttributes[i];

                // AllowEntitiesAndBlobsInPolymorphicStruct attribute
                if (attribute.AttributeClass.Name == TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute)
                {
                    allowEntitiesAndBlobs = true;
                }

                // IsMergedFieldsPolymorphicStruct attribute
                if (attribute.AttributeClass.Name == TypeName_IsMergedFieldsPolymorphicStructAttribute)
                {
                    isMergedFieldsStruct = true;
                }
            }

            // Build list of interface members, and members of parent interfaces if any
            InterfaceMembers.Clear();
            InterfaceMembers.AddRange(interfaceTypeSymbol.GetMembers());
            foreach (INamedTypeSymbol parentInterface in interfaceTypeSymbol.AllInterfaces)
            {
                InterfaceMembers.AddRange(parentInterface.GetMembers());
            }

            // Get public method and property infos
            List<MethodModel> interfaceMethodModels = new List<MethodModel>();
            List<PropertyModel> interfacePropertyModels = new List<PropertyModel>();
            foreach (ISymbol memberSymbol in InterfaceMembers)
            {
                if (memberSymbol.Kind == SymbolKind.Method && memberSymbol is IMethodSymbol methodSymbol)
                {
                    if (methodSymbol.CanBeReferencedByName &&
                        (methodSymbol.DeclaredAccessibility == Accessibility.Public || methodSymbol.DeclaredAccessibility == Accessibility.Internal))
                    {
                        MethodModel methodModel = new MethodModel();

                        methodModel.Name = methodSymbol.Name;
                        methodModel.HasNonVoidReturnType = methodSymbol.ReturnType.ToString() != TypeName_Void;
                        methodModel.ReturnTypeName = SourceGenUtils.GetFullTypeName(methodSymbol.ReturnType);
                        methodModel.Accessibility = methodSymbol.DeclaredAccessibility;

                        // Generics
                        ImmutableArray<ITypeParameterSymbol> genericTypeParameters = methodSymbol.TypeParameters;
                        if (methodSymbol.IsGenericMethod && genericTypeParameters.Length > 0)
                        {
                            methodModel.MethodGenericTypesDeclaration = "";
                            methodModel.MethodGenericTypesConstraint = "";

                            string genericTypeName = "";
                            int genericTypesCounter = 0;

                            foreach (ITypeParameterSymbol genericTypeParam in genericTypeParameters)
                            {
                                int genericTypeConstraintsCounterForType = 0;
                                genericTypeName = genericTypeParam.Name;

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
                                            methodModel.MethodGenericTypesConstraint += $" where {genericTypeName} : ";
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
                                            methodModel.MethodGenericTypesConstraint += $" where {genericTypeName} : ";
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
                                            methodModel.MethodGenericTypesConstraint += $" where {genericTypeName} : ";
                                        }
                                        else
                                        {
                                            methodModel.MethodGenericTypesConstraint += $", ";
                                        }
                                        methodModel.MethodGenericTypesConstraint += $"{SourceGenUtils.GetFullTypeName(constraintType)}";
                                        genericTypeConstraintsCounterForType++;
                                    }
                                }

                                genericTypesCounter++;
                            }

                            methodModel.MethodGenericTypesDeclaration += $">";
                        }
                        else
                        {
                            methodModel.MethodGenericTypesDeclaration = "";
                            methodModel.MethodGenericTypesConstraint = "";
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

                                methodModel.MethodParametersDefinition += $"{SourceGenUtils.GetFullTypeName(parameterSymbol.Type)} ";

                                methodModel.MethodParametersDefinition += $"{parameterSymbol.Name}";
                                methodModel.MethodParametersInvoke += $"{parameterSymbol.Name}";

                                // Remember out params separated by dots, so we can set them to default in method invokes
                                if (refKindString == "out")
                                {
                                    if (string.IsNullOrEmpty(methodModel.MethodOutParameterNames))
                                    {
                                        methodModel.MethodOutParameterNames = parameterSymbol.Name;
                                    }
                                    else
                                    {
                                        methodModel.MethodOutParameterNames += $".{parameterSymbol.Name}";
                                    }
                                }

                                parametersCounter++;
                            }
                        }

                        methodModel.RecomputeValueHash();
                        interfaceMethodModels.Add(methodModel);
                    }
                }

                if (memberSymbol.Kind == SymbolKind.Property && memberSymbol is IPropertySymbol propertySymbol)
                {
                    if ((propertySymbol.DeclaredAccessibility == Accessibility.Public || propertySymbol.DeclaredAccessibility == Accessibility.Internal))
                    {
                        PropertyModel propertyModel = new PropertyModel();

                        propertyModel.Name = propertySymbol.Name;
                        propertyModel.TypeName = SourceGenUtils.GetFullTypeName(propertySymbol.Type);
                        propertyModel.HasGet = propertySymbol.GetMethod != null;
                        propertyModel.HasSet = propertySymbol.SetMethod != null;
                        propertyModel.Accessibility = propertySymbol.DeclaredAccessibility;

                        propertyModel.RecomputeValueHash();
                        interfacePropertyModels.Add(propertyModel);
                    }
                }
            }

            return new PolyInterfaceModel(
                interfaceTypeSymbol.Name,
                SourceGenUtils.GetFullTypeName(interfaceTypeSymbol),
                SourceGenUtils.GetFullTypeName(interfaceTypeSymbol),
                targetStructModel,
                allowEntitiesAndBlobs,
                isMergedFieldsStruct,
                ((INamedTypeSymbol)interfaceTypeSymbol).IsGenericType,
                interfaceMethodModels,
                interfacePropertyModels);
        }

        private static bool PolyStructValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is StructDeclarationSyntax;
        }

        private static PolyStructModel PolyStructValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ITypeSymbol structTypeSymbol = (ITypeSymbol)generatorAttributeSyntaxContext.TargetSymbol;

            List<string> interfaceTypeNames = new List<string>();
            foreach (INamedTypeSymbol structInterface in structTypeSymbol.Interfaces)
            {
                interfaceTypeNames.Add(structInterface.ToDisplayString());
            }

            StructModel structModel = new StructModel(structTypeSymbol.Name, SourceGenUtils.GetFullTypeName(structTypeSymbol), SourceGenUtils.GetFullNamespaceTypeName(structTypeSymbol));
            return new PolyStructModel(structModel, structTypeSymbol, interfaceTypeNames);
        }

        private static CompiledStructsForInterfaceData CreateCompiledStructsForInterfaceData(PolyInterfaceModel polyInterfaceModel, ImmutableArray<PolyStructModel> polyStructModels)
        {
            CompiledStructsForInterfaceData compiledStructsForInterfaceData = new CompiledStructsForInterfaceData();
            compiledStructsForInterfaceData.PolyInterfaceModel = polyInterfaceModel;
            compiledStructsForInterfaceData.PolyStructModels = new List<PolyStructModel>();

            // Add poly structs implementing this poly interface to a list
            ImmutableArray<PolyStructModel>.Enumerator polyStructModelsEnumerator = polyStructModels.GetEnumerator();
            while (polyStructModelsEnumerator.MoveNext())
            {
                PolyStructModel polyStructModel = polyStructModelsEnumerator.Current;
                List<string> structInterfacesTypeNames = polyStructModel.InterfaceTypeNames;
                for (int i = 0; i < structInterfacesTypeNames.Count; i++)
                {
                    if (structInterfacesTypeNames[i] == compiledStructsForInterfaceData.PolyInterfaceModel.TypeName)
                    {
                        compiledStructsForInterfaceData.PolyStructModels.Add(polyStructModel);
                        break;
                    }
                }
            }

            return compiledStructsForInterfaceData;
        }

        private static void PolymorphicStructSourceOutputter(SourceProductionContext sourceProductionContext, (ImmutableArray<PolyInterfaceModel> Left, ImmutableArray<PolyStructModel> Right) source)
        {
            sourceProductionContext.CancellationToken.ThrowIfCancellationRequested();

            if (EnableDebug)
            {
                OutputDebugFile(sourceProductionContext, "Debug_PolymorphicStructInterfaces", new List<string>
                {
                    $"Found polyinterface structs count: {source.Left.Length} and polystructs count {source.Right.Length}",
                });
            }


            if (source.Left.Length > 0)
            {
                for (int a = 0; a < source.Left.Length; a++)
                {
                    LogMessages.Clear();
                    MergedFieldsData.Clear();

                    CompiledStructsForInterfaceData compiledCodeData = CreateCompiledStructsForInterfaceData(source.Left[a], source.Right);

                    // Prevent generics
                    if (compiledCodeData.PolyInterfaceModel.IsGeneric)
                    {
                        LogMessages.Add(new LogMessage
                        {
                            Type = LogMessage.MsgType.Error,
                            Message = $"PolymorphicStructs error: generic polymorphic interfaces are not supported. (Interface {compiledCodeData.PolyInterfaceModel.TypeName})",
                        });
                    }
                    FilterOutInvalidStructs(compiledCodeData, LogMessages);

                    PolyInterfaceModel polyInterfaceModel = compiledCodeData.PolyInterfaceModel;

                    FileWriter writer = new FileWriter();

                    // Usings
                    writer.WriteUsingsAndRemoveDuplicates(GetCommonUsings());

                    writer.WriteLine($"");

                    writer.WriteInNamespace(polyInterfaceModel.TargetStructModel.NamespaceName, () =>
                    {
                        string structImplementsString = string.Empty;
                        if (polyInterfaceModel.IsMergedFieldsStruct)
                        {
                            // Compile merged fields data for each struct
                            for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                            {
                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];

                                MergedFieldsData.StructFieldTypeCounter.Clear();

                                // Iterate struct fields & properties
                                foreach (ISymbol memberSymbol in polyStructModel.StructTypeSymbol.GetMembers())
                                {
                                    if (memberSymbol.Kind == SymbolKind.Field && 
                                        memberSymbol is IFieldSymbol fieldSymbol &&
                                        !fieldSymbol.IsImplicitlyDeclared)
                                    {
                                        SpecificFieldModel specificFieldModel = new SpecificFieldModel
                                        {
                                            FieldName = fieldSymbol.Name,
                                            FieldTypeName = SourceGenUtils.GetFullTypeName(fieldSymbol.Type),
                                            StructName = polyStructModel.StructModel.Name,
                                        };

                                        // Update field type counter in this struct
                                        int fieldTypeCounter = 1;
                                        if (MergedFieldsData.StructFieldTypeCounter.TryGetValue(specificFieldModel.FieldTypeName, out fieldTypeCounter))
                                        {
                                            fieldTypeCounter++;
                                            MergedFieldsData.StructFieldTypeCounter[specificFieldModel.FieldTypeName] = fieldTypeCounter;
                                        }
                                        else
                                        {
                                            MergedFieldsData.StructFieldTypeCounter.Add(specificFieldModel.FieldTypeName, fieldTypeCounter);
                                        }

                                        // Check if a merged field of this type exists already
                                        bool foundMatchingMergedField = false;
                                        MergedFieldModel matchingMergedFieldModel = default;
                                        foreach (KeyValuePair<MergedFieldModel, Dictionary<string, SpecificFieldModel>> entry in MergedFieldsData.MergedFieldToSpecificFieldsMap)
                                        {
                                            MergedFieldModel mergedFieldModel = entry.Key;

                                            // If found a match
                                            if (mergedFieldModel.TypeCounter == fieldTypeCounter &&
                                                mergedFieldModel.FieldTypeName == specificFieldModel.FieldTypeName)
                                            {
                                                foundMatchingMergedField = true;
                                                matchingMergedFieldModel = mergedFieldModel;
                                                break;
                                            }
                                        }

                                        // If we did not find a matching merged field, create a new one
                                        if (!foundMatchingMergedField)
                                        {
                                            matchingMergedFieldModel = new MergedFieldModel
                                            {
                                                FieldTypeName = specificFieldModel.FieldTypeName,
                                                TypeCounter = fieldTypeCounter,
                                            };
                                            MergedFieldsData.MergedFieldToSpecificFieldsMap.Add(matchingMergedFieldModel, new Dictionary<string, SpecificFieldModel>());
                                        }

                                        // Add specific field to matching merged field maps
                                        {
                                            Dictionary<string, SpecificFieldModel> structNameToFieldModelMap = MergedFieldsData.MergedFieldToSpecificFieldsMap[matchingMergedFieldModel];

                                            // Add map entries for this struct + field
                                            structNameToFieldModelMap.Add(specificFieldModel.StructName, specificFieldModel);
                                            MergedFieldsData.MergedFieldToSpecificFieldsMap[matchingMergedFieldModel] = structNameToFieldModelMap;
                                            MergedFieldsData.SpecificFieldToMergedFieldMap.Add(specificFieldModel, matchingMergedFieldModel);
                                        }
                                    }

                                    if (memberSymbol.Kind == SymbolKind.Property && memberSymbol is IPropertySymbol propertySymbol)
                                    {
                                        // Only allow properties that are part of the poly interface
                                        bool foundMatchingPropertyInInterface = false;
                                        for (int p = 0; p < polyInterfaceModel.InterfacePropertyModels.Count; p++)
                                        {
                                            PropertyModel propertyModel = polyInterfaceModel.InterfacePropertyModels[p];
                                            if (propertySymbol.Name == propertyModel.Name)
                                            {
                                                foundMatchingPropertyInInterface = true;
                                                break;
                                            }
                                        }

                                        if(!foundMatchingPropertyInInterface)
                                        {
                                            LogMessages.Add(new LogMessage
                                            {
                                                Type = LogMessage.MsgType.Error,
                                                Message = $"PolymorphicStructs error: for MergedFields polymorphic structs, properties are only supported if they are part of the polymorphic interface. Property {polyStructModel.StructModel.TypeName}.{propertySymbol.Name} will not work as expected.",
                                            });
                                        }
                                    }
                                }
                            }

                            // Create merged fields Ids after all merged fields added
                            int mergedFieldIdCounter = 0;
                            foreach (KeyValuePair<MergedFieldModel, Dictionary<string, SpecificFieldModel>> entry in MergedFieldsData.MergedFieldToSpecificFieldsMap)
                            {
                                MergedFieldsData.MergedFieldToMergedFieldIdMap.Add(entry.Key, mergedFieldIdCounter);
                                mergedFieldIdCounter++;
                            }
                        }
                        else
                        {
                            structImplementsString = $" : {TypeName_IPolymorphicObject}";
                            writer.WriteLine($"{Decorator_StructLayout_Explicit}");
                        }
                        writer.WriteLine($"public unsafe partial struct {polyInterfaceModel.TargetStructModel.Name}{structImplementsString}");
                        writer.WriteInScope(() =>
                        {
                            // Types enum
                            writer.WriteLine($"public enum {Name_Enum_TypeId}");
                            writer.WriteInScope(() =>
                            {
                                for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                                {
                                    PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];
                                    writer.WriteLine($"{polyStructModel.StructModel.Name},");
                                }
                            });

                            writer.WriteLine($"");

                            if (!polyInterfaceModel.IsMergedFieldsStruct)
                            {
                                writer.WriteLine($"[{TypeName_FieldOffset}(0)]");
                            }
                            writer.WriteLine($"public {Name_Enum_TypeId} CurrentTypeId;");

                            // Fields
                            if (polyInterfaceModel.IsMergedFieldsStruct)
                            {
                                // Merged fields
                                foreach (KeyValuePair<MergedFieldModel, Dictionary<string, SpecificFieldModel>> entry in MergedFieldsData.MergedFieldToSpecificFieldsMap)
                                {
                                    MergedFieldModel mergedFieldModel = entry.Key;
                                    writer.WriteLine($"public {mergedFieldModel.FieldTypeName} {MergedFieldsData.GetMergedFieldName(mergedFieldModel)};");
                                }
                            }
                            else
                            {
                                // Union fields
                                for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                                {
                                    PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];
                                    writer.WriteLine($"[{TypeName_FieldOffset}({SizeOf_TypeId})]");
                                    writer.WriteLine($"public {polyStructModel.StructModel.TypeName} Field_{polyStructModel.StructModel.Name};");
                                }
                            }

                            writer.WriteLine($"");

                            // Properties
                            for (int i = 0; i < polyInterfaceModel.InterfacePropertyModels.Count; i++)
                            {
                                PropertyModel propertyModel = polyInterfaceModel.InterfacePropertyModels[i];

                                writer.WriteLine($"public {propertyModel.TypeName} {propertyModel.Name}");
                                writer.WriteInScope(() =>
                                {
                                    if (propertyModel.HasGet)
                                    {
                                        writer.WriteLine($"get");
                                        writer.WriteInScope(() =>
                                        {
                                            // Switch over typeId
                                            writer.WriteLine($"switch (CurrentTypeId)");
                                            writer.WriteInScope(() =>
                                            {
                                                for (int t = 0; t < compiledCodeData.PolyStructModels.Count; t++)
                                                {
                                                    PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[t];

                                                    writer.WriteLine($"case {Name_Enum_TypeId}.{polyStructModel.StructModel.Name}:");
                                                    writer.WriteInScope(() =>
                                                    {
                                                        if (polyInterfaceModel.IsMergedFieldsStruct)
                                                        {
                                                            // Merged fields
                                                            // cast merged struct to specific
                                                            writer.WriteLine($"{polyStructModel.StructModel.TypeName} specificStruct = this;");
                                                            // invoke property on specific
                                                            writer.WriteLine($"{propertyModel.TypeName} result = specificStruct.{propertyModel.Name};");
                                                            // cast back to merged
                                                            writer.WriteLine($"this = specificStruct;");
                                                            writer.WriteLine($"return result;");
                                                        }
                                                        else
                                                        {
                                                            // Union struct
                                                            writer.WriteLine($"return Field_{polyStructModel.StructModel.Name}.{propertyModel.Name};");
                                                        }
                                                    });
                                                }
                                            });

                                            writer.WriteLine($"return default;");
                                        });
                                    }
                                    if (propertyModel.HasSet)
                                    {
                                        writer.WriteLine($"set");
                                        writer.WriteInScope(() =>
                                        {
                                            // Switch over typeId
                                            writer.WriteLine($"switch (CurrentTypeId)");
                                            writer.WriteInScope(() =>
                                            {
                                                for (int t = 0; t < compiledCodeData.PolyStructModels.Count; t++)
                                                {
                                                    PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[t];

                                                    writer.WriteLine($"case {Name_Enum_TypeId}.{polyStructModel.StructModel.Name}:");
                                                    writer.WriteInScope(() =>
                                                    {
                                                        if (polyInterfaceModel.IsMergedFieldsStruct)
                                                        {
                                                            // Merged fields
                                                            // cast merged struct to specific
                                                            writer.WriteLine($"{polyStructModel.StructModel.TypeName} specificStruct = this;");
                                                            // invoke property on specific
                                                            writer.WriteLine($"specificStruct.{propertyModel.Name} = value;");
                                                            // cast back to merged
                                                            writer.WriteLine($"this = specificStruct;");
                                                        }
                                                        else
                                                        {
                                                            // Union struct
                                                            writer.WriteLine($"Field_{polyStructModel.StructModel.Name}.{propertyModel.Name} = value;");
                                                        }
                                                        writer.WriteLine($"break;");
                                                    });
                                                }
                                            });
                                        });
                                    }
                                });

                                writer.WriteLine($"");
                            }

                            writer.WriteLine($"");

                            // Implicit casts
                            for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                            {
                                PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];

                                // Cast struct to poly struct
                                writer.WriteLine($"public static implicit operator {polyInterfaceModel.TargetStructModel.TypeName} ({polyStructModel.StructModel.TypeName} s)");
                                writer.WriteInScope(() =>
                                {
                                    if (polyInterfaceModel.IsMergedFieldsStruct)
                                    {
                                        // Merged fields
                                        writer.WriteLine($"{polyInterfaceModel.TargetStructModel.TypeName} newPolyStruct = default;");

                                        // set properties that can store a value
                                        for (int p = 0; p < polyInterfaceModel.InterfacePropertyModels.Count; p++)
                                        {
                                            PropertyModel propertyModel = polyInterfaceModel.InterfacePropertyModels[p];
                                            if (propertyModel.HasSet && propertyModel.HasGet)
                                            {
                                                writer.WriteLine($"newPolyStruct.{propertyModel.Name} = s.{propertyModel.Name};");
                                            }
                                        }

                                        // set fields
                                        writer.WriteLine($"newPolyStruct.CurrentTypeId = {Name_Enum_TypeId}.{polyStructModel.StructModel.Name};");
                                        foreach (KeyValuePair<SpecificFieldModel, MergedFieldModel> entry in MergedFieldsData.SpecificFieldToMergedFieldMap)
                                        {
                                            SpecificFieldModel specificFieldModel = entry.Key;

                                            // If field of this struct
                                            if (entry.Key.StructName == polyStructModel.StructModel.Name)
                                            {
                                                MergedFieldModel mergedFieldModel = entry.Value;
                                                writer.WriteLine($"newPolyStruct.{MergedFieldsData.GetMergedFieldName(mergedFieldModel)} = s.{specificFieldModel.FieldName};");
                                            }
                                        }
                                        writer.WriteLine($"return newPolyStruct;");
                                    }
                                    else
                                    {
                                        writer.WriteLine($"return new {polyInterfaceModel.TargetStructModel.TypeName}");
                                        writer.WriteInScope(() =>
                                        {
                                            // Union struct
                                            writer.WriteLine($"CurrentTypeId = {Name_Enum_TypeId}.{polyStructModel.StructModel.Name},");
                                            writer.WriteLine($"Field_{polyStructModel.StructModel.Name} = s,");
                                        }, ";");
                                    }
                                });

                                writer.WriteLine($"");

                                // Cast poly struct to struct
                                writer.WriteLine($"public static implicit operator {polyStructModel.StructModel.TypeName} ({polyInterfaceModel.TargetStructModel.TypeName} s)");
                                writer.WriteInScope(() =>
                                {
                                    if (polyInterfaceModel.IsMergedFieldsStruct)
                                    {
                                        // Merged fields
                                        writer.WriteLine($"{polyStructModel.StructModel.TypeName} newStruct = default;");

                                        // set properties that can store a value
                                        for (int p = 0; p < polyInterfaceModel.InterfacePropertyModels.Count; p++)
                                        {
                                            PropertyModel propertyModel = polyInterfaceModel.InterfacePropertyModels[p];
                                            if (propertyModel.HasSet && propertyModel.HasGet)
                                            {
                                                writer.WriteLine($"newStruct.{propertyModel.Name} = s.{propertyModel.Name};");
                                            }
                                        }

                                        // set fields
                                        foreach (KeyValuePair<MergedFieldModel, Dictionary<string, SpecificFieldModel>> entry in MergedFieldsData.MergedFieldToSpecificFieldsMap)
                                        {
                                            MergedFieldModel mergedFieldModel = entry.Key;
                                            Dictionary<string, SpecificFieldModel> specificFieldMap = entry.Value;

                                            if(specificFieldMap.TryGetValue(polyStructModel.StructModel.Name, out SpecificFieldModel specificFieldModel))
                                            {
                                                writer.WriteLine($"newStruct.{specificFieldModel.FieldName} = s.{MergedFieldsData.GetMergedFieldName(mergedFieldModel)};");
                                            }
                                        }

                                        writer.WriteLine($"return newStruct;");
                                    }
                                    else
                                    {
                                        // Union struct
                                        writer.WriteLine($"return s.Field_{polyStructModel.StructModel.Name};");
                                    }
                                });

                                writer.WriteLine($"");
                            }

                            writer.WriteLine($"");

                            // IPolymorphicObject interface
                            if (!polyInterfaceModel.IsMergedFieldsStruct)
                            {
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public int GetTypeId()");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"return (int)CurrentTypeId;");
                                });

                                writer.WriteLine($"");

                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public int GetDataBytesSize()");
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
                                                writer.WriteLine($"return {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.TypeName}>();");
                                            });
                                        }
                                    });
                                    writer.WriteLine($"return 0;");
                                });

                                writer.WriteLine($"");

                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public int GetDataBytesSizeFor(int typeId)");
                                writer.WriteInScope(() =>
                                {
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
                                                writer.WriteLine($"return {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.TypeName}>();");
                                            });
                                        }
                                    });
                                    writer.WriteLine($"return 0;");
                                });

                                writer.WriteLine($"");

                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public void WriteDataTo(byte* dstPtr, out int writeSize)");
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
                                                writer.WriteLine($"writeSize = {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.TypeName}>();");
                                                writer.WriteLine($"*({polyStructModel.StructModel.TypeName}*)dstPtr = Field_{polyStructModel.StructModel.Name};");
                                                writer.WriteLine($"return;");
                                            });
                                        }
                                    });
                                    writer.WriteLine($"");
                                    writer.WriteLine($"writeSize = 0;");
                                });


                                writer.WriteLine($"");

                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public void SetDataFrom(int typeId, byte* srcPtr, out int readSize)");
                                writer.WriteInScope(() =>
                                {
                                    writer.WriteLine($"CurrentTypeId = ({Name_Enum_TypeId})typeId;");
                                    writer.WriteLine($"");

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
                                                writer.WriteLine($"readSize = {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.TypeName}>();");
                                                writer.WriteLine($" Field_{polyStructModel.StructModel.Name} = *({polyStructModel.StructModel.TypeName}*)srcPtr;");
                                                writer.WriteLine($"return;");
                                            });
                                        }
                                    });
                                    writer.WriteLine($"");
                                    writer.WriteLine($"readSize = 0;");
                                });
                            }

                            writer.WriteLine($"");

                            // Poly Methods
                            for (int i = 0; i < polyInterfaceModel.InterfaceMethodModels.Count; i++)
                            {
                                MethodModel methodModel = polyInterfaceModel.InterfaceMethodModels[i];

                                string curatedReturnType = methodModel.HasNonVoidReturnType ? methodModel.ReturnTypeName : "void";

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
                                                if (polyInterfaceModel.IsMergedFieldsStruct)
                                                {
                                                    // Merged fields
                                                    if (methodModel.HasNonVoidReturnType)
                                                    {
                                                        // cast merged struct to specific
                                                        writer.WriteLine($"{polyStructModel.StructModel.TypeName} specificStruct = this;");
                                                        // invoke method on specific
                                                        writer.WriteLine($"{methodModel.ReturnTypeName} result = specificStruct.{methodModel.Name}({methodModel.MethodParametersInvoke});");
                                                        // cast back to merged
                                                        writer.WriteLine($"this = specificStruct;");
                                                        writer.WriteLine($"return result;");
                                                    }
                                                    else
                                                    {
                                                        // cast merged struct to specific
                                                        writer.WriteLine($"{polyStructModel.StructModel.TypeName} specificStruct = this;");
                                                        // invoke method on specific
                                                        writer.WriteLine($"specificStruct.{methodModel.Name}({methodModel.MethodParametersInvoke});");
                                                        // cast back to merged
                                                        writer.WriteLine($"this = specificStruct;");
                                                        writer.WriteLine($"return;");
                                                    }
                                                }
                                                else
                                                {
                                                    // Union struct
                                                    if (methodModel.HasNonVoidReturnType)
                                                    {
                                                        writer.WriteLine($"return Field_{polyStructModel.StructModel.Name}.{methodModel.Name}({methodModel.MethodParametersInvoke});");
                                                    }
                                                    else
                                                    {
                                                        writer.WriteLine($"Field_{polyStructModel.StructModel.Name}.{methodModel.Name}({methodModel.MethodParametersInvoke});");
                                                        writer.WriteLine($"return;");
                                                    }
                                                }
                                            });
                                        }
                                    });

                                    // Out params default
                                    if (!string.IsNullOrEmpty(methodModel.MethodOutParameterNames))
                                    {
                                        string[] outParamNames = methodModel.MethodOutParameterNames.Split('.');
                                        for (int o = 0; o < methodModel.MethodOutParameterNames.Length; o++)
                                        {
                                            writer.WriteLine($"{methodModel.MethodOutParameterNames[o]} = default;");
                                        }
                                    }

                                    if (methodModel.HasNonVoidReturnType)
                                    {
                                        writer.WriteLine($"return default;");
                                    }
                                });

                                writer.WriteLine($"");
                            }
                            WriteLogMessagesOutputter(writer, LogMessages);
                        });
                    });
                    writer.WriteLine($"");

                    SourceText sourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
                    sourceProductionContext.AddSource($"{polyInterfaceModel.TargetStructModel.Name}{FileName_GeneratedSuffixAndFileType}", sourceText);
                }
            }
        }

        private static void FilterOutInvalidStructs(CompiledStructsForInterfaceData compiledData, List<LogMessage> logMessages)
        {
            for (int i = compiledData.PolyStructModels.Count - 1; i >= 0; i--)
            {
                bool structIsValid = true;
                PolyStructModel structModel = compiledData.PolyStructModels[i];

                if (!compiledData.PolyInterfaceModel.AllowEntitiesAndBlobs)
                {
                    SymbolsToProcess.Clear();
                    SymbolsToProcess.Add(structModel.StructTypeSymbol);
                    ProcessPreventingStructInvalidFieldOrPropertyTypes(SymbolsToProcess, logMessages, ref structIsValid);
                }

                // Prevent generics
                if(((INamedTypeSymbol)structModel.StructTypeSymbol).IsGenericType)
                {
                    logMessages.Add(new LogMessage
                    {
                        Type = LogMessage.MsgType.Error,
                        Message = $"PolymorphicStructs error: generic polymorphic structs are not supported. (Struct {structModel.StructModel.TypeName})",
                    });
                }

                if (!structIsValid)
                {
                    compiledData.PolyStructModels.RemoveAt(i);
                }
            }
        }

        private static void ProcessPreventingStructInvalidFieldOrPropertyTypes(
            List<ITypeSymbol> symbolsToProcess,
            List<LogMessage> logMessages, 
            ref bool structIsValid)
        {
            SymbolEqualityComparer symbolEqualityComparer = SymbolEqualityComparer.Default;
            for (int i = 0; i < symbolsToProcess.Count; i++)
            {
                ITypeSymbol processedTypeSymbol = symbolsToProcess[i];

                foreach (ISymbol memberSymbol in processedTypeSymbol.GetMembers())
                {
                    if (memberSymbol.Kind == SymbolKind.Field && memberSymbol is IFieldSymbol fieldSymbol)
                    {
                        ProcessPreventingStructInvalidFieldOrPropertyTypes(false, fieldSymbol.Type, logMessages, ref structIsValid);
                        if(structIsValid && !symbolEqualityComparer.Equals(fieldSymbol.Type, processedTypeSymbol))
                        {
                            symbolsToProcess.Add(fieldSymbol.Type);
                        }
                    }
                    if (memberSymbol.Kind == SymbolKind.Property && memberSymbol is IPropertySymbol propertySymbol)
                    {
                        ProcessPreventingStructInvalidFieldOrPropertyTypes(true, propertySymbol.Type, logMessages, ref structIsValid);
                        if (structIsValid && !symbolEqualityComparer.Equals(propertySymbol.Type, processedTypeSymbol))
                        {
                            symbolsToProcess.Add(propertySymbol.Type);
                        }
                    }
                }
            }
        }

        private static void ProcessPreventingStructInvalidFieldOrPropertyTypes(
            bool isProperty,
            ITypeSymbol typeSymbol,
            List<LogMessage> messages,
            ref bool isStructValid)
        {
            string fullTypeName = SourceGenUtils.GetFullTypeName(typeSymbol);

            if (fullTypeName == FullTypeName_Entity)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, FullTypeName_Entity));
            }

            if (fullTypeName.Length >= FullTypeName_BlobString.Length && fullTypeName.Substring(0, FullTypeName_BlobString.Length) == FullTypeName_BlobString)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, FullTypeName_BlobString));
            }

            if (fullTypeName.Length >= FullTypeName_BlobAssetReference.Length && fullTypeName.Substring(0, FullTypeName_BlobAssetReference.Length) == FullTypeName_BlobAssetReference)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, FullTypeName_BlobAssetReference));
            }

            if (fullTypeName.Length >= FullTypeName_BlobArray.Length && fullTypeName.Substring(0, FullTypeName_BlobArray.Length) == FullTypeName_BlobArray)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, FullTypeName_BlobArray));
            }

            if (fullTypeName.Length >= FullTypeName_BlobPtr.Length && fullTypeName.Substring(0, FullTypeName_BlobPtr.Length) == FullTypeName_BlobPtr)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, FullTypeName_BlobPtr));
            }
        }
        
        private static LogMessage GetEntityAndBlobsError(bool isProperty, string typeName)
        {
            return new LogMessage
            {
                Type = LogMessage.MsgType.Error,
                Message = $"PolymorphicStructs error: {typeName} {(isProperty ? "properties" : "fields")} are not allowed in polymorphic structs by default. Use the [{TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute}] on the polymorphic interface if you wish to disable this restriction and know what you're doing. You may also consult the documentation for suggested workarounds, such as storing these fields in a struct that encompasses the generated polymorphic struct.",
            };
        }

        private static void WriteLogMessagesOutputter(FileWriter writer, List<LogMessage> messages)
        {
            if (messages.Count > 0)
            {
                writer.WriteLine($"{Decorator_InitializeOnLoadMethod}");
                writer.WriteLine($"public static void SourceGenLogMessages()");
                writer.WriteInScope(() =>
                {
                    for (int i = 0; i < messages.Count; i++)
                    {
                        LogMessage msg = messages[i];
                        switch (msg.Type)
                        {
                            case LogMessage.MsgType.Log:
                                writer.WriteLine($"UnityEngine.Debug.Log($\"{msg.Message}\");");
                                break;
                            case LogMessage.MsgType.Error:
                                writer.WriteLine($"UnityEngine.Debug.LogError($\"{msg.Message}\");");
                                break;
                        }
                    }
                });
            }
        }

        private static void OutputDebugFile(SourceProductionContext context, string name, List<string> messages)
        {
            FileWriter writer = new FileWriter();
            writer.WriteLine($"public struct {name}");
            writer.WriteInScope(() =>
            {
                writer.WriteLine($"{Decorator_InitializeOnLoadMethod}");
                writer.WriteLine($"public static void SourceGenLogMessages()");
                writer.WriteInScope(() =>
                {
                    for (int i = 0; i < messages.Count; i++)
                    {
                        writer.WriteLine($"UnityEngine.Debug.Log($\"{messages[i]}\");");
                    }
                });
            });
            SourceText debugSourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
            context.AddSource($"{name}.cs", debugSourceText);
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