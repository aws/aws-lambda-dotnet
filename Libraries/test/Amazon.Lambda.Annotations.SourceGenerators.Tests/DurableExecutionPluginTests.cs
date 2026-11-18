// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using VerifyCS = Amazon.Lambda.Annotations.SourceGenerators.Tests.CSharpSourceGeneratorVerifier<Amazon.Lambda.Annotations.SourceGenerator.Generator>;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    /// <summary>
    /// <c>[DurableExecution(Tracing = ..., PluginProvider = ...)]</c>: what the generator emits for them, and the
    /// diagnostics that stop a mistake from becoming a compile error in generated code.
    /// </summary>
    /// <remarks>
    /// The real durable packages cannot be referenced here (their AWSSDK.Core 4.x conflicts with the 3.7.x pin the
    /// generator-test framework needs), so the types the generator resolves by metadata name, and the
    /// <c>WrapAsync</c> overloads the wrapper binds to, are supplied as source stubs, as in the other durable tests.
    /// </remarks>
    [Collection(TestServerlessAppCollection.Name)]
    public class DurableExecutionPluginTests : IDisposable
    {
        private const string PluginApiStubs = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution.Instrumentation
{
    public interface IDurableInstrumentationPluginFactory { }
}

namespace Amazon.Lambda.DurableExecution
{
    public interface IDurableContext { }
    public sealed class DurableExecutionInvocationInput { }
    public sealed class DurableExecutionInvocationOutput { }

    public sealed class DurableExecutionOptions
    {
        public IList<Amazon.Lambda.DurableExecution.Instrumentation.IDurableInstrumentationPluginFactory> Plugins { get; } =
            new List<Amazon.Lambda.DurableExecution.Instrumentation.IDurableInstrumentationPluginFactory>();
    }

    public static class DurableFunction
    {
        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
            Func<TInput, IDurableContext, Task<TOutput>> workflow, DurableExecutionInvocationInput invocationInput, ILambdaContext lambdaContext)
            => Task.FromResult(new DurableExecutionInvocationOutput());

        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
            Func<TInput, IDurableContext, Task<TOutput>> workflow, DurableExecutionInvocationInput invocationInput, ILambdaContext lambdaContext,
            DurableExecutionOptions options)
            => Task.FromResult(new DurableExecutionInvocationOutput());

        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
            Func<TInput, IDurableContext, Task> workflow, DurableExecutionInvocationInput invocationInput, ILambdaContext lambdaContext)
            => Task.FromResult(new DurableExecutionInvocationOutput());

        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
            Func<TInput, IDurableContext, Task> workflow, DurableExecutionInvocationInput invocationInput, ILambdaContext lambdaContext,
            DurableExecutionOptions options)
            => Task.FromResult(new DurableExecutionInvocationOutput());
    }
}
";

        private const string OtelStubs = @"
namespace Amazon.Lambda.DurableExecution.OpenTelemetry
{
    public enum OtelView { Execution, Invocation }

