using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.SQSEvents;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        public class Test : CSharpSourceGeneratorTest<TSourceGenerator, DefaultVerifier>
        {
            public enum ReferencesMode {All, NoApiGatewayEvents}

            public enum TargetFramework { Net8_0, Net10_0 }

            private ImmutableArray<string> PreprocessorSymbols { get; set; } = ImmutableArray<string>.Empty;

            public Test(ReferencesMode referencesMode = ReferencesMode.All, TargetFramework targetFramework = TargetFramework.Net10_0)
            {
                PreprocessorSymbols = ImmutableArray.Create<string>("ANALYZER_UNIT_TESTS");

                var assemblyResolver = (Type t) =>
                {
                    var path = t.Assembly.Location;
                    if (targetFramework == TargetFramework.Net8_0 && path.Contains("net10.0"))
                        path = path.Replace("net10.0", "net8.0");

                    return path;
                };

                if (referencesMode == ReferencesMode.NoApiGatewayEvents)
                {
                    SolutionTransforms.Add((solution, projectId) =>
                    {
                        return solution
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(ILambdaContext))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(IServiceCollection))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(ServiceProvider))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(RestApiAttribute))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(DefaultLambdaJsonSerializer))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(HostApplicationBuilder))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(IHost))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(SnapshotRestore.Registry.RestoreHooksRegistry))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(LambdaBootstrapBuilder))));
                    });

                }
                else
                {
                    SolutionTransforms.Add((solution, projectId) =>
                    {
                        return solution
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(ILambdaContext))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(APIGatewayProxyRequest))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(DynamoDBEvent))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(SNSEvent))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(SQSEvent))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(ScheduledEvent))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(ApplicationLoadBalancerRequest))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(IServiceCollection))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(ServiceProvider))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(RestApiAttribute))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(DefaultLambdaJsonSerializer))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(HostApplicationBuilder))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(IHost))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(SnapshotRestore.Registry.RestoreHooksRegistry))))
                            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyResolver(typeof(LambdaBootstrapBuilder))));
                    });
                }

                // Set up the target framework moniker and reference assemblies 
                if (targetFramework == TargetFramework.Net10_0)
                {
                    SolutionTransforms.Add((solution, projectId) => 
                    {
                        return solution.AddAnalyzerConfigDocument(
                            DocumentId.CreateNewId(projectId), 
                            "TargetFrameworkConfig.editorconfig", 
                            SourceText.From("""
                                                is_global = true
                                                build_property.TargetFramework = net10.0
                                            """),
                            filePath: "/TargetFrameworkConfig.editorconfig");
                    });
                    ReferenceAssemblies = new ReferenceAssemblies("net10.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "10.0.0"), Path.Combine("ref", "net10.0"));
                }
                else if (targetFramework == TargetFramework.Net8_0) 
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
                return ((CSharpParseOptions)base.CreateParseOptions())
                    .WithLanguageVersion(LanguageVersion)
                    .WithPreprocessorSymbols(PreprocessorSymbols);
            }
        }
    }
}
