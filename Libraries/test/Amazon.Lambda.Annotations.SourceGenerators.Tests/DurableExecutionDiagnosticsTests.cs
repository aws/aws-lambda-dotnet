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
    public class DurableExecutionDiagnosticsTests
    {
        // Minimal serializer registration so the generator does not also emit AWSLambda0108.
        private const string AssemblyAttributes =
            "[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]\n";

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

        private static VerifyCS.Test NewTest(string userSource, OutputKind outputKind)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = outputKind,
                    Sources =
                    {
                        ("Workflow.cs", userSource),
                        ("AssemblyAttributes.cs", AssemblyAttributes),
                        ("DurableStubs.cs", DurableStubs),
                    },
                }
            };
            return test;
        }

        private static async Task AddAnnotationSourcesAsync(VerifyCS.Test test)
        {
            test.TestState.Sources.Add((Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await AnnotationsSource("LambdaFunctionAttribute.cs")));
            test.TestState.Sources.Add((Path.Combine("Amazon.Lambda.Annotations", "DurableExecutionAttribute.cs"), await AnnotationsSource("DurableExecutionAttribute.cs")));
        }

        [Fact]
        public async Task InvalidSignature_WhenWrongSecondParameter_ReportsError()
        {
            var source = @"
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;

namespace MyApp
{
    public class Workflows
    {
        [LambdaFunction]
        [DurableExecution]
        public Task<string> Run(string input, ILambdaContext ctx) => Task.FromResult(input);
    }
}";
            var test = NewTest(source, OutputKind.ConsoleApplication);
            await AddAnnotationSourcesAsync(test);
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0142", DiagnosticSeverity.Error)
                    .WithSpan("Workflow.cs", 10, 9, 12, 93)
                    .WithArguments("The second parameter must be 'Amazon.Lambda.DurableExecution.IDurableContext'."));
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            await test.RunAsync();
        }

        [Fact]
        public async Task InvalidSignature_WhenReturnsValueTask_ReportsError()
        {
            var source = @"
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.DurableExecution;

namespace MyApp
{
    public class Workflows
    {
        [LambdaFunction]
        [DurableExecution]
        public ValueTask<string> Run(string input, IDurableContext ctx) => new ValueTask<string>(input);
    }
}";
            var test = NewTest(source, OutputKind.ConsoleApplication);
            await AddAnnotationSourcesAsync(test);
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0142", DiagnosticSeverity.Error)
                    .WithSpan("Workflow.cs", 10, 9, 12, 105)
                    .WithArguments("The return type must be Task or Task<TOutput> but was 'System.Threading.Tasks.ValueTask<string>'."));
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            await test.RunAsync();
        }

        [Fact]
        public async Task InvalidSignature_WhenWrongParameterCount_ReportsError()
        {
            // One parameter (not two). The return type is valid so only the param-count branch fires.
            var source = @"
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;

namespace MyApp
{
    public class Workflows
    {
        [LambdaFunction]
        [DurableExecution]
        public Task<string> Run(string input) => Task.FromResult(input);
    }
}";
            var test = NewTest(source, OutputKind.ConsoleApplication);
            await AddAnnotationSourcesAsync(test);
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0142", DiagnosticSeverity.Error)
                    .WithSpan("Workflow.cs", 9, 9, 11, 73)
                    .WithArguments("The method has 1 parameter(s)."));
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            await test.RunAsync();
        }

        [Fact]
        public async Task ZipOnly_WhenImagePackaging_ReportsError()
        {
            var source = @"
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.DurableExecution;

namespace MyApp
{
    public class Workflows
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [DurableExecution]
        public Task<string> Run(string input, IDurableContext ctx) => Task.FromResult(input);
    }
}";
            var test = NewTest(source, OutputKind.ConsoleApplication);
            await AddAnnotationSourcesAsync(test);
            // LambdaPackageType must bind for the generator to read PackageType = Image off the attribute.
            test.TestState.Sources.Add((Path.Combine("Amazon.Lambda.Annotations", "LambdaPackageType.cs"), await AnnotationsSource("LambdaPackageType.cs")));
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0141", DiagnosticSeverity.Error)
                    .WithSpan("Workflow.cs", 10, 9, 12, 94));
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            await test.RunAsync();
        }

        [Fact]
        public async Task InvalidAttribute_WhenRetentionPeriodOutOfRange_ReportsError()
        {
            var source = @"
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.DurableExecution;

namespace MyApp
{
    public class Workflows
    {
        [LambdaFunction]
        [DurableExecution(RetentionPeriodInDays = -1)]
        public Task<string> Run(string input, IDurableContext ctx) => Task.FromResult(input);
    }
}";
            var test = NewTest(source, OutputKind.ConsoleApplication);
            await AddAnnotationSourcesAsync(test);
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0144", DiagnosticSeverity.Error)
                    .WithSpan("Workflow.cs", 10, 9, 12, 94)
                    .WithArguments("RetentionPeriodInDays = -1. It must be between 1 and 90."));
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            await test.RunAsync();
        }

        [Fact]
        public async Task ExplicitRole_ReportsCheckpointPolicyInfo()
        {
            // A valid durable function with an explicit Role: generation succeeds and emits the AWSLambda0143
            // Info diagnostic telling the user to attach the checkpoint actions to their role manually.
            var source = @"
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.DurableExecution;

namespace MyApp
{
    public class Workflows
    {
        [LambdaFunction(Role = ""arn:aws:iam::123456789012:role/MyRole"")]
        [DurableExecution]
        public Task<string> Run(string input, IDurableContext ctx) => Task.FromResult(input);
    }
}";
            var test = NewTest(source, OutputKind.ConsoleApplication);
            await AddAnnotationSourcesAsync(test);
            // Generation proceeds (Info severity does not halt it); ignore the per-file codegen Info noise
            // (AWSLambda0103) and the generated-source list so this test focuses on the explicit-Role diagnostic.
            test.DisabledDiagnostics.Add("AWSLambda0103");
            test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0143", DiagnosticSeverity.Info)
                    .WithSpan("Workflow.cs", 10, 9, 12, 94));
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            await test.RunAsync();
        }

        [Fact]
        public async Task ExclusiveEvent_WhenCombinedWithRestApi_ReportsMultipleEventsNotSupported()
        {
            // The existing AWSLambda0102 covers durable + another event attribute (no new diagnostic).
            var source = @"
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.DurableExecution;

namespace MyApp
{
    public class Workflows
    {
        [LambdaFunction]
        [DurableExecution]
        [RestApi(LambdaHttpMethod.Get, ""/run"")]
        public Task<string> Run(string input, IDurableContext ctx) => Task.FromResult(input);
    }
}";
            var test = NewTest(source, OutputKind.ConsoleApplication);
            await AddAnnotationSourcesAsync(test);
            test.TestState.Sources.Add((Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), await AnnotationsSource(Path.Combine("APIGateway", "RestApiAttribute.cs"))));
            test.TestState.Sources.Add((Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await AnnotationsSource(Path.Combine("APIGateway", "HttpApiAttribute.cs"))));
            test.TestState.ExpectedDiagnostics.Add(
                new DiagnosticResult("AWSLambda0102", DiagnosticSeverity.Error).WithSpan("Workflow.cs", 11, 9, 14, 94));
            test.CompilerDiagnostics = CompilerDiagnostics.None;
            await test.RunAsync();
        }
    }
}
