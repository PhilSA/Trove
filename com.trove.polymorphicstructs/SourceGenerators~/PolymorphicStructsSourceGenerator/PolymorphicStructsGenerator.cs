
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
    public enum PolyInterfaceType
    {
        UnionStruct,
        ByteArray,
    }

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

        public string MetaDataName;
        public StructModel TargetStructModel;
        public bool AllowEntitiesAndBlobs;
        public List<MethodModel> InterfaceMethodModels;
        public List<PropertyModel> InterfacePropertyModels;

        public PolyInterfaceModel(
            string metaDataName,
            StructModel targetStructModel,
            bool allowEntitiesAndBlobs,
            List<MethodModel> interfaceMethodModels,
            List<PropertyModel> interfacePropertyModels)
        {
            MetaDataName = metaDataName;
            TargetStructModel = targetStructModel;
            AllowEntitiesAndBlobs = allowEntitiesAndBlobs;
            InterfaceMethodModels = interfaceMethodModels;
            InterfacePropertyModels = interfacePropertyModels;

            ValueHash = 0;
            RecomputeValueHash();
        }

        public void RecomputeValueHash()
        {
            string valuesString = $"{MetaDataName}{TargetStructModel.ValueHash}{AllowEntitiesAndBlobs}{InterfaceMethodModels.Count}{InterfacePropertyModels.Count}";
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
        public ITypeSymbol StructTypeSymbol;

        public PolyStructModel(StructModel structModel, ITypeSymbol structTypeSymbol, List<string> interfaceMetaDataNames)
        {
            StructModel = structModel;
            InterfaceMetaDataNames = interfaceMetaDataNames;
            StructTypeSymbol = structTypeSymbol;

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
        public Accessibility Accessibility;

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

    public struct PropertyModel : IEquatable<MethodModel>
    {
        public int ValueHash;

        public string Name;
        public string TypeMetaDataName;
        public bool HasGet;
        public bool HasSet;
        public Accessibility Accessibility;

        public void RecomputeValueHash()
        {
            string valuesString = $"{Name}{TypeMetaDataName}{HasGet}{HasSet}";
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
        public const bool EnableDebug = false;

        public const string NamespaceName_Trove = "Trove";
        public const string NamespaceName_PolymorphicStructs = NamespaceName_Trove + ".PolymorphicStructs";

        public const string TypeName_Void = "void";
        public const string TypeName_IPolymorphicObject = "IPolymorphicObject";
        public const string TypeName_PolymorphicStructInterfaceAttribute = "PolymorphicStructInterfaceAttribute";
        public const string TypeName_PolymorphicStructAttribute = "PolymorphicStructAttribute";
        public const string TypeName_WriteBackStructDataAttribute = "WriteBackStructDataAttribute";
        public const string TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute = "AllowEntitiesAndBlobsInPolymorphicStructAttribute";
        public const string TypeName_UnsafeUtility = "UnsafeUtility";
        public const string TypeName_FieldOffset = "FieldOffset";

        public const string MetaDataName_PolymorphicStructInterfaceAttribute = NamespaceName_PolymorphicStructs + "." + TypeName_PolymorphicStructInterfaceAttribute;
        public const string MetaDataName_PolymorphicStructAttribute = NamespaceName_PolymorphicStructs + "." + TypeName_PolymorphicStructAttribute;
        public const string MetaDataName_Entity = "Unity.Entities.Entity";
        public const string MetaDataName_BlobAssetReference = "Unity.Entities.BlobAssetReference";
        public const string MetaDataName_BlobString = "Unity.Entities.BlobString";
        public const string MetaDataName_BlobArray = "Unity.Entities.BlobArray";
        public const string MetaDataName_BlobPtr = "Unity.Entities.BlobPtr";

        public const string FileName_GeneratedSuffixAndFileType = ".generated.cs";
        public const string FileName_PolymorphiUnionStructAttribute = TypeName_PolymorphicStructInterfaceAttribute + FileName_GeneratedSuffixAndFileType;
        public const string FileName_PolymorphicStructAttribute = TypeName_PolymorphicStructAttribute + FileName_GeneratedSuffixAndFileType;
        public const string FileName_WriteBackStructDataAttribute = TypeName_WriteBackStructDataAttribute + FileName_GeneratedSuffixAndFileType;
        public const string FileName_AllowEntitiesAndBlobsInPolymorphicStructAttribute = TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute + FileName_GeneratedSuffixAndFileType;

        public const string Decorator_InitializeOnLoadMethod = "[UnityEditor.InitializeOnLoadMethod]";
        public const string Decorator_MethodImpl_AggressiveInlining = "[MethodImpl(MethodImplOptions.AggressiveInlining)]";
        public const string Decorator_StructLayout_Explicit = "[StructLayout(LayoutKind.Explicit)]";
        
        public const string Name_Enum_TypeId = "TypeId";

        public const string SizeOf_TypeId = "4";

        public static List<LogMessage> LogMessages = new List<LogMessage>();
        public static List<ITypeSymbol> SymbolsToProcess = new List<ITypeSymbol>();

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Generate attributes used for marking
            GenerateAttributes(context);

            // Create the values provider for poly interfaces and structs
            IncrementalValuesProvider<PolyInterfaceModel> polyUnionStructValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                MetaDataName_PolymorphicStructInterfaceAttribute,
                PolyUnionStructInterfaceValuesProviderPredicate,
                PolyUnionStructInterfaceValuesProviderTransform);
            IncrementalValuesProvider<PolyStructModel> polyStructValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                MetaDataName_PolymorphicStructAttribute,
                PolyStructValuesProviderPredicate,
                PolyStructValuesProviderTransform);

            // Collect poly structs into an array, and create combined value providers of (PolyInterface, PolyStructsArray)
            IncrementalValueProvider<ImmutableArray<PolyInterfaceModel>> unionStructInterfacesValueArrayProvider = polyUnionStructValuesProvider.Collect();
            IncrementalValueProvider<ImmutableArray<PolyStructModel>> polyStructsValueArrayProvider = polyStructValuesProvider.Collect();

            // Create the combimed interfaces + structs value providers
            IncrementalValueProvider<(ImmutableArray<PolyInterfaceModel> Left, ImmutableArray<PolyStructModel> Right)> polyUnionStructAndStructsValuesProvider = unionStructInterfacesValueArrayProvider.Combine(polyStructsValueArrayProvider);

            // For each element matching this pipeline, handle output
            context.RegisterSourceOutput(polyUnionStructAndStructsValuesProvider, UnionStructSourceOutputter);
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
                i.AddSource(FileName_PolymorphiUnionStructAttribute, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_PolymorphicStructs, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Struct)]");
                    writer.WriteLine($"internal class {TypeName_PolymorphicStructAttribute}: System.Attribute {{}}");
                });
                i.AddSource(FileName_PolymorphicStructAttribute, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_PolymorphicStructs, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Method)]");
                    writer.WriteLine($"internal class {TypeName_WriteBackStructDataAttribute}: System.Attribute {{}}");
                });
                i.AddSource(FileName_WriteBackStructDataAttribute, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_PolymorphicStructs, () =>
                {
                    writer.WriteLine($"[System.AttributeUsage(System.AttributeTargets.Interface)]");
                    writer.WriteLine($"internal class {TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute}: System.Attribute {{}}");
                });
                i.AddSource(FileName_AllowEntitiesAndBlobsInPolymorphicStructAttribute, writer.FileContents);
            });
        }

        private static bool PolyUnionStructInterfaceValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is InterfaceDeclarationSyntax;
        }

        private static PolyInterfaceModel PolyUnionStructInterfaceValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return CommonInterfaceValuesProviderTransform(generatorAttributeSyntaxContext, "PStruct");
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
            return new PolyStructModel(structModel, structTypeSymbol, interfaceMetaDataNames);
        }

        private static PolyInterfaceModel CommonInterfaceValuesProviderTransform(
            GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext,
            string targetStructNamePrefix)
        {
            ITypeSymbol interfaceTypeSymbol = (ITypeSymbol)generatorAttributeSyntaxContext.TargetSymbol;

            string interfaceNamespaceMetadataName = SourceGenUtils.GetNamespaceMetaDataName(interfaceTypeSymbol);

            bool allowEntitiesAndBlobs = false;
            ImmutableArray<AttributeData> interfaceAttributes = interfaceTypeSymbol.GetAttributes();
            for (int i = 0; i < interfaceAttributes.Length; i++)
            {
                AttributeData attribute = interfaceAttributes[i];
                if (attribute.AttributeClass.Name == TypeName_AllowEntitiesAndBlobsInPolymorphicStructAttribute)
                {
                    allowEntitiesAndBlobs = true;
                    break;
                }
            }

            // Target struct
            string targetStructName = $"{targetStructNamePrefix}_{interfaceTypeSymbol.Name}";
            StructModel targetStructModel = new StructModel(targetStructName, interfaceNamespaceMetadataName, $"{interfaceNamespaceMetadataName}.{targetStructName}");

            // Get public method and property infos
            List<MethodModel> interfaceMethodModels = new List<MethodModel>();
            List<PropertyModel> interfacePropertyModels = new List<PropertyModel>();
            foreach (ISymbol memberSymbol in interfaceTypeSymbol.GetMembers())
            {
                if (memberSymbol.Kind == SymbolKind.Method && memberSymbol is IMethodSymbol methodSymbol)
                {
                    if (methodSymbol.CanBeReferencedByName &&
                        (methodSymbol.DeclaredAccessibility == Accessibility.Public || methodSymbol.DeclaredAccessibility == Accessibility.Internal))
                    {
                        MethodModel methodModel = new MethodModel();

                        methodModel.Name = methodSymbol.Name;
                        methodModel.HasNonVoidReturnType = methodSymbol.ReturnType.ToString() != TypeName_Void;
                        methodModel.ReturnTypeMetaDataName = methodSymbol.ReturnType.MetadataName;
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
                                        methodModel.MethodGenericTypesConstraint += $"{constraintType}";
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

                if (memberSymbol.Kind == SymbolKind.Property && memberSymbol is IPropertySymbol propertySymbol)
                {
                    if ((propertySymbol.DeclaredAccessibility == Accessibility.Public || propertySymbol.DeclaredAccessibility == Accessibility.Internal))
                    {
                        PropertyModel propertyModel = new PropertyModel();

                        propertyModel.Name = propertySymbol.Name;
                        propertyModel.TypeMetaDataName = propertySymbol.Type.MetadataName;
                        propertyModel.HasGet = propertySymbol.GetMethod != null;
                        propertyModel.HasSet = propertySymbol.SetMethod != null;
                        propertyModel.Accessibility = propertySymbol.DeclaredAccessibility;

                        propertyModel.RecomputeValueHash();
                        interfacePropertyModels.Add(propertyModel);
                    }
                }
            }

            return new PolyInterfaceModel(
                interfaceTypeSymbol.MetadataName, 
                targetStructModel,
                allowEntitiesAndBlobs,
                interfaceMethodModels, 
                interfacePropertyModels);
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
                List<string> structInterfaces = polyStructModel.InterfaceMetaDataNames;
                for (int i = 0; i < structInterfaces.Count; i++)
                {
                    if (structInterfaces[i] == compiledStructsForInterfaceData.PolyInterfaceModel.MetaDataName)
                    {
                        compiledStructsForInterfaceData.PolyStructModels.Add(polyStructModel);
                        break;
                    }
                }
            }

            return compiledStructsForInterfaceData;
        }

        private static void UnionStructSourceOutputter(SourceProductionContext sourceProductionContext, (ImmutableArray<PolyInterfaceModel> Left, ImmutableArray<PolyStructModel> Right) source)
        {
            sourceProductionContext.CancellationToken.ThrowIfCancellationRequested();

            if (EnableDebug)
            {
                OutputDebugFile(sourceProductionContext, "Debug_PolymorphicStructsUnionStructs", new List<string>
                {
                    $"Found union structs count: {source.Left.Length} and polystructs count {source.Right.Length}",
                });
            }

            if (source.Left.Length > 0)
            {
                for (int a = 0; a < source.Left.Length; a++)
                {
                    LogMessages.Clear();

                    CompiledStructsForInterfaceData compiledCodeData = CreateCompiledStructsForInterfaceData(source.Left[a], source.Right);

                    FilterOutInvalidStructs(PolyInterfaceType.UnionStruct, compiledCodeData, LogMessages);

                    PolyInterfaceModel polyInterfaceModel = compiledCodeData.PolyInterfaceModel;

                    FileWriter writer = new FileWriter();

                    // Usings
                    writer.WriteUsingsAndRemoveDuplicates(GetCommonUsings());

                    writer.WriteLine($"");

                    writer.WriteInNamespace(polyInterfaceModel.TargetStructModel.Namespace, () =>
                    {
                        writer.WriteLine($"{Decorator_StructLayout_Explicit}");
                        writer.WriteLine($"public unsafe partial struct {polyInterfaceModel.TargetStructModel.Name} : {TypeName_IPolymorphicObject}");
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

                            // Properties
                            for (int i = 0; i < polyInterfaceModel.InterfacePropertyModels.Count; i++)
                            {
                                PropertyModel propertyModel = polyInterfaceModel.InterfacePropertyModels[i];

                                writer.WriteLine($"public {propertyModel.TypeMetaDataName} {propertyModel.Name}");
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
                                                        writer.WriteLine($"return Field_{polyStructModel.StructModel.Name}.{propertyModel.Name};");
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
                                                        writer.WriteLine($"Field_{polyStructModel.StructModel.Name}.{propertyModel.Name} = value;");
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

                            // IPolymorphicObject interface
                            {
                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public int GetBytesSize()");
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
                                                writer.WriteLine($"return {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.Name}>();");
                                            });
                                        }
                                    });
                                    writer.WriteLine($"return 0;");
                                });

                                writer.WriteLine($"");

                                writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                                writer.WriteLine($"public void WriteTo(byte* dstPtr, out int writeSize)");
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
                                                writer.WriteLine($"writeSize = {SizeOf_TypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.Name}>();");
                                                writer.WriteLine($"*(int*)dstPtr = (int)CurrentTypeId;");
                                                writer.WriteLine($"dstPtr += (long)4;");
                                                writer.WriteLine($"*({polyStructModel.StructModel.Name}*)dstPtr = Field_{polyStructModel.StructModel.Name};");
                                                writer.WriteLine($"break;");
                                            });
                                        }
                                    });
                                    writer.WriteLine($"");
                                    writer.WriteLine($"writeSize = 0;");
                                });
                            }

                            writer.WriteLine($"");

                            // Poly Methods
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

                            WriteLogMessagesOutputter(writer, LogMessages);
                        });
                    });
                    writer.WriteLine($"");

                    SourceText sourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
                    sourceProductionContext.AddSource($"{polyInterfaceModel.TargetStructModel.Name}{FileName_GeneratedSuffixAndFileType}", sourceText);
                }
            }
        }

        private static void FilterOutInvalidStructs(PolyInterfaceType polyInterfaceType, CompiledStructsForInterfaceData compiledData, List<LogMessage> logMessages)
        {
            switch (polyInterfaceType)
            {
                case PolyInterfaceType.UnionStruct:
                case PolyInterfaceType.ByteArray:
                    for (int i = compiledData.PolyStructModels.Count - 1; i >= 0; i--)
                    {
                        bool structIsValid = true;
                        PolyStructModel structModel = compiledData.PolyStructModels[i];

                        if (!compiledData.PolyInterfaceModel.AllowEntitiesAndBlobs)
                        {
                            SymbolsToProcess.Clear();
                            SymbolsToProcess.Add(structModel.StructTypeSymbol);
                            ProcessPreventingStructInvalidFieldOrPropertyTypes(SymbolsToProcess, polyInterfaceType, logMessages, ref structIsValid);
                        }

                        if (!structIsValid)
                        {
                            compiledData.PolyStructModels.RemoveAt(i);
                        }
                    }
                    break;
            }
        }

        private static void ProcessPreventingStructInvalidFieldOrPropertyTypes(
            List<ITypeSymbol> symbolsToProcess,
            PolyInterfaceType polyInterfaceType,
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
                        ProcessPreventingStructInvalidFieldOrPropertyTypes(polyInterfaceType, false, fieldSymbol.Type, logMessages, ref structIsValid);
                        if(structIsValid && !symbolEqualityComparer.Equals(fieldSymbol.Type, processedTypeSymbol))
                        {
                            symbolsToProcess.Add(fieldSymbol.Type);
                        }
                    }
                    if (memberSymbol.Kind == SymbolKind.Property && memberSymbol is IPropertySymbol propertySymbol)
                    {
                        ProcessPreventingStructInvalidFieldOrPropertyTypes(polyInterfaceType, true, propertySymbol.Type, logMessages, ref structIsValid);
                        if (structIsValid && !symbolEqualityComparer.Equals(propertySymbol.Type, processedTypeSymbol))
                        {
                            symbolsToProcess.Add(propertySymbol.Type);
                        }
                    }
                }
            }
        }

        private static void ProcessPreventingStructInvalidFieldOrPropertyTypes(
            PolyInterfaceType polyInterfaceType,
            bool isProperty,
            ITypeSymbol typeSymbol,
            List<LogMessage> messages,
            ref bool isStructValid)
        {
            string fullTypeName = typeSymbol.ToDisplayString();

            if (fullTypeName == MetaDataName_Entity)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, MetaDataName_Entity));
            }

            if (fullTypeName.Length >= MetaDataName_BlobString.Length && fullTypeName.Substring(0, MetaDataName_BlobString.Length) == MetaDataName_BlobString)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, MetaDataName_BlobString));
            }

            if (fullTypeName.Length >= MetaDataName_BlobAssetReference.Length && fullTypeName.Substring(0, MetaDataName_BlobAssetReference.Length) == MetaDataName_BlobAssetReference)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, MetaDataName_BlobAssetReference));
            }

            if (fullTypeName.Length >= MetaDataName_BlobArray.Length && fullTypeName.Substring(0, MetaDataName_BlobArray.Length) == MetaDataName_BlobArray)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, MetaDataName_BlobArray));
            }

            if (fullTypeName.Length >= MetaDataName_BlobPtr.Length && fullTypeName.Substring(0, MetaDataName_BlobPtr.Length) == MetaDataName_BlobPtr)
            {
                isStructValid = false;
                messages.Add(GetEntityAndBlobsError(isProperty, MetaDataName_BlobPtr));
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

        private static void GenerateTypeIdEnum(FileWriter writer, List<PolyStructModel> polyStructModels)
        {
            writer.WriteLine($"public enum {Name_Enum_TypeId}");
            writer.WriteInScope(() =>
            {
                for (int i = 0; i < polyStructModels.Count; i++)
                {
                    PolyStructModel polyStructModel = polyStructModels[i];
                    writer.WriteLine($"{polyStructModel.StructModel.Name},");
                }
            });
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