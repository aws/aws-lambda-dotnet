using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerators
{
    [Generator]
    internal class SourceGenerator : ISourceGenerator
    {
        public SourceGenerator()
        {
#if DEBUG
            // if (!Debugger.IsAttached)
            // {
            //     Debugger.Launch();
            // }
#endif
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // retrieve the populated receiver 
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            {
                return;
            }

            foreach (var lambdaFunction in receiver.LambdaFunctions)
            {
                var codeGenerator = new LambdaFunctionCodeGenerator(lambdaFunction, receiver.StartupClass, context);
                var (hint, sourceText) =codeGenerator.GenerateSource();
                context.AddSource(hint, sourceText);
            }
        }

        private (string, SourceText) ProcessLambdaFunction(MethodDeclarationSyntax methodSyntax, ClassDeclarationSyntax startupSyntax, GeneratorExecutionContext context)
        {
            var methodModel = context.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
            var methodSymbol = methodModel.GetDeclaredSymbol(methodSyntax);
            var className = $"{methodSymbol.ContainingType.Name}_{methodSymbol.Name}_Generated";
            var returnType = methodSymbol.ReturnType as INamedTypeSymbol;
            var isTaskReturnType = returnType.IsGenericType && returnType.BaseType.ToString() == typeof(System.Threading.Tasks.Task).FullName;


            var classModel = context.Compilation.GetSemanticModel(startupSyntax.SyntaxTree);
            MethodDeclarationSyntax startupMethodSyntax = null;
            IMethodSymbol startupMethodSymbol = null;
            foreach (var member in startupSyntax.Members)
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

            var returnTypeString = "APIGatewayProxyResponse";
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
        private readonly {methodSymbol.ContainingType} {methodSymbol.ContainingType.Name.ToCamelCase()};

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
            var serviceProvider = services.BuildServiceProvider();

            {methodSymbol.ContainingType.Name.ToCamelCase()} = services.BuildServiceProvider().GetService<Functions>();
        }}

        public{(methodSymbol.IsAsync ? " async " : " ")}{returnTypeString} {methodSymbol.Name}(APIGatewayProxyRequest request, ILambdaContext context)
        {{"
            );

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
                source.Append($@"
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
                source.Append($@"
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

            source.Append($@"
        }}
    }}
}}"
            );

            return ($"{className}.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }
    }
}
