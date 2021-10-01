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
        private string _returnType;

        public LambdaFunctionCodeGenerator(
            MethodDeclarationSyntax lambdaFunctionMethodSyntax,
            ClassDeclarationSyntax startupSyntax,
            GeneratorExecutionContext context)
        {
            _lambdaFunctionMethodSyntax = lambdaFunctionMethodSyntax;
            _startupSyntax = startupSyntax;
            _context = context;
        }

        public (string, SourceText) GenerateSource()
        {
            var methodModel = _context.Compilation.GetSemanticModel(_lambdaFunctionMethodSyntax.SyntaxTree);
            var methodSymbol = methodModel.GetDeclaredSymbol(_lambdaFunctionMethodSyntax);
            if (methodSymbol == null)
            {
                throw new InvalidOperationException($"Symbol not found.");
            }
            var className = $"{methodSymbol.ContainingType.Name}_{methodSymbol.Name}_Generated";
            var returnType = methodSymbol.ReturnType as INamedTypeSymbol;

            var classModel = _context.Compilation.GetSemanticModel(_startupSyntax.SyntaxTree);
            MethodDeclarationSyntax startupMethodSyntax = null;
            IMethodSymbol startupMethodSymbol = null;
            foreach (var member in _startupSyntax.Members)
            {
                if (member.Kind() == SyntaxKind.MethodDeclaration)
                {
                    startupMethodSyntax = (MethodDeclarationSyntax)member;
                    startupMethodSymbol = classModel.GetDeclaredSymbol(startupMethodSyntax);
                    if (startupMethodSymbol != null && startupMethodSymbol.Parameters.Count() == 1 && startupMethodSymbol.Parameters[0].Type.ToString() == "Microsoft.Extensions.DependencyInjection.IServiceCollection" && startupMethodSymbol.DeclaredAccessibility == Accessibility.Public)
                    {
                        break;
                    }
                }
            }

            var returnTypeString = "";
            if (methodSymbol.ReturnsVoid)
            {
                returnTypeString = "APIGatewayProxyResponse";
            }
            else
            {
                returnTypeString = "APIGatewayProxyResponse";
            }

            if (methodSymbol.IsAsync)
            {
                returnTypeString = $"System.Threading.Tasks.Task<{returnTypeString}>";
            }

            var source = new StringBuilder(
$@"using System;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace {methodSymbol.ContainingNamespace}
{{
    public class {className} 
    {{
        private readonly ServiceProvider serviceProvider;

        public {className}()
        {{
            var services = new ServiceCollection();");
            if (startupMethodSymbol != null)
            {
                source.Append($@"
            var startup = new {startupMethodSymbol.ContainingType}();
            startup.{startupMethodSymbol.Name}(services);"
                );
            }

            source.Append($@"
            serviceProvider = services.BuildServiceProvider();
        }}

        public{(methodSymbol.IsAsync ? " async " : " ")}{returnTypeString} {methodSymbol.Name}(APIGatewayProxyRequest request, ILambdaContext _context)
        {{
            using var scope = serviceProvider.CreateScope();

            var {methodSymbol.ContainingType.Name.ToCamelCase()} = scope.ServiceProvider.GetRequiredService<{methodSymbol.ContainingType}>();"
            );

            if (methodSymbol.ReturnsVoid)
            {
                if (methodSymbol.IsAsync)
                {
                    source.Append($@"
            await {methodSymbol.ContainingType.Name.ToCamelCase()}.{methodSymbol.Name}();

            return new APIGatewayProxyResponse
            {{
                StatusCode = 200
            }};"
                    );
                }
                else
                {
                    source.Append($@"
            {methodSymbol.ContainingType.Name.ToCamelCase()}.{methodSymbol.Name}();

            return new APIGatewayProxyResponse
            {{
                StatusCode = 200
            }};"
                    );
                }
            }
            else
            {
                if (methodSymbol.IsAsync)
                {
                    source.Append($@"
            var response = await {methodSymbol.ContainingType.Name.ToCamelCase()}.{methodSymbol.Name}();"
                    );
                }
                else
                {
                    source.Append($@"
            var response = {methodSymbol.ContainingType.Name.ToCamelCase()}.{methodSymbol.Name}();"
                    );
                }

                if (returnType.ToString() == "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse")
                {
                    source.Append($@"
            return response;");
                }
                else if (returnType.IsValueType)
                {
                    source.AppendLine($@"
            return new APIGatewayProxyResponse
            {{
                StatusCode = 200,
                Body = response.ToString(),
                Headers = new Dictionary<string, string> 
                {{
                    {{ ""Content-Type"", ""text/plain"" }}
                }}
            }};"
                    );
                }
                else
                {
                    source.AppendLine($@"
            return new APIGatewayProxyResponse
            {{
                StatusCode = 200,
                Body = System.Text.Json.JsonSerializer.Serialize(response),
                Headers = new Dictionary<string, string> 
                {{
                    {{ ""Content-Type"", ""text/plain"" }}
                }}
            }};");
                }
            }


            source.Append($@"
        }}
    }}
}}"
            );

            return ($"{className}.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }
    }
}