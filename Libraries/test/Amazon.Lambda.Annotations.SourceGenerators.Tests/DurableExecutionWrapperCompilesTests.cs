// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    // Compile-level verification of the generated durable wrapper. Component C asserts the exact wrapper
    // TEXT the generator emits; this layer takes that emitted call shape and confirms it actually BINDS and
    // compiles against realistic DurableFunction.WrapAsync overload signatures - the unique risk being that
    // the no-explicit-generic method-group call resolves to the right overload (typed vs. void).
    //
    // We use the real overload set (typed + void) so overload resolution is genuinely exercised. The wrapper
    // body mirrors exactly what DurableExecutionInvoke.tt produces:
    //   return await Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync(instance.Method, __request__, __context__);
    public class DurableExecutionWrapperCompilesTests
    {
        private const string DurableSdk = @"
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

        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
            Func<TInput, IDurableContext, Task<TOutput>> workflow,
            DurableExecutionInvocationInput invocationInput,
            ILambdaContext lambdaContext,
            object lambdaClient) => Task.FromResult(new DurableExecutionInvocationOutput());

        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
            Func<TInput, IDurableContext, Task> workflow,
            DurableExecutionInvocationInput invocationInput,
            ILambdaContext lambdaContext) => Task.FromResult(new DurableExecutionInvocationOutput());

        public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
            Func<TInput, IDurableContext, Task> workflow,
            DurableExecutionInvocationInput invocationInput,
            ILambdaContext lambdaContext,
            object lambdaClient) => Task.FromResult(new DurableExecutionInvocationOutput());
    }
}
";

        // Mirrors the shape of DurableExecutionInvoke.tt + the generated method signature/usings from
        // GeneratedMethodModelBuilder, for a user workflow and a generated wrapper class.
        private static string WrapperProgram(string userReturnType, string userBody, string wrapperReturnExpression) => $@"
using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;

namespace MyApp
{{
    public class OrderProcessor
    {{
        public {userReturnType} Process(string order, IDurableContext ctx) => {userBody};
    }}

    public class OrderProcessor_Process_Generated
    {{
        private readonly OrderProcessor orderProcessor = new OrderProcessor();

        public async Task<Amazon.Lambda.DurableExecution.DurableExecutionInvocationOutput> Process(
            Amazon.Lambda.DurableExecution.DurableExecutionInvocationInput __request__,
            ILambdaContext __context__)
        {{
            {wrapperReturnExpression}
        }}
    }}
}}
";

        // Typed-output workflows need explicit <TInput, TOutput>; void workflows need explicit <TInput>.
        // Method-group arguments cannot infer these (CS0411), which is why the generator spells them out.
        private const string TypedWrapAsyncCall =
            "return await Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync<string, string>(orderProcessor.Process, __request__, __context__);";

        private const string VoidWrapAsyncCall =
            "return await Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync<string>(orderProcessor.Process, __request__, __context__);";

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var references = new List<MetadataReference>();
            var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
                .Split(Path.PathSeparator);
            foreach (var path in trusted)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    references.Add(MetadataReference.CreateFromFile(path));
            }
            references.Add(MetadataReference.CreateFromFile(typeof(Amazon.Lambda.Core.ILambdaContext).Assembly.Location));
            return references;
        }

        private static IReadOnlyList<Diagnostic> CompileErrors(string program)
        {
            var compilation = CSharpCompilation.Create(
                "DurableWrapperCompiles",
                new[] { CSharpSyntaxTree.ParseText(DurableSdk), CSharpSyntaxTree.ParseText(program) },
                BuildReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        }

        [Fact]
        public void GeneratedWrapper_BindsTypedOutputOverload()
        {
            // (string, IDurableContext) -> Task<string> binds WrapAsync<string, string>.
            var program = WrapperProgram("Task<string>", "Task.FromResult(order)", TypedWrapAsyncCall);

            var errors = CompileErrors(program);

            Assert.True(errors.Count == 0, "Unexpected compile errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
        }

        [Fact]
        public void GeneratedWrapper_BindsVoidOverload()
        {
            // (string, IDurableContext) -> Task binds WrapAsync<string>.
            var program = WrapperProgram("Task", "Task.CompletedTask", VoidWrapAsyncCall);

            var errors = CompileErrors(program);

            Assert.True(errors.Count == 0, "Unexpected compile errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
        }

        [Fact]
        public void MethodGroupCall_WithoutExplicitGenerics_FailsToCompile()
        {
            // Documents WHY the generator must emit explicit generics: the inference-free method-group form
            // does not compile (CS0411). This guards against a regression back to the inferred form.
            var inferredCall = "return await Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync(orderProcessor.Process, __request__, __context__);";
            var program = WrapperProgram("Task<string>", "Task.FromResult(order)", inferredCall);

            var errors = CompileErrors(program);

            Assert.Contains(errors, e => e.Id == "CS0411");
        }
    }
}
