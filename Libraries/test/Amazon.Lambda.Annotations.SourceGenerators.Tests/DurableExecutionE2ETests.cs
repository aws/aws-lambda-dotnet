// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using VerifyCS = Amazon.Lambda.Annotations.SourceGenerators.Tests.CSharpSourceGeneratorVerifier<Amazon.Lambda.Annotations.SourceGenerator.Generator>;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    /// <summary>
    /// End-to-end snapshot coverage for the <c>[DurableExecution]</c> path — the layer every other
    /// annotation attribute has (e.g. <c>VerifyValidScheduleEvents</c>, <c>VerifyValidSQSEvents</c>) but
    /// durable previously lacked. It runs the real <see cref="SourceGenerator.Generator"/> against a
    /// fixture annotated with <c>[LambdaFunction]</c> + <c>[DurableExecution]</c> and asserts, byte-for-byte,
    /// BOTH the generated handler wrapper AND the executable <c>Program.g.cs</c> AND the emitted
    /// <c>serverless.template</c> (the durable <c>DurableConfig</c> block plus the mixed string/object
    /// checkpoint-IAM <c>Policies</c> array). The per-slice unit tests (wrapper text, CFN writer,
    /// diagnostics) each exercise a hand-built model in isolation; this is the only test that drives the
    /// whole generator pipeline for durable, catching branch-ordering or wrapper/template drift the slices
    /// cannot.
    /// </summary>
    /// <remarks>
    /// Durable requires the executable model, so the compilation is a <see cref="OutputKind.ConsoleApplication"/>
    /// and the generator emits <c>Program.g.cs</c>. The <c>Amazon.Lambda.DurableExecution</c> package cannot be
    /// referenced here (its AWSSDK.Core 4.x conflicts with the 3.7.x pin the generator-test framework requires),
    /// so the durable SDK types the generator resolves by metadata name — and the <c>DurableFunction.WrapAsync</c>
    /// the generated wrapper binds to — are supplied as source stubs, the same technique
    /// <see cref="DurableExecutionDiagnosticsTests"/> uses. <see cref="CompilerDiagnostics.None"/> keeps the test
    /// focused on the generator's output rather than the full RuntimeSupport compilation (which the executable
    /// E2E tests must otherwise pin line-by-line).
    /// </remarks>
    [Collection(TestServerlessAppCollection.Name)]
    public class DurableExecutionE2ETests : IDisposable
    {
        // Minimal durable SDK stubs: the three envelope types the generator resolves by metadata name, plus the
        // DurableFunction.WrapAsync overloads the generated wrapper delegates to (so the wrapper binds).
        private const string DurableStubs = @"
using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution
{
    public interface IDurableContext { }
    public sealed class DurableExecutionInvocationInput { }
    public sealed class DurableExecutionInvocationOutput { }

    public static class DurableFunction
    {
        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
            Func<TInput, IDurableContext, Task<TOutput>> workflow,
            DurableExecutionInvocationInput invocationInput,
            ILambdaContext lambdaContext) => Task.FromResult(new DurableExecutionInvocationOutput());

        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
            Func<TInput, IDurableContext, Task> workflow,
            DurableExecutionInvocationInput invocationInput,
            ILambdaContext lambdaContext) => Task.FromResult(new DurableExecutionInvocationOutput());
    }
}
";

        // GenerateMain=true so the generator emits Program.g.cs (the executable model).
        private const string ExecutableAssemblyAttributes =
            "using Amazon.Lambda.Annotations;\n" +
            "using Amazon.Lambda.Core;\n" +
            "[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]\n" +
            "[assembly: LambdaGlobalProperties(GenerateMain = true)]\n";

        // No GenerateMain: the class-library model. The managed runtime hosts its own bootstrap and resolves
        // the serializer from this assembly attribute, so no Program.g.cs is generated.
        private const string ClassLibraryAssemblyAttributes =
            "using Amazon.Lambda.Core;\n" +
            "[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]\n";

        [Fact]
        public async Task VerifyValidDurableExecution()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "durableExecution.template"));
            var expectedWrapperContent = await ReadSnapshotContent(Path.Combine("Snapshots", "DurableExecution", "ValidDurableExecution_ProcessOrder_Generated.g.cs"));
            var expectedProgramContent = await ReadSnapshotContent(Path.Combine("Snapshots", "DurableExecution", "ProgramDurableExecution.g.cs"));

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "DurableExecutionExamples", "ValidDurableExecution.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "DurableExecutionExamples", "ValidDurableExecution.cs.txt"))),
                        ("DurableStubs.cs", DurableStubs),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "DurableExecutionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "DurableExecutionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"))),
                        ("AssemblyAttributes.cs", ExecutableAssemblyAttributes),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "ValidDurableExecution_ProcessOrder_Generated.g.cs",
                            SourceText.From(expectedWrapperContent, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Program.g.cs",
                            SourceText.From(expectedProgramContent, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        // The generator reports AWSLambda0103 for each function wrapper and the template, but
                        // NOT for Program.g.cs (it is AddSource'd without a diagnostic). Program.g.cs is still
                        // verified byte-for-byte via GeneratedSources above.
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info)
                            .WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info)
                            .WithArguments("ValidDurableExecution_ProcessOrder_Generated.g.cs", expectedWrapperContent),
                    }
                },
                // Focus on generator output, not the full RuntimeSupport/Program.g.cs compilation (the durable
                // package isn't referenced here, so the bootstrap call cannot fully bind in-test).
                CompilerDiagnostics = CompilerDiagnostics.None,
            };

            // The template content is verified byte-for-byte via the AWSLambda0103 diagnostic above (in-memory).
            // We deliberately do NOT also read TestServerlessApp/serverless.template off disk: that file is shared
            // with the SourceGeneratorTests fixtures, and this test class runs in parallel with them, so a disk
            // read would race. The diagnostic assertion is the race-free source of truth.
            await test.RunAsync();
        }

        [Fact]
        public async Task VerifyValidDurableExecution_ClassLibrary()
        {
            // The class-library model (no GenerateMain, OutputKind = DynamicallyLinkedLibrary). Durable works
            // here too: the managed runtime resolves [assembly: LambdaSerializer] and populates
            // ILambdaContext.Serializer, which DurableFunction.WrapAsync reads. The generator emits the SAME
            // wrapper (reused snapshot) but NO Program.g.cs, and a template whose Handler is the
            // Assembly::Type::Method form with no ANNOTATIONS_HANDLER env var.
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "durableExecutionClassLibrary.template"));
            var expectedWrapperContent = await ReadSnapshotContent(Path.Combine("Snapshots", "DurableExecution", "ValidDurableExecution_ProcessOrder_Generated.g.cs"));

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.DynamicallyLinkedLibrary,
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "DurableExecutionExamples", "ValidDurableExecution.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "DurableExecutionExamples", "ValidDurableExecution.cs.txt"))),
                        ("DurableStubs.cs", DurableStubs),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "DurableExecutionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "DurableExecutionAttribute.cs"))),
                        ("AssemblyAttributes.cs", ClassLibraryAssemblyAttributes),
                    },
                    GeneratedSources =
                    {
                        // Same wrapper as the executable model — only the deployed Handler string differs.
                        // No Program.g.cs is generated for a class library.
                        (
                            typeof(SourceGenerator.Generator),
                            "ValidDurableExecution_ProcessOrder_Generated.g.cs",
                            SourceText.From(expectedWrapperContent, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info)
                            .WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info)
                            .WithArguments("ValidDurableExecution_ProcessOrder_Generated.g.cs", expectedWrapperContent),
                    }
                },
                CompilerDiagnostics = CompilerDiagnostics.None,
            };

            // Template verified via the AWSLambda0103 diagnostic (in-memory); no racy on-disk read (see the
            // executable test above for why).
            await test.RunAsync();
        }

        public void Dispose()
        {
            var template = Path.Combine("TestServerlessApp", "serverless.template");
            if (File.Exists(template))
                File.Delete(template);
        }

        // Mirror SourceGeneratorTests.ReadSnapshotContent: trim the trailing newline some editors add,
        // normalize to the environment's line endings, then substitute the assembly-version placeholder.
        private static async Task<string> ReadSnapshotContent(string snapshotPath)
        {
            var content = await File.ReadAllTextAsync(snapshotPath);
            return content.Trim().ToEnvironmentLineEndings().ApplyReplacements();
        }
    }
}
