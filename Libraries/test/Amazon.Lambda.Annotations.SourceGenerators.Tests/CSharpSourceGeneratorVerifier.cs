using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Immutable;
using System.IO;

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
            public enum ReferencesMode {All, NoApiGatewayEvents}

            public enum TargetFramework { Net60, Net80 }

            public Test(ReferencesMode referencesMode = ReferencesMode.All, TargetFramework targetFramework = TargetFramework.Net60)
            {
                if(referencesMode == ReferencesMode.NoApiGatewayEvents)
                {
                    this.SolutionTransforms.Add((solution, projectId) =>
                    {
                        return solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(ILambdaContext).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(RestApiAttribute).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(DefaultLambdaJsonSerializer).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(LambdaBootstrapBuilder).Assembly.Location));
                    });

                }
                else
                {
                    this.SolutionTransforms.Add((solution, projectId) =>
                    {
                        return solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(ILambdaContext).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(APIGatewayProxyRequest).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(SQSEvent).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(RestApiAttribute).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(DefaultLambdaJsonSerializer).Assembly.Location))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(LambdaBootstrapBuilder).Assembly.Location));
                    });
                }

                // Set up the target framework moniker and reference assemblies 
                if (targetFramework == TargetFramework.Net60)
                {
                    SolutionTransforms.Add((solution, projectId) => 
                    {
                        return solution.AddAnalyzerConfigDocument(
                            DocumentId.CreateNewId(projectId), 
                            "TargetFrameworkConfig.editorconfig", 
                            SourceText.From("""
                                                is_global = true
                                                build_property.TargetFramework = net6.0
                                            """),
                            filePath: "/TargetFrameworkConfig.editorconfig");
                    });
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60;
                }
                else if (targetFramework == TargetFramework.Net80) 
                {
                    SolutionTransforms.Add((solution, projectId) =>
                    {
                        return solution.AddAnalyzerConfigDocument(
                            DocumentId.CreateNewId(projectId),
                            "TargetFrameworkConfig.editorconfig", 
                            SourceText.From("""
                                                is_global = true
                                                build_property.TargetFramework = net8.0
                                            """),
                            filePath: "/TargetFrameworkConfig.editorconfig");
                    });
                    // There isn't a static .NET 8 yet
                    ReferenceAssemblies = new ReferenceAssemblies("net8.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "8.0.0"), Path.Combine("ref", "net8.0"));
                }
            }

            protected override CompilationOptions CreateCompilationOptions()
            {
                var compilationOptions = base.CreateCompilationOptions();

                return compilationOptions
                    .WithSpecificDiagnosticOptions(compilationOptions.SpecificDiagnosticOptions.SetItems(GetNullableWarningsFromCompiler()))
                    .WithSpecificDiagnosticOptions(compilationOptions.SpecificDiagnosticOptions.SetItems(EnableNullability()));
            }

            public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Default;

            private static ImmutableDictionary<string, ReportDiagnostic> GetNullableWarningsFromCompiler()
            {
                string[] args = { "/warnaserror:nullable" };
                var commandLineArguments = CSharpCommandLineParser.Default.Parse(args, baseDirectory: Environment.CurrentDirectory, sdkDirectory: Environment.CurrentDirectory);
                var nullableWarnings = commandLineArguments.CompilationOptions.SpecificDiagnosticOptions;

                return nullableWarnings;
            }

            private static ImmutableDictionary<string, ReportDiagnostic> EnableNullability()
            {
                string[] args = { "/p:Nullable=enable" };
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