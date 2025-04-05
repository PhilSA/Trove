using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace PolymorphicStructsSourceGenerators
{
    public static class SourceGenUtils
    {
        public static string GetNamespaceMetaDataName(ISymbol typeSymbol)
        {
            string n = string.Empty;
            if (typeSymbol.ContainingNamespace != null)
            {
                n = typeSymbol.ContainingNamespace.MetadataName;
            }
            return n;
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