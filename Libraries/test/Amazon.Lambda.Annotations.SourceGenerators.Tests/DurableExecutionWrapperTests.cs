// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Templates;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class DurableExecutionWrapperTests
    {
        // Builds a LambdaFunctionModel with the pieces DurableExecutionInvoke reads: the containing type
        // name, the user method name, the input parameter type, and the return type (Task<TOutput> vs Task).
        private static LambdaFunctionModel BuildModel(string containingTypeName, string methodName, string inputType, string outputType)
        {
            TypeModel returnType;
            bool returnsGenericTask;
            if (outputType == null)
            {
                returnType = new TypeModel { FullName = "System.Threading.Tasks.Task", TypeArguments = new List<TypeModel>() };
                returnsGenericTask = false;
            }
            else
            {
                returnType = new TypeModel
                {
                    FullName = $"System.Threading.Tasks.Task<{outputType}>",
                    IsGenericType = true,
                    TypeArguments = new List<TypeModel> { new TypeModel { FullName = outputType } }
                };
                returnsGenericTask = true;
            }

            return new LambdaFunctionModel
            {
                LambdaMethod = new LambdaMethodModel
                {
                    Name = methodName,
                    ReturnsGenericTask = returnsGenericTask,
                    ReturnType = returnType,
                    Parameters = new List<ParameterModel>
                    {
                        new ParameterModel { Name = "input", Type = new TypeModel { FullName = inputType } },
                        new ParameterModel { Name = "ctx", Type = new TypeModel { FullName = "Amazon.Lambda.DurableExecution.IDurableContext" } }
                    },
                    ContainingType = new TypeModel
                    {
                        Name = containingTypeName,
                        FullName = $"MyApp.{containingTypeName}",
                        IsValueType = false
                    }
                }
            };
        }

        [Fact]
        public void DurableExecutionInvoke_TypedOutput_EmitsExplicitGenerics()
        {
            var model = BuildModel("OrderProcessor", "Workflow", "MyApp.Order", "MyApp.OrderResult");

            var body = new DurableExecutionInvoke(model).TransformText();

            Assert.Equal(
                "            return await Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync<MyApp.Order, MyApp.OrderResult>(orderProcessor.Workflow, __request__, __context__);\r\n",
                body);
        }

        [Fact]
        public void DurableExecutionInvoke_VoidWorkflow_EmitsSingleTypeArgument()
        {
            var model = BuildModel("OrderProcessor", "Workflow", "MyApp.Order", outputType: null);

            var body = new DurableExecutionInvoke(model).TransformText();

            Assert.Equal(
                "            return await Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync<MyApp.Order>(orderProcessor.Workflow, __request__, __context__);\r\n",
                body);
        }

        [Fact]
        public void DurableExecutionInvoke_UsesCamelCasedInstanceField()
        {
            var model = BuildModel("MyWorkflows", "Run", "string", "string");

            var body = new DurableExecutionInvoke(model).TransformText();

            // The instance is the same camel-cased field FieldsAndConstructor/NoEventMethodBody use.
            Assert.Contains("myWorkflows.Run", body);
            // Explicit generic args are required (method-group args cannot be inferred).
            Assert.Contains("WrapAsync<string, string>", body);
            // Single delegation; the wrapper does not touch a serializer field or deserialize a stream.
            Assert.DoesNotContain("serializer", body);
            Assert.DoesNotContain("stream", body);
        }

        // Drives the FULL LambdaFunctionTemplate (which owns the DI prologue) for a DI-resolved durable
        // workflow, guarding the composition the per-slice tests can't see: the generated wrapper must resolve
        // the workflow instance from the request-scoped ServiceProvider and then immediately delegate to
        // WrapAsync using that SAME scoped instance. A future reorder of the DI vs. durable branches in
        // LambdaFunctionTemplate would break this even though the slice tests stay green.
        [Fact]
        public void LambdaFunctionTemplate_DurableWithDependencyInjection_ResolvesScopedInstanceThenDelegates()
        {
            var model = BuildDurableDependencyInjectionModel();

            var generated = new LambdaFunctionTemplate(model).TransformText();

            // The workflow instance is resolved from the per-invocation scope (not a constructor-initialized field).
            Assert.Contains("using var scope = serviceProvider.CreateScope();", generated);
            Assert.Contains("var orderProcessor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();", generated);
            Assert.DoesNotContain("new OrderProcessor()", generated);

            // The scoped resolution must come immediately before the durable delegation, using the scoped instance.
            var resolveIndex = generated.IndexOf("scope.ServiceProvider.GetRequiredService<OrderProcessor>()", System.StringComparison.Ordinal);
            var delegateIndex = generated.IndexOf("WrapAsync<MyApp.Order, MyApp.OrderResult>(orderProcessor.Workflow, __request__, __context__)", System.StringComparison.Ordinal);
            Assert.True(resolveIndex >= 0, "Scoped instance resolution not found in generated wrapper.");
            Assert.True(delegateIndex > resolveIndex, "Durable WrapAsync delegation must follow the scoped instance resolution.");
        }

        // A LambdaFunctionModel populated enough to render the whole LambdaFunctionTemplate for a DI durable
        // function (UsingDependencyInjection = true, ConfigureServices style), routed to the durable branch.
        private static LambdaFunctionModel BuildDurableDependencyInjectionModel()
        {
            var generatedReturnType = new TypeModel
            {
                FullName = "System.Threading.Tasks.Task<Amazon.Lambda.DurableExecution.DurableExecutionInvocationOutput>"
            };

            return new LambdaFunctionModel
            {
                SourceGeneratorVersion = "1.0.0.0",
                StartupType = new TypeModel { Name = "Startup", FullName = "MyApp.Startup" },
                LambdaMethod = new LambdaMethodModel
                {
                    Name = "Workflow",
                    ContainingNamespace = "MyApp",
                    UsingDependencyInjection = true,
                    UsingHostBuilderForDependencyInjection = false,
                    ReturnsGenericTask = true,
                    ReturnType = new TypeModel
                    {
                        FullName = "System.Threading.Tasks.Task<MyApp.OrderResult>",
                        IsGenericType = true,
                        TypeArguments = new List<TypeModel> { new TypeModel { FullName = "MyApp.OrderResult" } }
                    },
                    Events = new HashSet<EventType> { EventType.DurableExecution },
                    Parameters = new List<ParameterModel>
                    {
                        new ParameterModel { Name = "input", Type = new TypeModel { FullName = "MyApp.Order" } },
                        new ParameterModel { Name = "ctx", Type = new TypeModel { FullName = "Amazon.Lambda.DurableExecution.IDurableContext" } }
                    },
                    ContainingType = new TypeModel
                    {
                        Name = "OrderProcessor",
                        FullName = "MyApp.OrderProcessor",
                        IsValueType = false
                    }
                },
                GeneratedMethod = new GeneratedMethodModel
                {
                    ReturnType = generatedReturnType,
                    Usings = new List<string>
                    {
                        "Microsoft.Extensions.DependencyInjection",
                        "Amazon.Lambda.Core",
                        "Amazon.Lambda.DurableExecution"
                    },
                    ContainingType = new TypeModel
                    {
                        Name = "OrderProcessor_Workflow_Generated",
                        FullName = "MyApp.OrderProcessor_Workflow_Generated",
                        IsValueType = false
                    },
                    Parameters = new List<ParameterModel>
                    {
                        new ParameterModel { Name = "__request__", Type = new TypeModel { FullName = "Amazon.Lambda.DurableExecution.DurableExecutionInvocationInput" }, Documentation = "The durable execution service envelope." },
                        new ParameterModel { Name = "__context__", Type = new TypeModel { FullName = "Amazon.Lambda.Core.ILambdaContext" }, Documentation = "The ILambdaContext." }
                    }
                }
            };
        }
    }
}
