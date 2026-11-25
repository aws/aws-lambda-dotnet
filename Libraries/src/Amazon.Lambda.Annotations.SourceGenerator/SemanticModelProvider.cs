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
        /// Returns the BuildServiceProvider method's semantic model in the LambdaStartup attributed class,
        /// if the class defines one. The method is expected to take a single IServiceCollection parameter
        /// and return an IServiceProvider, allowing an alternate IoC container to be used.
        /// The signature is validated by the caller; this method returns the symbol even when the
        /// signature does not match so an invalid definition can be reported.
        /// If <see cref="startupSyntax"/> is null, returns null.
        /// </summary>
        /// <param name="startupSyntax">LambdaStartup attributed class syntax</param>
        public IMethodSymbol GetBuildServiceProviderMethodModel(ClassDeclarationSyntax startupSyntax)
        {
            if (startupSyntax == null)
            {
                return null;
            }

            var classModel = _context.Compilation.GetSemanticModel(startupSyntax.SyntaxTree);

            foreach (var member in startupSyntax.Members.Where(member => member.Kind() == SyntaxKind.MethodDeclaration))
            {
                var methodSyntax = (MethodDeclarationSyntax)member;
                var methodSymbol = classModel.GetDeclaredSymbol(methodSyntax);
                if (methodSymbol != null && methodSymbol.Name == "BuildServiceProvider")
                {
                    return methodSymbol;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns true if <paramref name="buildServiceProviderMethodSymbol"/> is a valid
        /// BuildServiceProvider hook: a non-private method taking a single IServiceCollection
        /// parameter and returning IServiceProvider or a type implementing it.
        /// </summary>
        public bool IsValidBuildServiceProviderMethod(IMethodSymbol buildServiceProviderMethodSymbol)
        {
            if (buildServiceProviderMethodSymbol == null)
            {
                return false;
            }

            if (buildServiceProviderMethodSymbol.DeclaredAccessibility == Accessibility.Private)
            {
                return false;
            }

            if (buildServiceProviderMethodSymbol.Parameters.Length != 1)
            {
                return false;
            }

            var iServiceCollectionSymbol = _context.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");
            if (!buildServiceProviderMethodSymbol.Parameters[0].Type.Equals(iServiceCollectionSymbol, SymbolEqualityComparer.Default))
            {
                return false;
            }

            var iServiceProviderSymbol = _context.Compilation.GetTypeByMetadataName("System.IServiceProvider");
            var returnType = buildServiceProviderMethodSymbol.ReturnType;
            if (returnType.Equals(iServiceProviderSymbol, SymbolEqualityComparer.Default))
            {
                return true;
            }

            return returnType.AllInterfaces.Any(i => i.Equals(iServiceProviderSymbol, SymbolEqualityComparer.Default));
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
