using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace PolymorphicStructsSourceGenerators
{
    public static class SourceGenUtils
    {
        public static string GetFullNamespaceTypeName(ITypeSymbol symbol)
        {
            if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
            {

                string namespaceName = symbol.ContainingNamespace.ToDisplayString();
                return namespaceName;
            }
            return string.Empty;
        }

        public static string GetFullTypeName(ITypeSymbol symbol)
        {
            string typeName = symbol.ToDisplayString();
            return typeName;
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
    }
}