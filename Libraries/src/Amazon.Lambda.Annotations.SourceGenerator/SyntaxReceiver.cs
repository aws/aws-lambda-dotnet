using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    internal class SyntaxReceiver : ISyntaxContextReceiver
    {
        public List<MethodDeclarationSyntax> LambdaMethods { get; } = new List<MethodDeclarationSyntax>();

        public List<ClassDeclarationSyntax> StartupClasses { get; private set; } = new List<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            // any method with at least one attribute is a candidate of function generation
            if (context.Node is MethodDeclarationSyntax methodDeclarationSyntax && methodDeclarationSyntax.AttributeLists.Count > 0)
            {
                // Get the symbol being declared by the method, and keep it if its annotated
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
                if (methodSymbol.GetAttributes().Any(attr => attr.AttributeClass.Name == nameof(LambdaFunctionAttribute)))
                {
                    LambdaMethods.Add(methodDeclarationSyntax);
                }
            }

            // any class with at least one attribute is a candidate of Startup class
            if (context.Node is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.AttributeLists.Count > 0)
            {
                // Get the symbol being declared by the class, and keep it if its annotated
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax);
                if (methodSymbol.GetAttributes().Any(attr => attr.AttributeClass.Name == nameof(LambdaStartupAttribute)))
                {
                    StartupClasses.Add(classDeclarationSyntax);
                }
            }
        }
    }
}