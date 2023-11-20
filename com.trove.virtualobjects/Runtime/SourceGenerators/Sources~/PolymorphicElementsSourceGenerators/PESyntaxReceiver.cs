using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace PolymorphicElementsSourceGenerators
{
    internal class PESyntaxReceiver : ISyntaxReceiver
    {
        public const string PEGroupAttributeName = "PolymorphicElementsGroup";
        public const string PEAttributeName = "PolymorphicElement";

        public List<InterfaceDeclarationSyntax> PolymorphicElementsGroupInterfaces = new List<InterfaceDeclarationSyntax>();
        public List<StructDeclarationSyntax> PolymorphicElementStructs = new List<StructDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is InterfaceDeclarationSyntax interfaceNode)
            {
                if (SourceGenUtils.HasAttribute(interfaceNode, PEGroupAttributeName))
                {
                    PolymorphicElementsGroupInterfaces.Add(interfaceNode);
                }
            }
            else if (syntaxNode is StructDeclarationSyntax structNode)
            {
                if (SourceGenUtils.HasAttribute(structNode, PEAttributeName))
                {
                    PolymorphicElementStructs.Add(structNode);
                }
            }
        }
    }
}