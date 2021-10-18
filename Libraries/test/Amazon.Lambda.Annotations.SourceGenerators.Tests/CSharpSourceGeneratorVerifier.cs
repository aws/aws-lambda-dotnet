using System;
using System.Collections.Immutable;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.Extensions.DependencyInjection;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    /// <summary>
    /// Source: https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md
    /// </summary>
    public static class CSharpSourceGeneratorVerifier<TSourceGenerator>
        where TSourceGenerator : ISourceGenerator, new()
    {
        public class Test : CSharpSourceGeneratorTest<TSourceGenerator, XUnitVerifier>
        {
            public Test()
            {
                this.SolutionTransforms.Add((solution, projectId) =>
                {
                    return solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(ILambdaContext).Assembly.Location))
                        .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(APIGatewayProxyRequest).Assembly.Location))
                        .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location))
                        .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location))
                        .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(RestApiAttribute).Assembly.Location))
                        .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(DefaultLambdaJsonSerializer).Assembly.Location));
                });
            }

            protected override CompilationOptions CreateCompilationOptions()
            {
                var compilationOptions = base.CreateCompilationOptions();
                return compilationOptions.WithSpecificDiagnosticOptions(
                    compilationOptions.SpecificDiagnosticOptions.SetItems(GetNullableWarningsFromCompiler()));
            }

            public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Default;

            private static ImmutableDictionary<string, ReportDiagnostic> GetNullableWarningsFromCompiler()
            {
                string[] args = { "/warnaserror:nullable" };
                var commandLineArguments = CSharpCommandLineParser.Default.Parse(args, baseDirectory: Environment.CurrentDirectory, sdkDirectory: Environment.CurrentDirectory);
                var nullableWarnings = commandLineArguments.CompilationOptions.SpecificDiagnosticOptions;

                return nullableWarnings;
            }

            protected override ParseOptions CreateParseOptions()
            {
                return ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);
            }
        }
    }
}