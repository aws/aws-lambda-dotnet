using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    using Microsoft.CodeAnalysis.CSharp;

    internal class SyntaxReceiver : ISyntaxContextReceiver
    {
        /// <summary>
        /// Secondary attribute names that require the [LambdaFunction] attribute to also be present.
        /// The key is the attribute class name, the value is the user-friendly name for diagnostics.
        /// </summary>
        private static readonly Dictionary<string, string> _secondaryAttributeNames = new Dictionary<string, string>
        {
            { "HttpApiAuthorizerAttribute", "HttpApiAuthorizer" },
            { "RestApiAuthorizerAttribute", "RestApiAuthorizer" },
            { "HttpApiAttribute", "HttpApi" },
            { "RestApiAttribute", "RestApi" },
            { "SQSEventAttribute", "SQSEvent" }
        };

        public List<MethodDeclarationSyntax> LambdaMethods { get; } = new List<MethodDeclarationSyntax>();

        public List<ClassDeclarationSyntax> StartupClasses { get; private set; } = new List<ClassDeclarationSyntax>();
        
        public List<MethodDeclarationSyntax> MethodDeclarations { get; } = new List<MethodDeclarationSyntax>();

        /// <summary>
        /// Methods that have a secondary Lambda annotation attribute but are missing [LambdaFunction].
        /// Each entry is a tuple of the method syntax, its location, and the friendly name of the secondary attribute found.
        /// </summary>
        public List<(MethodDeclarationSyntax Method, string AttributeName)> MethodsWithMissingLambdaFunction { get; } = new List<(MethodDeclarationSyntax, string)>();

        /// <summary>
        /// Path to the directory containing the .csproj file
        /// </summary>
        public string ProjectDirectory 
        {
            get
            {
                return _directoryManager.GetDirectoryName(ProjectPath);
            }
        }

        /// <summary>
        /// Path to the .csproj file
        /// </summary>
        public string ProjectPath { get; private set; }

        private readonly IFileManager _fileManager;
        private readonly IDirectoryManager _directoryManager;

        public SyntaxReceiver(IFileManager fileManager, IDirectoryManager directoryManager)
        {
            _fileManager = fileManager;
            _directoryManager = directoryManager;
        }

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if(this.ProjectDirectory == null && context.Node is ClassDeclarationSyntax)
            {
                var templateHandler = new CloudFormationTemplateHandler(_fileManager, _directoryManager);
                this.ProjectPath = templateHandler.DetermineProjectPath(context.Node.SyntaxTree.FilePath);
            }

            // any method with at least one attribute is a candidate of function generation
            if (context.Node is MethodDeclarationSyntax methodDeclarationSyntax && methodDeclarationSyntax.AttributeLists.Count > 0)
            {
                // Get the symbol being declared by the method, and keep it if its annotated
                var methodSymbol = ModelExtensions.GetDeclaredSymbol(
                    context.SemanticModel,
                    methodDeclarationSyntax);
                var attributes = methodSymbol.GetAttributes();
                if (attributes.Any(attr => attr.AttributeClass.Name == nameof(LambdaFunctionAttribute)))
                {
                    LambdaMethods.Add(methodDeclarationSyntax);
                }
                else
                {
                    // Check if the method has a secondary attribute without [LambdaFunction]
                    foreach (var attr in attributes)
                    {
                        var attrName = attr.AttributeClass?.Name;
                        if (attrName != null && _secondaryAttributeNames.TryGetValue(attrName, out var friendlyName))
                        {
                            MethodsWithMissingLambdaFunction.Add((methodDeclarationSyntax, friendlyName));
                            break; // Only report once per method
                        }
                    }
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
            
            if (context.Node is MethodDeclarationSyntax methodDeclaration)
            {
                var model = context.SemanticModel.GetDeclaredSymbol(
                    methodDeclaration);

                if (model.Name == "Main" && model.IsStatic)
                {
                    MethodDeclarations.Add(methodDeclaration);   
                }
            }
        }
    }
}