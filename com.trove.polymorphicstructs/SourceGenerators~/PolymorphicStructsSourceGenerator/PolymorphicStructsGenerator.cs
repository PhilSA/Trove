
using System.Collections.Generic;
using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PolymorphicStructsSourceGenerators
{
    public class CompiledPolyStructsAndInterfaceData
    {
        public PolyInterfaceModel PolyInterfaceModel = default;
        public List<PolyStructModel> PolyStructModels = new List<PolyStructModel>();
    }

    public struct PolyInterfaceModel
    {
        public string MetaDataName;
        public StructModel TargetStructModel;
        public List<MethodModel> InterfaceMethodModels;

        public List<string> Logs;
        public List<string> Errors;
    }

    public struct PolyStructModel
    {
        public StructModel StructModel;
        public List<string> InterfaceMetaDataNames;
    }

    public struct MethodModel
    {
        public string Name;
        public bool HasNonVoidReturnType;
        public string ReturnTypeMetaDataName;
        public string MethodGenericTypesDeclaration;
        public string MethodGenericTypesConstraint;
        public string MethodParametersDefinition;
        public string MethodParametersInvoke;
    }

    public struct StructModel
    {
        public string Name;
        public string Namespace;
        public string MetaDataName;
    }

    /*
    Plan
    - Find interfaces with [PolyInterface]
    - Find structs with [PolyStruct]
    - for each [PolyInterface] interface,
        - build list of [PolyStruct] structs that implement them
        - etc...
    - Generate a partial PolymorphicTypeManager
        - is it possible for users to define its name? What if they put a [PolyStructManager] on it?
        - Should I consider function pointers version
        - Contains:
            - Type enum
            - Writers
            - GetElementSize
            - Execute1
            - Execute2
            - variants of Executes for Arrays, Lists, Buffers, Streams
    - Generate a partial union struct
        - is it possible for users to define its name?
        - Should it be merge struct, or union struct?
            - Merge struct is better for entity remap
            - Union struct is better for perf
        - Contains:
            - Type enum
            - implicit casts
            - Execute1
            - Execute2

    */


    [Generator]
    public class PolymorphicStructsGenerator : IIncrementalGenerator
    {
        public const string NamespaceName_Package = "Trove.PolymorphicStructs";
        public const string NamespaceName_Generated = NamespaceName_Package + ".Generated";

        public const string TypeName_Void = "void";
        public const string TypeName_PolymorphicTypeManagerAttribute = "PolymorphicTypeManagerInterfaceAttribute";
        public const string TypeName_PolymorphicUnionStructAttribute = "PolymorphicUnionStructInterfaceAttribute";
        public const string TypeName_PolymorphicStructAttribute = "PolymorphicStructAttribute";
        public const string TypeName_UnsafeUtility = "Unity.Collections.LowLevel.Unsafe.UnsafeUtility";
        public const string TypeName_PolymorphicUtility = "Trove.PolymorphicStructs.PolymorphicUtility";
        public const string TypeName_NativeStream_Writer = "Unity.Collections.NativeStream.Writer";
        public const string TypeName_UnsafeStream_Writer = "Unity.Collections.LowLevel.Unsafe.UnsafeStream.Writer";
        public const string TypeName_NativeStream_Reader = "Unity.Collections.NativeStream.Reader";
        public const string TypeName_UnsafeStream_Reader = "Unity.Collections.LowLevel.Unsafe.UnsafeStream.Reader";
        public const string TypeName_NativeList_Byte = "Unity.Collections.NativeList<byte>";
        public const string TypeName_UnsafeList_Byte = "Unity.Collections.LowLevel.Unsafe.UnsafeList<byte>";
        public const string TypeName_DynamicBuffer_Byte = "Unity.Entities.DynamicBuffer<byte>";

        public const string MetaDataName_PolymorphicTypeManagerAttribute = NamespaceName_Generated + "." + TypeName_PolymorphicTypeManagerAttribute + "`1";
        public const string MetaDataName_PolymorphicUnionStructAttribute = NamespaceName_Generated + "." + TypeName_PolymorphicUnionStructAttribute + "`1";
        public const string MetaDataName_PolymorphicStructAttribute = NamespaceName_Generated + "." + TypeName_PolymorphicStructAttribute;

        public const string FileName_Errors = "PolymorphicStructErrors";
        public const string FileName_GeneratedSuffixAndFileType = ".generated.cs";
        public const string FileName_PolymorphicTypeManagerAttribute = TypeName_PolymorphicTypeManagerAttribute + FileName_GeneratedSuffixAndFileType;
        public const string FileName_PolymorphiUnionStructAttribute = TypeName_PolymorphicUnionStructAttribute + FileName_GeneratedSuffixAndFileType;
        public const string FileName_PolymorphicStructAttribute = "PolymorphicStructAttribute" + FileName_GeneratedSuffixAndFileType;

        public const string Decorator_DidReloadScripts = "[UnityEditor.Callbacks.DidReloadScripts]";
        public const string Decorator_MethodImpl_AggressiveInlining = "[System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]";

        public const string Name_ErrorIntro = "PolymorphicStructs source generator error:";
        public const string Name_Method_GetSizeForTypeId = "GetSizeForTypeId";
        public const string Name_Method_Write = "Write";
        public const string Name_Enum_TypeId = "TypeId";
        public const string Name_DestinationPtr = "destinationPtr";
        public const string Name_StreamWriter = "streamWriter";
        public const string Name_StreamReader = "streamReader";
        public const string Name_ByteList = "byteList";
        public const string Name_ByteBuffer = "byteBuffer";
        public const string Name_WriteIndex = "writeIndex";
        public const string Name_ByteArrayPtr = "byteArrayPtr";
        public const string Name_ByteArrayLength = "byteArrayLength";
        public const string Name_ByteIndex = "byteIndex";
        public const string Name_WriteBack = "writeBack";

        public const string ByteSizeOfTypeId = "4";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Generate attributes used for marking
            GenerateAttributes(context);

            // Create the values provider for poly interfaces and structs
            IncrementalValuesProvider<PolyInterfaceModel> polyTypeManagerValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                TypeName_PolymorphicTypeManagerAttribute,
                PolyTypeManagerValuesProviderPredicate,
                PolyTypeManagerInterfaceValuesProviderTransform);
            IncrementalValuesProvider<PolyInterfaceModel> polyUnionStructValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                TypeName_PolymorphicUnionStructAttribute,
                PolyUnionStructInterfaceValuesProviderPredicate,
                PolyUnionStructInterfaceValuesProviderTransform);
            IncrementalValuesProvider<PolyStructModel> polyStructValuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                MetaDataName_PolymorphicStructAttribute,
                PolyStructValuesProviderPredicate,
                PolyStructValuesProviderTransform);

            // Collect poly structs into an array, and create combined value providers of (PolyInterface, PolyStructsArray)
            IncrementalValueProvider<ImmutableArray<PolyStructModel>> polyStructsValueArrayProvider = polyStructValuesProvider.Collect();
            IncrementalValuesProvider<(PolyInterfaceModel Left, ImmutableArray<PolyStructModel> Right)> polyTypeManagerAndStructsValuesProvider = polyTypeManagerValuesProvider.Combine(polyStructsValueArrayProvider);
            IncrementalValuesProvider<(PolyInterfaceModel Left, ImmutableArray<PolyStructModel> Right)> polyUnionStructAndStructsValuesProvider = polyUnionStructValuesProvider.Combine(polyStructsValueArrayProvider);

            // For each element matching this pipeline, handle output
            context.RegisterSourceOutput(polyTypeManagerAndStructsValuesProvider, TypeManagerSourceOutputter);
            context.RegisterSourceOutput(polyUnionStructAndStructsValuesProvider, UnionStructSourceOutputter);
        }

        private void GenerateAttributes(IncrementalGeneratorInitializationContext context)
        {
            // Note: generated attributes should be internal so as to stop the attr from being generated in multiple projects
            // https://andrewlock.net/creating-a-source-generator-part-7-solving-the-source-generator-marker-attribute-problem-part1/

            // Generate attributes used in codegen
            context.RegisterPostInitializationOutput(i =>
            {
                FileWriter writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_Generated, () =>
                {
                    writer.WriteLine($"internal class {TypeName_PolymorphicTypeManagerAttribute} : System.Attribute {{}}");
                });
                i.AddSource(FileName_PolymorphicTypeManagerAttribute, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_Generated, () =>
                {
                    writer.WriteLine($"internal class {TypeName_PolymorphicUnionStructAttribute} : System.Attribute {{}}");
                });
                i.AddSource(FileName_PolymorphiUnionStructAttribute, writer.FileContents);

                writer = new FileWriter();
                writer.WriteInNamespace(NamespaceName_Generated, () =>
                {
                    writer.WriteLine($"internal class {TypeName_PolymorphicStructAttribute}: System.Attribute {{}}");
                });
                i.AddSource(FileName_PolymorphicStructAttribute, writer.FileContents);
            });
        }

        private static bool PolyTypeManagerValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is InterfaceDeclarationSyntax;
        }

        private static PolyInterfaceModel PolyTypeManagerInterfaceValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            // TODO: should this be there
            cancellationToken.ThrowIfCancellationRequested();

            return CommonInterfaceValuesProviderTransform(generatorAttributeSyntaxContext, cancellationToken, false);

        }

        private static bool PolyUnionStructInterfaceValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is InterfaceDeclarationSyntax;
        }

        private static PolyInterfaceModel PolyUnionStructInterfaceValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            // TODO: should this be there
            cancellationToken.ThrowIfCancellationRequested();

            return CommonInterfaceValuesProviderTransform(generatorAttributeSyntaxContext, cancellationToken, true);
        }

        private static bool PolyStructValuesProviderPredicate(SyntaxNode syntaxNode, System.Threading.CancellationToken cancellationToken)
        {
            return syntaxNode is StructDeclarationSyntax;
        }

        private static PolyStructModel PolyStructValuesProviderTransform(GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext, System.Threading.CancellationToken cancellationToken)
        {
            // TODO: should this be there
            cancellationToken.ThrowIfCancellationRequested();

            INamedTypeSymbol structTypeSymbol = generatorAttributeSyntaxContext.TargetSymbol.ContainingType;

            List<string> interfaceMetaDataNames = new List<string>();
            // TODO: support interface hierarchies
            foreach (INamedTypeSymbol structInterface in structTypeSymbol.Interfaces)
            {
                interfaceMetaDataNames.Add(structInterface.MetadataName);
            }

            return new PolyStructModel
            {
                InterfaceMetaDataNames = interfaceMetaDataNames,
                StructModel = new StructModel
                {
                    Name = structTypeSymbol.Name,
                    MetaDataName = structTypeSymbol.MetadataName,
                    // TODO: deal with global or nested namespaces
                    Namespace = structTypeSymbol.ContainingNamespace.MetadataName,
                },
            };
        }
        private static PolyInterfaceModel CommonInterfaceValuesProviderTransform(
            GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext,
            System.Threading.CancellationToken cancellationToken,
            bool requireTargetStructHasNoFields)
        {
            List<string> logs = new List<string>();
            List<string> errors = new List<string>();

            INamedTypeSymbol interfaceTypeSymbol = generatorAttributeSyntaxContext.TargetSymbol.ContainingType;

            // TODO: support interface hierarchy? (could be costly?)

            // Get target struct
            StructModel targetStructModel = new StructModel();
            {
                // Error if the attribute appears multiple times
                if (generatorAttributeSyntaxContext.Attributes.Length > 1)
                {
                    errors.Add($"{Name_ErrorIntro} Cannot have polymorphic struct/interface attributes multiple times on the same type.");
                }

                INamedTypeSymbol targetStructSymbol = generatorAttributeSyntaxContext.Attributes[0].AttributeClass.TypeParameters[0].ContainingType;

                targetStructModel.Name = targetStructSymbol.Name;
                targetStructModel.MetaDataName = targetStructSymbol.MetadataName;
                // TODO: deal with global or nested namespaces
                targetStructModel.Namespace = targetStructSymbol.ContainingNamespace.MetadataName;

                // Check that target struct has no fields or properties(if needed)
                if (requireTargetStructHasNoFields)
                {
                    foreach (ISymbol memberSymbol in targetStructSymbol.GetMembers())
                    {
                        if (memberSymbol.Kind == SymbolKind.Field || memberSymbol.Kind == SymbolKind.Property)
                        {
                            errors.Add($"{Name_ErrorIntro} The generic struct type {targetStructModel.Name} targeted by the {interfaceTypeSymbol.Name} must not have any fields or properties.");
                            break;
                        }
                    }
                }

                // TODO: ensure struct is public partial
                //bool isPublic = false;
                //bool isPartial = false;
                //foreach (var modifier in elementStructSyntax.Modifiers)
                //{
                //    if (modifier.IsKind(SyntaxKind.PublicKeyword))
                //    {
                //        isPublic = true;
                //    }
                //    if (modifier.IsKind(SyntaxKind.PartialKeyword))
                //    {
                //        isPartial = true;
                //    }
                //}
                //elementData.IsPublicPartial = isPublic && isPartial;
            }

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

                    interfaceMethodModels.Add(methodModel);
                }
            }

            return new PolyInterfaceModel
            {
                MetaDataName = interfaceTypeSymbol.MetadataName,
                TargetStructModel = targetStructModel,
                InterfaceMethodModels = interfaceMethodModels,

                Logs = logs,
                Errors = errors,
            };
        }

        private static CompiledPolyStructsAndInterfaceData CreateCompiledCodeData(PolyInterfaceModel polyInterfaceModel, ImmutableArray<PolyStructModel> polyStructModels)
        {
            CompiledPolyStructsAndInterfaceData compiledCodeData = new CompiledPolyStructsAndInterfaceData();

            // TODO: Sanity checks
            // - no return type on interface methods
            // - no 2 structs with same name (structModel.Name)

            // Add poly structs implementing this poly interface to a list
            compiledCodeData.PolyStructModels.Clear();
            ImmutableArray<PolyStructModel>.Enumerator polyStructModelsEnumerator = polyStructModels.GetEnumerator();
            while (polyStructModelsEnumerator.MoveNext())
            {
                List<string> structInterfaces = polyStructModelsEnumerator.Current.InterfaceMetaDataNames;
                for (int i = 0; i < structInterfaces.Count; i++)
                {
                    // TODO: compare name hashes instead?
                    if (structInterfaces[i] == polyInterfaceModel.MetaDataName)
                    {
                        compiledCodeData.PolyStructModels.Add(polyStructModelsEnumerator.Current);
                        break;
                    }
                }
            }

            return compiledCodeData;
        }

        private static void TypeManagerSourceOutputter(SourceProductionContext sourceProductionContext, (PolyInterfaceModel Left, ImmutableArray<PolyStructModel> Right) source)
        {
            CompiledPolyStructsAndInterfaceData compiledCodeData = CreateCompiledCodeData(source.Left, source.Right);
            FileWriter writer = new FileWriter();

            // Usings
            writer.WriteUsingsAndRemoveDuplicates(new List<string>
            {
                "System",
                "Unity.Collections.LowLevel.Unsafe",
            });

            writer.WriteLine($"");

            PolyInterfaceModel polyInterfaceModel = compiledCodeData.PolyInterfaceModel;

            writer.WriteInNamespace(polyInterfaceModel.TargetStructModel.Namespace, () =>
            {
                writer.WriteLine($"public unsafe partial {polyInterfaceModel.TargetStructModel.Name}");
                writer.WriteInScope(() =>
                {
                    // Types enum
                    GenerateTypeIdEnum(writer, compiledCodeData.PolyStructModels);

                    writer.WriteLine($"");

                    // GetSizeForTypeId 
                    writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                    writer.WriteLine($"public int {Name_Method_GetSizeForTypeId}(int typeId)");
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

                    // Writers
                    for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                    {
                        PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];

                        // To ptr
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static void {Name_Method_Write}(byte* {Name_DestinationPtr}, {polyStructModel.StructModel.MetaDataName} s)");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"{TypeName_UnsafeUtility}.AsRef<int>({Name_DestinationPtr}) = (int){Name_Enum_TypeId}.{polyStructModel.StructModel.Name}");
                            writer.WriteLine($"{Name_DestinationPtr} = {Name_DestinationPtr} + (long){ByteSizeOfTypeId}");
                            writer.WriteLine($"{TypeName_UnsafeUtility}.AsRef<{polyStructModel.StructModel.MetaDataName}>({Name_DestinationPtr}) = s");
                        });

                        writer.WriteLine($"");

                        // To NativeStream
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static void {Name_Method_Write}(ref {TypeName_NativeStream_Writer} {Name_StreamWriter}, {polyStructModel.StructModel.MetaDataName} s)");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"{Name_Method_Write}({Name_StreamWriter}.Allocate({ByteSizeOfTypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>()), s);");
                        });

                        writer.WriteLine($"");

                        // To UnsafeStream
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static void {Name_Method_Write}(ref {TypeName_UnsafeStream_Writer} {Name_StreamWriter}, {polyStructModel.StructModel.MetaDataName} s)");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"{Name_Method_Write}({Name_StreamWriter}.Allocate({ByteSizeOfTypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>()), s);");
                        });

                        writer.WriteLine($"");

                        // To NativeList<byte>
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static void {Name_Method_Write}(ref {TypeName_NativeList_Byte} {Name_ByteList}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_WriteIndex})");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"{Name_WriteIndex} = {Name_ByteList}.Length");
                            writer.WriteLine($"int newLength = {Name_WriteIndex} + {ByteSizeOfTypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                            writer.WriteLine($"if(newLength > {Name_ByteList}.Capacity)");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"{Name_ByteList}.SetCapacity(newLength * 2);");
                            });
                            writer.WriteLine($"{Name_ByteList}.ResizeUninitialized(newLength);");
                            writer.WriteLine($"{Name_Method_Write}({Name_ByteList}.GetUnsafePtr(), s);");
                        });

                        writer.WriteLine($"");

                        // To UnsafeList<byte>
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static void {Name_Method_Write}(ref {TypeName_UnsafeList_Byte} {Name_ByteList}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_WriteIndex})");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"{Name_WriteIndex} = {Name_ByteList}.Length");
                            writer.WriteLine($"int newLength = {Name_WriteIndex} + {ByteSizeOfTypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                            writer.WriteLine($"if(newLength > {Name_ByteList}.Capacity)");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"{Name_ByteList}.SetCapacity(newLength * 2);");
                            });
                            writer.WriteLine($"{Name_ByteList}.Resize(newLength);");
                            writer.WriteLine($"{Name_Method_Write}({Name_ByteList}.Ptr, s);");
                        });

                        writer.WriteLine($"");

                        // To DynamicBuffer<byte>
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static void {Name_Method_Write}(ref {TypeName_DynamicBuffer_Byte} {Name_ByteBuffer}, {polyStructModel.StructModel.MetaDataName} s, out int {Name_WriteIndex})");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"{Name_WriteIndex} = {Name_ByteBuffer}.Length");
                            writer.WriteLine($"int newLength = {Name_WriteIndex} + {ByteSizeOfTypeId} + {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                            writer.WriteLine($"if(newLength > {Name_ByteBuffer}.Capacity)");
                            writer.WriteInScope(() =>
                            {
                                writer.WriteLine($"{Name_ByteBuffer}.EnsureCapacity(newLength * 2);");
                            });
                            writer.WriteLine($"{Name_ByteBuffer}.ResizeUninitialized(newLength);");
                            writer.WriteLine($"{Name_Method_Write}((byte*){Name_ByteList}.GetUnsafePtr(), s);");
                        });
                    }

                    writer.WriteLine($"");

                    // Method executors
                    for (int i = 0; i < polyInterfaceModel.InterfaceMethodModels.Count; i++)
                    {
                        MethodModel methodModel = polyInterfaceModel.InterfaceMethodModels[i];

                        // From Ptr
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}(byte* {Name_ByteArrayPtr}, int {Name_ByteArrayLength}, ref int {Name_ByteIndex}, bool {Name_WriteBack}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"int startByteIndex = {Name_ByteIndex};");
                            writer.WriteLine($"");

                            // If can read typeId
                            writer.WriteLine($"if ({Name_ByteArrayLength} >= {Name_ByteIndex} + {ByteSizeOfTypeId})");
                            writer.WriteInScope(() =>
                            {
                                // Read typeId
                                writer.WriteLine($"byte* readPtr = {Name_ByteArrayPtr} + (long){Name_ByteIndex};");
                                writer.WriteLine($"{TypeName_UnsafeUtility}.CopyPtrToStructure(readPtr, out int typeId);");
                                writer.WriteLine($"{Name_ByteIndex} += {ByteSizeOfTypeId};");

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
                                            writer.WriteLine($"int sizeOfStruct = {TypeName_UnsafeUtility}.SizeOf<{polyStructModel.StructModel.MetaDataName}>();");
                                            writer.WriteLine($"if ({Name_ByteArrayLength} >= {Name_ByteIndex} + sizeOfStruct)");
                                            writer.WriteInScope(() =>
                                            {
                                                // Read struct
                                                writer.WriteLine($"readPtr = {Name_ByteArrayPtr} + (long){Name_ByteIndex};");
                                                writer.WriteLine($"{TypeName_UnsafeUtility}.CopyPtrToStructure(readPtr, out {polyStructModel.StructModel.MetaDataName} __s);");
                                                writer.WriteLine($"{Name_ByteIndex} += sizeOfStruct;");

                                                writer.WriteLine($"");

                                                // Invoke method on struct
                                                writer.WriteLine($"__s.{methodModel.Name}({methodModel.MethodParametersInvoke});");

                                                writer.WriteLine($"");

                                                // Handle struct writeback
                                                writer.WriteLine($"if ({Name_WriteBack})");
                                                writer.WriteInScope(() =>
                                                {
                                                    writer.WriteLine($"byte* writePtr = {Name_ByteArrayPtr} + (long)startByteIndex;");
                                                    writer.WriteLine($"{TypeName_UnsafeUtility}.AsRef<{polyStructModel.StructModel.MetaDataName}>(writePtr) = __s;");
                                                });

                                                writer.WriteLine($"");

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

                        writer.WriteLine($"");
                        writer.WriteInScope(() =>
                        {
                        });

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
                                writer.WriteLine($"int typeId = {Name_StreamReader}.Read<int>();");

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

                                            writer.WriteLine($"");

                                            // Invoke method on struct
                                            // TODO: handle generic methods
                                            writer.WriteLine($"__s.{methodModel.Name}({methodModel.MethodParametersInvoke});");

                                            writer.WriteLine($"");

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
                                writer.WriteLine($"int typeId = {Name_StreamReader}.Read<int>();");

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

                                            writer.WriteLine($"");

                                            // Invoke method on struct
                                            writer.WriteLine($"__s.{methodModel.Name}({methodModel.MethodParametersInvoke});");

                                            writer.WriteLine($"");

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
                        writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}({TypeName_NativeList_Byte} {Name_ByteList}, ref int {Name_ByteIndex}, bool {Name_WriteBack}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"return {methodModel.Name}({Name_ByteList}.GetUnsafePtr(), {Name_ByteList}.Length, ref {Name_ByteIndex}, {Name_WriteBack}, {methodModel.MethodParametersInvoke})");
                        });

                        writer.WriteLine($"");

                        // From UnsafeList
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}({TypeName_UnsafeList_Byte} {Name_ByteList}, ref int {Name_ByteIndex}, bool {Name_WriteBack}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"return {methodModel.Name}({Name_ByteList}.GetUnsafePtr(), {Name_ByteList}.Length, ref {Name_ByteIndex}, {Name_WriteBack}, {methodModel.MethodParametersInvoke})");
                        });

                        writer.WriteLine($"");

                        // From DynamicBuffer
                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public static bool {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}({TypeName_DynamicBuffer_Byte} {Name_ByteBuffer}, ref int {Name_ByteIndex}, bool {Name_WriteBack}, {methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"return {methodModel.Name}((byte*){Name_ByteBuffer}.GetUnsafePtr(), {Name_ByteList}.Length, ref {Name_ByteIndex}, {Name_WriteBack}, {methodModel.MethodParametersInvoke})");
                        });

                        writer.WriteLine($"");
                    }
                });
            });
            writer.WriteLine($"");

            SourceText sourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
            sourceProductionContext.AddSource($"{polyInterfaceModel.TargetStructModel.Name}{FileName_GeneratedSuffixAndFileType}", sourceText);

            OutputErrorsAndLogs(sourceProductionContext, compiledCodeData.PolyInterfaceModel.Logs, compiledCodeData.PolyInterfaceModel.Errors);
        }

        private static void UnionStructSourceOutputter(SourceProductionContext sourceProductionContext, (PolyInterfaceModel Left, ImmutableArray<PolyStructModel> Right) source)
        {
            CompiledPolyStructsAndInterfaceData compiledCodeData = CreateCompiledCodeData(source.Left, source.Right);

            FileWriter writer = new FileWriter();

            // Usings
            writer.WriteUsingsAndRemoveDuplicates(new List<string>
            {
                "System",
                "Unity.Collections.LowLevel.Unsafe",
            });

            writer.WriteLine($"");

            PolyInterfaceModel polyInterfaceModel = compiledCodeData.PolyInterfaceModel;

            writer.WriteInNamespace(polyInterfaceModel.TargetStructModel.Namespace, () =>
            {
                writer.WriteLine($"[StructLayout(LayoutKind.Explicit)]");
                writer.WriteLine($"public unsafe partial struct {polyInterfaceModel.TargetStructModel.Name}");
                writer.WriteInScope(() =>
                {
                    // Types enum
                    GenerateTypeIdEnum(writer, compiledCodeData.PolyStructModels);

                    writer.WriteLine($"");

                    // Union fields
                    writer.WriteLine($"[FieldOffset(0)]");
                    writer.WriteLine($"public int TypeId;");
                    for (int i = 0; i < compiledCodeData.PolyStructModels.Count; i++)
                    {
                        PolyStructModel polyStructModel = compiledCodeData.PolyStructModels[i];
                        writer.WriteLine($"[FieldOffset({ByteSizeOfTypeId})]");
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
                                writer.WriteLine($"TypeId = {Name_Enum_TypeId}.{polyStructModel.StructModel.Name},");
                                writer.WriteLine($"Field_{polyStructModel.StructModel.Name} = s,;");
                            }, ";");
                        });

                        writer.WriteLine($"");

                        // Cast union struct to struct
                        writer.WriteLine($"public static implicit operator {polyStructModel.StructModel.MetaDataName}({polyInterfaceModel.TargetStructModel.Name} s)");
                        writer.WriteInScope(() =>
                        {
                            writer.WriteLine($"return Field_{polyStructModel.StructModel.Name};");
                        });

                        writer.WriteLine($"");
                    }

                    writer.WriteLine($"");

                    // TODO: Methods
                    for (int i = 0; i < polyInterfaceModel.InterfaceMethodModels.Count; i++)
                    {
                        MethodModel methodModel = polyInterfaceModel.InterfaceMethodModels[i];

                        writer.WriteLine($"{Decorator_MethodImpl_AggressiveInlining}");
                        writer.WriteLine($"public {methodModel.ReturnTypeMetaDataName} {methodModel.Name}{methodModel.MethodGenericTypesDeclaration}({methodModel.MethodParametersDefinition}){methodModel.MethodGenericTypesConstraint}");
                        writer.WriteInScope(() =>
                        {
                            // Switch over typeId
                            writer.WriteLine($"switch (TypeId)");
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
                        });

                        writer.WriteLine($"");
                    }
                });
            });
            writer.WriteLine($"");

            SourceText sourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
            sourceProductionContext.AddSource($"{polyInterfaceModel.TargetStructModel.Name}{FileName_GeneratedSuffixAndFileType}", sourceText);

            OutputErrorsAndLogs(sourceProductionContext, compiledCodeData.PolyInterfaceModel.Logs, compiledCodeData.PolyInterfaceModel.Errors);
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

        private static void OutputErrorsAndLogs(SourceProductionContext sourceProductionContext, List<string> logs, List<string> errors)
        {
            // TODO;
            FileWriter writer = new FileWriter();

            writer.WriteLine($"");

            writer.WriteLine($"{Decorator_DidReloadScripts}");
            writer.WriteLine($"private static void PolymorphicStructSourceGenLogs()");
            writer.WriteInScope(() =>
            {
                for (int i = 0; i < logs.Count; i++)
                {
                    writer.WriteLine($"UnityEngine.Debug.Log(\"{logs[i]}\")");
                }
                for (int i = 0; i < errors.Count; i++)
                {
                    writer.WriteLine($"UnityEngine.Debug.LogError(\"{errors[i]}\")");
                }
            });

            SourceText sourceText = SourceText.From(writer.FileContents, Encoding.UTF8);
            sourceProductionContext.AddSource($"{FileName_Errors}{FileName_GeneratedSuffixAndFileType}", sourceText);
        }
    }
}