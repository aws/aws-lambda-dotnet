// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Amazon.Lambda.Annotations.SourceGenerators.Tests.CSharpSourceGeneratorVerifier<Amazon.Lambda.Annotations.SourceGenerator.Generator>;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    /// <summary>
    /// Tests for AWSLambda0145: when a [DurableExecution] function registers the source-generator
    /// serializer (SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;), the durable invocation envelope
    /// types must be registered on TContext with [JsonSerializable], otherwise serialization fails at
    /// invocation time.
    /// </summary>
    public class DurableExecutionSerializerContextDiagnosticsTests
    {
        // Minimal durable SDK stubs. The real Amazon.Lambda.DurableExecution package cannot be referenced
        // by this test project (its AWSSDK.Core 4.x conflicts with the 3.7.x pin required by the generator
        // test framework), so the types the generator resolves by metadata name are supplied as source.
        private const string DurableStubs = @"
namespace Amazon.Lambda.DurableExecution
{
    public interface IDurableContext { }
    public sealed class DurableExecutionInvocationInput { }
    public sealed class DurableExecutionInvocationOutput { }
}
";

        private static async Task<string> AnnotationsSource(string fileName) =>
            await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", fileName));

        // The user source registers the serializer itself (via [assembly: LambdaSerializer]) so the default
        // DefaultLambdaJsonSerializer registration is intentionally not added here.
        private static async Task<VerifyCS.Test> NewTestAsync(string userSource)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        ("Workflow.cs", userSource),
                        ("DurableStubs.cs", DurableStubs),
                    },
                }
            };
            test.TestState.Sources.Add((Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await AnnotationsSource("LambdaFunctionAttribute.cs")));
            test.TestState.Sources.Add((Path.Combine("Amazon.Lambda.Annotations", "DurableExecutionAttribute.cs"), await AnnotationsSource("DurableExecutionAttribute.cs")));

            // Generation proceeds (Warning severity does not halt it); ignore the per-file codegen Info
            // (AWSLambda0103), the generated-source list, and compiler errors from the deliberately
            // unimplemented JsonSerializerContext (only the generator's symbol resolution matters here).
            test.DisabledDiagnostics.Add("AWSLambda0103");
            test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            return test;
        }

        // Builds a workflow that registers SourceGeneratorLambdaJsonSerializer<MyContext>, where MyContext
        // registers whichever envelope types are passed in via extraJsonSerializable.
        private static string WorkflowSource(string extraJsonSerializable) => $@"
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<MyApp.MyContext>))]

namespace MyApp
{{
{extraJsonSerializable}
    public partial class MyContext : JsonSerializerContext {{ }}

    public class Workflows
    {{
        [LambdaFunction]
        [DurableExecution(executionTimeout: 300)]
        public Task<string> Run(string input, IDurableContext ctx) => Task.FromResult(input);
    }}
}}";

        [Fact]
        public async Task BothEnvelopeTypesRegistered_NoDiagnostic()
        {
            var source = WorkflowSource(
                "    [JsonSerializable(typeof(DurableExecutionInvocationInput))]\n" +
                "    [JsonSerializable(typeof(DurableExecutionInvocationOutput))]");
            var test = await NewTestAsync(source);
            // No AWSLambda0145 expected.
            await test.RunAsync();
        }

        [Fact]
        public async Task OutputEnvelopeTypeMissing_ReportsWarning()
        {
            var source = WorkflowSource(
                "    [JsonSerializable(typeof(DurableExecutionInvocationInput))]");
            var test = await NewTestAsync(source);
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0145", DiagnosticSeverity.Warning)
                    .WithSpan("Workflow.cs", 18, 9, 20, 94)
                    .WithArguments("MyApp.MyContext", "Amazon.Lambda.DurableExecution.DurableExecutionInvocationOutput"));
            await test.RunAsync();
        }

        [Fact]
        public async Task BothEnvelopeTypesMissing_ReportsTwoWarnings()
        {
            var source = WorkflowSource(string.Empty);
            var test = await NewTestAsync(source);
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0145", DiagnosticSeverity.Warning)
                    .WithSpan("Workflow.cs", 18, 9, 20, 94)
                    .WithArguments("MyApp.MyContext", "Amazon.Lambda.DurableExecution.DurableExecutionInvocationInput"));
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0145", DiagnosticSeverity.Warning)
                    .WithSpan("Workflow.cs", 18, 9, 20, 94)
                    .WithArguments("MyApp.MyContext", "Amazon.Lambda.DurableExecution.DurableExecutionInvocationOutput"));
            await test.RunAsync();
        }

        [Fact]
        public async Task DefaultSerializer_NoDiagnostic()
        {
            // The reflection-based DefaultLambdaJsonSerializer needs no [JsonSerializable] registration,
            // so the durable envelope check does not apply and AWSLambda0145 is never emitted.
            var source = @"
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyApp
{
    public class Workflows
    {
        [LambdaFunction]
        [DurableExecution(executionTimeout: 300)]
        public Task<string> Run(string input, IDurableContext ctx) => Task.FromResult(input);
    }
}";
            var test = await NewTestAsync(source);
            await test.RunAsync();
        }
    }
}
