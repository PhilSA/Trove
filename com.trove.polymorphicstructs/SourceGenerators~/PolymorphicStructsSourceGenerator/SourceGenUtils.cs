using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace PolymorphicStructsSourceGenerators
{
    public static class SourceGenUtils
    {
        public static bool HasAttribute(BaseTypeDeclarationSyntax typeSyntax, string attributeName)
        {
            if (typeSyntax.AttributeLists != null)
            {
                foreach (AttributeListSyntax attributeList in typeSyntax.AttributeLists)
                {
                    foreach (AttributeSyntax attribute in attributeList.Attributes)
                    {
                        if (attribute.Name.ToString() == attributeName)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool HasAttribute(BaseMethodDeclarationSyntax methodSyntax, string attributeName)
        {
            if (methodSyntax.AttributeLists != null)
            {
                foreach (AttributeListSyntax attributeList in methodSyntax.AttributeLists)
                {
                    foreach (AttributeSyntax attribute in attributeList.Attributes)
                    {
                        if (attribute.Name.ToString() == attributeName)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool HasAttribute(ISymbol symbol, string attributeName)
        {
            System.Collections.Immutable.ImmutableArray<AttributeData> attributes = symbol.GetAttributes();
            if (attributes != null)
            {
                foreach (AttributeData attributeData in attributes)
                {
                    if (attributeData.AttributeClass.Name == attributeName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool ImplementsInterface(BaseTypeDeclarationSyntax typeSyntax, string interfaceName)
        {
            if (typeSyntax.BaseList != null)
            {
                foreach (BaseTypeSyntax type in typeSyntax.BaseList.Types)
                {
                    if (type.ToString() == interfaceName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string RefKindToString(RefKind refKind)
        {
            switch (refKind)
            {
                case RefKind.None:
                    return "";
                case RefKind.Ref:
                    return "ref";
                case RefKind.Out:
                    return "out";
                case RefKind.In:
                    return "in";
            }

            return "";
        }

        public static string GetNamespace(BaseTypeDeclarationSyntax syntax)
        {
            string nameSpace = string.Empty;
            SyntaxNode potentialNamespaceParent = syntax.Parent;

            while (potentialNamespaceParent != null && !(potentialNamespaceParent is NamespaceDeclarationSyntax))
            {
                potentialNamespaceParent = potentialNamespaceParent.Parent;
            }

            if (potentialNamespaceParent != null && potentialNamespaceParent is NamespaceDeclarationSyntax namespaceParent)
            {
                nameSpace = namespaceParent.Name.ToString();
            }

            return nameSpace;
        }
    }
}