    public static class OtelPlugin
    {
        public static Amazon.Lambda.DurableExecution.Instrumentation.IDurableInstrumentationPluginFactory CreateFactory(OtelView view = OtelView.Execution) => null;
    }
}
";

        // The package from before the plugin API: only the envelope types and the original WrapAsync.
        private const string OldDurableStubs = @"
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
            Func<TInput, IDurableContext, Task<TOutput>> workflow, DurableExecutionInvocationInput invocationInput, ILambdaContext lambdaContext)
            => Task.FromResult(new DurableExecutionInvocationOutput());
    }
}
";

        private const string AssemblyAttributes =
            "[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]\n";

        private static async Task<string> AnnotationsSource(string fileName) =>
            await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", fileName));

        private static async Task<VerifyCS.Test> NewTestAsync(string userSource, params string[] stubs)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.DynamicallyLinkedLibrary,
                    Sources =
                    {
                        ("Workflow.cs", userSource),
                        ("AssemblyAttributes.cs", AssemblyAttributes),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await AnnotationsSource("LambdaFunctionAttribute.cs")),
                        (Path.Combine("Amazon.Lambda.Annotations", "DurableExecutionAttribute.cs"), await AnnotationsSource("DurableExecutionAttribute.cs")),
                    },
                },
            };
            for (var i = 0; i < stubs.Length; i++)
                test.TestState.Sources.Add(($"Stubs{i}.cs", stubs[i]));

            // The generator reports an Info (AWSLambda0103) for each file it writes; these tests are about the
            // wrapper and the diagnostics. Generated-source comparison is off unless a test supplies snapshots.
            test.DisabledDiagnostics.Add("AWSLambda0103");
            test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            return test;
        }

        private static string Workflow(string attributeArguments, string members = "") => $@"using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace MyApp
{{
    public class Workflows
    {{
{members}        [LambdaFunction]
        [DurableExecution(300{attributeArguments})]
        public Task<string> Run(string input, IDurableContext ctx) => Task.FromResult(input);
    }}
}}";

        // The location the generator reports: from the [LambdaFunction] attribute to the end of the method.
        private static DiagnosticResult At(string id, DiagnosticSeverity severity, string source)
        {
            var lines = source.Replace("\r\n", "\n").Split('\n');
            var first = Array.FindIndex(lines, l => l.Contains("[LambdaFunction]"));
            var last = Array.FindIndex(lines, first, l => l.TrimStart().StartsWith("public Task<string> Run"));
            return new DiagnosticResult(id, severity)
                .WithSpan("Workflow.cs", first + 1, lines[first].IndexOf('[') + 1, last + 1, lines[last].Length + 1);
        }

        // ── What is generated ───────────────────────────────────────────────

        [Fact]
        public async Task VerifyDurablePlugins()
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.DynamicallyLinkedLibrary,
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "DurableExecutionExamples", "DurablePluginExamples.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "DurableExecutionExamples", "DurablePluginExamples.cs.txt"))),
                        ("PluginApiStubs.cs", PluginApiStubs),
                        ("OtelStubs.cs", OtelStubs),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await AnnotationsSource("LambdaFunctionAttribute.cs")),
                        (Path.Combine("Amazon.Lambda.Annotations", "DurableExecutionAttribute.cs"), await AnnotationsSource("DurableExecutionAttribute.cs")),
                        ("AssemblyAttributes.cs", AssemblyAttributes),
                    },
                },
            };

            foreach (var name in new[] { "TracedDurableExecution", "ProviderDurableExecution", "TracedProviderDurableExecution" })
            {
                var expected = await ReadSnapshotContent(Path.Combine("Snapshots", "DurableExecution", $"{name}_ProcessOrder_Generated.g.cs"));
                test.TestState.GeneratedSources.Add((
                    typeof(SourceGenerator.Generator),
                    $"{name}_ProcessOrder_Generated.g.cs",
                    SourceText.From(expected, Encoding.UTF8, SourceHashAlgorithm.Sha256)));
            }

            test.DisabledDiagnostics.Add("AWSLambda0103");
            // The generated code is compiled against the stubs and must bind, including the WrapAsync overload that
            // takes the options. Anything but the harness's own bookkeeping is a failure.
            await test.RunAsync();
        }

        // ── Diagnostics: the attribute ──────────────────────────────────────

        [Fact]
        public async Task TracingNone_AndNoProvider_ReportsNothing()
        {
            var source = Workflow(", Tracing = DurableTracing.None");
            var test = await NewTestAsync(source, OldDurableStubs);

            await test.RunAsync();
        }

        [Fact]
        public async Task EmptyPluginProvider_IsAnInvalidAttribute()
        {
            var source = Workflow(", PluginProvider = \"\"");
            var test = await NewTestAsync(source, PluginApiStubs);
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0144", DiagnosticSeverity.Error, source)
                .WithArguments("PluginProvider is empty. Name a static method, for example nameof(GetPlugins), or leave it unset."));

            await test.RunAsync();
        }

        [Fact]
        public async Task UndefinedTracingValue_IsAnInvalidAttribute()
        {
            var source = Workflow(", Tracing = (DurableTracing)7");
            var test = await NewTestAsync(source, PluginApiStubs, OtelStubs);
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0144", DiagnosticSeverity.Error, source)
                .WithArguments("Tracing = 7. It must be one of None, Execution, Invocation."));

            await test.RunAsync();
        }

        // ── Diagnostics: Tracing ────────────────────────────────────────────

        [Fact]
        public async Task Tracing_WithoutTheOpenTelemetryPackage_ReportsError()
        {
            var source = Workflow(", Tracing = DurableTracing.Invocation");
            var test = await NewTestAsync(source, PluginApiStubs);
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0147", DiagnosticSeverity.Error, source).WithArguments("Invocation"));

            await test.RunAsync();
        }

        [Fact]
        public async Task Tracing_WithAnOlderDurablePackage_ReportsThePluginApiIsMissing()
        {
            var source = Workflow(", Tracing = DurableTracing.Execution");
            var test = await NewTestAsync(source, OldDurableStubs, OtelStubs);
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0146", DiagnosticSeverity.Error, source)
                .WithArguments("Tracing", "Amazon.Lambda.DurableExecution.DurableExecutionOptions"));

            await test.RunAsync();
        }

        [Fact]
        public async Task Tracing_WithThePackages_ReportsNothing()
        {
            var source = Workflow(", Tracing = DurableTracing.Execution");
            var test = await NewTestAsync(source, PluginApiStubs, OtelStubs);

            await test.RunAsync();
        }

        // ── Diagnostics: PluginProvider ─────────────────────────────────────

        private const string Factories = "IEnumerable<IDurableInstrumentationPluginFactory>";

        [Theory]
        [InlineData("internal static " + Factories + " GetPlugins() => null;")]
        [InlineData("public static " + Factories + " GetPlugins() => null;")]
        [InlineData("protected internal static " + Factories + " GetPlugins() => null;")]
        [InlineData("internal static IDurableInstrumentationPluginFactory[] GetPlugins() => null;")]
        [InlineData("internal static List<IDurableInstrumentationPluginFactory> GetPlugins() => null;")]
        [InlineData("internal static IEnumerable<MyFactory> GetPlugins() => null;")]
        public async Task PluginProvider_ThatCanBeCalled_ReportsNothing(string method)
        {
            var source = Workflow(", PluginProvider = nameof(GetPlugins)", "        class MyFactory : IDurableInstrumentationPluginFactory { }\n        " + method + "\n\n");
            var test = await NewTestAsync(source, PluginApiStubs);

            await test.RunAsync();
        }

        [Fact]
        public async Task PluginProvider_WithNoSuchMethod_ReportsError()
        {
            var source = Workflow(", PluginProvider = \"Missing\"");
            var test = await NewTestAsync(source, PluginApiStubs);
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0148", DiagnosticSeverity.Error, source)
                .WithArguments("Missing", "'MyApp.Workflows' has no method with that name."));

            await test.RunAsync();
        }

        [Theory]
        [InlineData("internal " + Factories + " GetPlugins() => null;", "it must be static.")]
        [InlineData("internal static " + Factories + " GetPlugins(int count) => null;", "it must take no parameters and have no type parameters.")]
        [InlineData("internal static " + Factories + " GetPlugins<T>() => null;", "it must take no parameters and have no type parameters.")]
        [InlineData("private static " + Factories + " GetPlugins() => null;", "it is private, but the generated code must be able to call it. Make it internal or public.")]
        [InlineData("protected static " + Factories + " GetPlugins() => null;", "it is protected, but the generated code must be able to call it. Make it internal or public.")]
        [InlineData("internal static string GetPlugins() => null;", "it must return IEnumerable<Amazon.Lambda.DurableExecution.Instrumentation.IDurableInstrumentationPluginFactory> but returns 'string'.")]
        [InlineData("internal static IEnumerable<string> GetPlugins() => null;", "it must return IEnumerable<Amazon.Lambda.DurableExecution.Instrumentation.IDurableInstrumentationPluginFactory> but returns 'System.Collections.Generic.IEnumerable<string>'.")]
        public async Task PluginProvider_ThatCannotBeUsed_ReportsWhy(string method, string reason)
        {
            var source = Workflow(", PluginProvider = nameof(GetPlugins)", "        " + method + "\n\n");
            var test = await NewTestAsync(source, PluginApiStubs);
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0148", DiagnosticSeverity.Error, source).WithArguments("GetPlugins", reason));

            await test.RunAsync();
        }

        [Fact]
        public async Task PluginProvider_WithAnOverloadThatWorks_IsAccepted()
        {
            var source = Workflow(", PluginProvider = nameof(GetPlugins)",
                "        internal static " + Factories + " GetPlugins(int count) => null;\n" +
                "        internal static " + Factories + " GetPlugins() => null;\n\n");
            var test = await NewTestAsync(source, PluginApiStubs);

            await test.RunAsync();
        }

        [Fact]
        public async Task PluginProvider_WithAnOlderDurablePackage_ReportsThePluginApiIsMissing()
        {
            var source = Workflow(", PluginProvider = nameof(GetPlugins)", "        internal static string GetPlugins() => null;\n\n");
            var test = await NewTestAsync(source, OldDurableStubs);
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0146", DiagnosticSeverity.Error, source)
                .WithArguments("PluginProvider", "Amazon.Lambda.DurableExecution.DurableExecutionOptions"));

            await test.RunAsync();
        }

        [Fact]
        public async Task TracingAndAProviderThatIsMissing_ReportBothProblems()
        {
            var source = Workflow(", Tracing = DurableTracing.Execution, PluginProvider = \"Missing\"");
            var test = await NewTestAsync(source, PluginApiStubs);
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0147", DiagnosticSeverity.Error, source).WithArguments("Execution"));
            test.TestState.ExpectedDiagnostics.Add(At("AWSLambda0148", DiagnosticSeverity.Error, source)
                .WithArguments("Missing", "'MyApp.Workflows' has no method with that name."));

            await test.RunAsync();
        }

        public void Dispose()
        {
            var template = Path.Combine("TestServerlessApp", "serverless.template");
            if (File.Exists(template))
                File.Delete(template);
        }

        private static async Task<string> ReadSnapshotContent(string snapshotPath)
        {
            var content = await File.ReadAllTextAsync(snapshotPath);
            return content.Trim().ToEnvironmentLineEndings().ApplyReplacements();
        }
    }
}
