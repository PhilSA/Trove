using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolymorphicElementsSourceGenerators
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