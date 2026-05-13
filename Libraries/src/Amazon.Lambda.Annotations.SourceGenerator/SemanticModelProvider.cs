using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    public class SemanticModelProvider
    {
        private readonly GeneratorExecutionContext _context;

        public SemanticModelProvider(GeneratorExecutionContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns the Configure(IServiceCollection) method's semantic model in the LambdaStartup attributed class.
        /// If <see cref="startupSyntax"/> is null, returns null.
        /// </summary>
        /// <param name="startupSyntax">LambdaStartup attributed class syntax</param>
        public IMethodSymbol GetConfigureServicesMethodModel(ClassDeclarationSyntax startupSyntax)
        {
            if (startupSyntax == null)
            {
                return null;
            }

            IMethodSymbol configureServicesMethodSymbol = null;

            var iServiceCollectionSymbol = _context.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");

            var classModel = _context.Compilation.GetSemanticModel(startupSyntax.SyntaxTree);

            // Filter the methods which can potentially have Configure(IServiceCollection) parameter signature
            var members = startupSyntax.Members.Where(member => member.Kind() == SyntaxKind.MethodDeclaration);

            foreach (var member in members)
            {
                var methodSyntax = (MethodDeclarationSyntax) member;
                var methodSymbol = classModel.GetDeclaredSymbol(methodSyntax);
                if (methodSymbol != null
                    && methodSymbol.Name == "ConfigureServices"
                    && methodSymbol.Parameters.Count() == 1
                    && methodSymbol.Parameters[0].Type
                        .Equals(iServiceCollectionSymbol, SymbolEqualityComparer.Default))
                {
                    configureServicesMethodSymbol = methodSymbol;
                    break;
                }
            }

            return configureServicesMethodSymbol;
        }

        public IMethodSymbol GetConfigureHostBuilderMethodModel(ClassDeclarationSyntax startupSyntax)
        {
            if (startupSyntax == null)
            {
                return null;
            }

            IMethodSymbol configureHostBuilderMethodSymbol = null;

            var classModel = _context.Compilation.GetSemanticModel(startupSyntax.SyntaxTree);

            foreach (var member in startupSyntax.Members.Where(member => member.Kind() == SyntaxKind.MethodDeclaration))
            {
                var methodSyntax = (MethodDeclarationSyntax)member;
                var methodSymbol = classModel.GetDeclaredSymbol(methodSyntax);
                if (methodSymbol != null && methodSymbol.Name == "ConfigureHostBuilder")
                {
                    configureHostBuilderMethodSymbol = methodSymbol;
                    break;
                }
            }

            return configureHostBuilderMethodSymbol;
        }

        /// <summary>
        /// Returns semantic model of <see cref="syntax"/>
        /// </summary>
        /// <param name="syntax">Method declaration syntax used for representing method by compiler.</param>
        public IMethodSymbol GetMethodSemanticModel(MethodDeclarationSyntax syntax)
        {
            var methodModel = _context.Compilation.GetSemanticModel(syntax.SyntaxTree);
            return methodModel.GetDeclaredSymbol(syntax);
        }
    }
}
