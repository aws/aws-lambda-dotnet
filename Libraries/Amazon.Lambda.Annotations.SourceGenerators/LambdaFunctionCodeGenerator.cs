using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerators
{
    public class LambdaFunctionCodeGenerator
    {
        private readonly MethodDeclarationSyntax _lambdaFunctionMethodSyntax;
        private readonly ClassDeclarationSyntax _startupSyntax;
        private readonly GeneratorExecutionContext _context;
        private IMethodSymbol _lambdaFunctionMethodModel;
        private IMethodSymbol _configureMethodSymbol;

        /// <summary>
        /// Returns class name of the generated method.
        /// </summary>
        public string ClassName => $"{LambdaFunctionMethodSymbol.ContainingType.Name}_{LambdaFunctionMethodSymbol.Name}_Generated";

        /// <summary>
        /// Represents simplified Lambda function,
        /// It may or may not have the parameters such as customer input and ILambdaContext.
        /// </summary>
        public IMethodSymbol LambdaFunctionMethodSymbol
        {
            get
            {
                if (_lambdaFunctionMethodModel == null)
                {
                    var methodModel = _context.Compilation.GetSemanticModel(_lambdaFunctionMethodSyntax.SyntaxTree);
                    _lambdaFunctionMethodModel = methodModel.GetDeclaredSymbol(_lambdaFunctionMethodSyntax);
                }

                return _lambdaFunctionMethodModel;
            }
        }

        public INamedTypeSymbol ReturnType => LambdaFunctionMethodSymbol.ReturnType as INamedTypeSymbol;
        
        /// <summary>
        /// Represents the Configure(IServiceCollection) method in the LambdaStartup attributed class.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when multiple LambdaStartup attributed classes exist in the project.</exception>
        public IMethodSymbol ConfigureMethodSymbol
        {
            get
            {
                if (_configureMethodSymbol == null)
                {
                    var iServiceCollectionSymbol =
                        _context.Compilation.GetTypeByMetadataName(
                            "Microsoft.Extensions.DependencyInjection.IServiceCollection");

                    var classModel = _context.Compilation.GetSemanticModel(_startupSyntax.SyntaxTree);
                    foreach (var member in _startupSyntax.Members.Where(member => member.Kind() == SyntaxKind.MethodDeclaration))
                    {
                        var methodSyntax = (MethodDeclarationSyntax)member;
                        var methodSymbol = classModel.GetDeclaredSymbol(methodSyntax);
                        if (methodSymbol != null
                            && methodSymbol.Parameters.Count() == 1
                            && methodSymbol.Parameters[0].Type.Equals(iServiceCollectionSymbol, SymbolEqualityComparer.Default))
                        {
                            if (_configureMethodSymbol != null)
                            {
                                throw new NotSupportedException(
                                    "Multiple LambdaStartup classes are not allowed in a Lambda project.");
                            }

                            _configureMethodSymbol = methodSymbol;
                            break;
                        }
                    }
                }

                return _configureMethodSymbol;
            }
        }

        /// <summary>
        /// Returns whether the simplified Lambda function returns void or <see cref="System.Threading.Tasks.Task"/>.
        /// </summary>
        public bool LambdaFunctionReturnsVoidOrTask
        {
            get
            {
                if (LambdaFunctionMethodSymbol.ReturnsVoid)
                {
                    return true;
                }

                var taskSymbol = _context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
                if (LambdaFunctionMethodSymbol.ReturnType.Equals(taskSymbol, SymbolEqualityComparer.Default))
                {
                    return true;
                }

                return false;
            }
        }

        public LambdaFunctionCodeGenerator(
            MethodDeclarationSyntax lambdaFunctionMethodSyntax,
            ClassDeclarationSyntax startupSyntax,
            GeneratorExecutionContext context)
        {
            _lambdaFunctionMethodSyntax = lambdaFunctionMethodSyntax;
            _startupSyntax = startupSyntax;
            _context = context;
        }

        /// <summary>
        /// Generates <see cref="SourceText"/> for the LambdaFunction including setting up the dependency injection.
        /// </summary>
        /// <returns>A <see cref="Tuple"/> containing source file name hint and source text.</returns>
        public (string, SourceText) GenerateSource()
        {
            var apiGatewayResponseSymbol =
                _context.Compilation.GetTypeByMetadataName("Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse");

            var returnTypeString = "APIGatewayProxyResponse";
            if (LambdaFunctionMethodSymbol.IsAsync)
            {
                returnTypeString = $"System.Threading.Tasks.Task<{returnTypeString}>";
            }

            var source = new StringBuilder(
                $@"using System;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace {LambdaFunctionMethodSymbol.ContainingNamespace}
{{
    public class {ClassName} 
    {{
        private readonly ServiceProvider serviceProvider;

        public {ClassName}()
        {{
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the scoped lifetime
            // because web dependencies are normally scoped to the client request. To use a different lifetime,
            // specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddScoped<{LambdaFunctionMethodSymbol.ContainingType}>();");

            // Call Configure method if LambdaStartup is provided
            if (ConfigureMethodSymbol != null)
            {
                source.Append($@"
            var startup = new {ConfigureMethodSymbol.ContainingType}();
            startup.{ConfigureMethodSymbol.Name}(services);");
            }

            source.Append($@"
            serviceProvider = services.BuildServiceProvider();
        }}

        public{(LambdaFunctionMethodSymbol.IsAsync ? " async " : " ")}{returnTypeString} {LambdaFunctionMethodSymbol.Name}(APIGatewayProxyRequest request, ILambdaContext _context)
        {{
            // Create a scope for every request, 
            // this allows creating scoped dependencies without creating a scope manually. 
            using var scope = serviceProvider.CreateScope();

            var {LambdaFunctionMethodSymbol.ContainingType.Name.ToCamelCase()} = scope.ServiceProvider.GetRequiredService<{LambdaFunctionMethodSymbol.ContainingType}>();");

            // TODO: implement input parameter parsing

            if (LambdaFunctionReturnsVoidOrTask)
            {
                // If Lambda function doesn't return, call the method without the return value.
                if (LambdaFunctionMethodSymbol.IsAsync)
                {
                    source.Append($@"
            await {LambdaFunctionMethodSymbol.ContainingType.Name.ToCamelCase()}.{LambdaFunctionMethodSymbol.Name}();");
                }
                else
                {
                    source.Append($@"
            {LambdaFunctionMethodSymbol.ContainingType.Name.ToCamelCase()}.{LambdaFunctionMethodSymbol.Name}();");
                }
            }
            else
            {
                // Lambda function returns a value, therefore, call the method with response.
                if (LambdaFunctionMethodSymbol.IsAsync)
                {
                    source.Append($@"
            var response = await {LambdaFunctionMethodSymbol.ContainingType.Name.ToCamelCase()}.{LambdaFunctionMethodSymbol.Name}();");
                }
                else
                {
                    source.Append($@"
            var response = {LambdaFunctionMethodSymbol.ContainingType.Name.ToCamelCase()}.{LambdaFunctionMethodSymbol.Name}();");
                }

                // Serialize the response to a string because APIGatewayProxyResponse body only allows string type.
                if (ReturnType.IsValueType)
                {
                    source.AppendLine($@"
            var body = response.ToString();"
                    );
                }
                else
                {
                    // TODO: Lambda function must use the configured serializer than System.Text.Json
                    source.AppendLine($@"
            var body = System.Text.Json.JsonSerializer.Serialize(response);");
                }
            }

            // Lambda function can have APIGatewayProxyResponse return type
            // In this case, there is no need to transform the response
            // Generated method must return the same response back to client
            if (ReturnType.Equals(apiGatewayResponseSymbol, SymbolEqualityComparer.Default))
            {
                source.Append($@"
            return response;");
            }
            else
            {
                source.Append($@"
            return new APIGatewayProxyResponse
            {{
                StatusCode = 200,");

                if (!LambdaFunctionReturnsVoidOrTask)
                {
                    source.Append($@"
                Body = body,
                Headers = new Dictionary<string, string> 
                {{
                    {{ ""Content-Type"", ""text/plain"" }}
                }}");
                }

                source.Append($@"
            }};");
            }

            source.Append($@"
        }}
    }}
}}"
            );

            return ($"{ClassName}.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }
    }
}