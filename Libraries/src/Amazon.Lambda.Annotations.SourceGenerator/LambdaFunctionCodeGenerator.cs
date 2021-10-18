using System;
using System.Linq;
using System.Text;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    public class LambdaFunctionCodeGenerator
    {
        private readonly MethodDeclarationSyntax _lambdaMethodSyntax;
        private readonly ClassDeclarationSyntax _startupSyntax;
        private readonly GeneratorExecutionContext _context;
        private IMethodSymbol _lambdaMethodModel;
        private IMethodSymbol _configureMethodSymbol;

        /// <summary>
        /// Represents simplified Lambda function,
        /// It may or may not have the parameters such as customer input and ILambdaContext.
        /// </summary>
        public IMethodSymbol LambdaMethodSymbol
        {
            get
            {
                if (_lambdaMethodModel == null)
                {
                    var methodModel = _context.Compilation.GetSemanticModel(_lambdaMethodSyntax.SyntaxTree);
                    _lambdaMethodModel = methodModel.GetDeclaredSymbol(_lambdaMethodSyntax);
                }

                return _lambdaMethodModel;
            }
        }

        public INamedTypeSymbol ReturnType => LambdaMethodSymbol.ReturnType as INamedTypeSymbol;

        /// <summary>
        /// Represents the Configure(IServiceCollection) method in the LambdaStartup attributed class.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when multiple LambdaStartup attributed classes exist in the project.</exception>
        public IMethodSymbol ConfigureMethodSymbol
        {
            get
            {
                // There doesn't exist LambdaStartup class
                // There is no need to look for Configure(IServiceCollection) method
                if (_startupSyntax == null)
                {
                    return null;
                }

                if (_configureMethodSymbol == null)
                {
                    var iServiceCollectionSymbol =
                        _context.Compilation.GetTypeByMetadataName(
                            "Microsoft.Extensions.DependencyInjection.IServiceCollection");

                    var classModel = _context.Compilation.GetSemanticModel(_startupSyntax.SyntaxTree);

                    // Filter the methods which can potentially have Configure(IServiceCollection) parameter signature
                    var members = _startupSyntax.Members.Where(member => member.Kind() == SyntaxKind.MethodDeclaration);

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
                            _configureMethodSymbol = methodSymbol;
                            break;
                        }
                    }
                }

                return _configureMethodSymbol;
            }
        }

        public LambdaFunctionCodeGenerator(
            MethodDeclarationSyntax lambdaMethodSyntax,
            ClassDeclarationSyntax startupSyntax,
            GeneratorExecutionContext context)
        {
            _lambdaMethodSyntax = lambdaMethodSyntax;
            _startupSyntax = startupSyntax;
            _context = context;
        }

        /// <summary>
        /// Generates <see cref="SourceText"/> for the LambdaFunction including setting up the dependency injection.
        /// </summary>
        /// <returns>A <see cref="Tuple"/> containing source file name hint and source text.</returns>
        public (string, SourceText) GenerateSource()
        {
            var model = LambdaFunctionModelBuilder.Build(LambdaMethodSymbol, ConfigureMethodSymbol, _context);
            var template = new LambdaFunctionTemplate(model);
            var sourceText = template.TransformText();
            return ($"{model.GeneratedMethod.ContainingType.Name}.g.cs", SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));
        }
    }
}