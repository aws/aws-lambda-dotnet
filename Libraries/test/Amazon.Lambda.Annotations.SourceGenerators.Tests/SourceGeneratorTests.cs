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
    public class SourceGeneratorTests : IDisposable
    {
        [Fact]
        public async Task Greeter()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "greeter.template"));
            var expectedSayHelloGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Greeter_SayHello_Generated.g.cs"));
            var expectedSayHelloAsyncGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Greeter_SayHelloAsync_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "Greeter.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "Greeter.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "Greeter_SayHello_Generated.g.cs",
                            SourceText.From(expectedSayHelloGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Greeter_SayHelloAsync_Generated.g.cs",
                            SourceText.From(expectedSayHelloAsyncGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Greeter_SayHello_Generated.g.cs", expectedSayHelloGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Greeter_SayHelloAsync_Generated.g.cs", expectedSayHelloAsyncGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }
        
        [Fact]
        public async Task TestExecutableOutputWithNoAnnotations()
        {

            await new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        (Path.Combine("TestExecutableServerlessApp", "ExecutableNoAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "ExecutableNoAttributes.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributesWithRouting.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    ExpectedDiagnostics =
                    {
                        DiagnosticResult.CompilerError("AWSLambda0113")
                            .WithMessage("Your project is configured to output an executable and generate a static Main method, but you have not configured any methods with the 'LambdaFunction' attribute"),
                        DiagnosticResult.CompilerError("CS5001")
                            .WithMessage("Program does not contain a static 'Main' method suitable for an entry point"),
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task GeneratorDoesNotRunDueToCompileError()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "greeter.template"));
            var expectedSayHelloGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Greeter_SayHello_Generated.g.cs"));
            var expectedSayHelloAsyncGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Greeter_SayHelloAsync_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {                   
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "Greeter.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "Greeter.cs"))),
                        ("NonCompilableCodeFile.cs", await File.ReadAllTextAsync("NonCompilableCodeFile.cs")),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        // If the generator would have ran then we would see the generated sources from the Greeter syntax tree.
                    },
                    ExpectedDiagnostics =
                    {
                        // Now AWS Lambda Annotations INFO diagnostics were emited showing again the generator didn't run.
                        new DiagnosticResult("CS1513", DiagnosticSeverity.Error).WithSpan("NonCompilableCodeFile.cs", 22, 2, 22, 2)
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task SimpleCalculator()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "simpleCalculator.template"));
            var expectedAddGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "SimpleCalculator_Add_Generated.g.cs"));
            var expectedSubtractGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "SimpleCalculator_Subtract_Generated.g.cs"));
            var expectedMultiplyGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "SimpleCalculator_Multiply_Generated.g.cs"));
            var expectedDivideAsyncGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "SimpleCalculator_DivideAsync_Generated.g.cs"));
            var expectedPiGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "SimpleCalculator_Pi_Generated.g.cs"));
            var expectedRandomGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "SimpleCalculator_Random_Generated.g.cs"));
            var expectedRandomsGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "SimpleCalculator_Randoms_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {

                        (Path.Combine("TestServerlessApp", "SimpleCalculator.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "SimpleCalculator.cs"))),
                        (Path.Combine("TestServerlessApp", "Startup.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "Startup.cs"))),
                        (Path.Combine("TestServerlessApp", "Services", "SimpleCalculatorService.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "Services", "SimpleCalculatorService.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "FromServicesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "FromServicesAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Add_Generated.g.cs",
                            SourceText.From(expectedAddGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Subtract_Generated.g.cs",
                            SourceText.From(expectedSubtractGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Multiply_Generated.g.cs",
                            SourceText.From(expectedMultiplyGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_DivideAsync_Generated.g.cs",
                            SourceText.From(expectedDivideAsyncGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Pi_Generated.g.cs",
                            SourceText.From(expectedPiGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Random_Generated.g.cs",
                            SourceText.From(expectedRandomGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Randoms_Generated.g.cs",
                            SourceText.From(expectedRandomsGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Add_Generated.g.cs", expectedAddGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Subtract_Generated.g.cs", expectedSubtractGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Multiply_Generated.g.cs", expectedMultiplyGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_DivideAsync_Generated.g.cs", expectedDivideAsyncGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Pi_Generated.g.cs", expectedPiGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Random_Generated.g.cs", expectedRandomGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Randoms_Generated.g.cs", expectedRandomsGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task ComplexCalculator()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "complexCalculator.template"));
            var expectedAddGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "ComplexCalculator_Add_Generated.g.cs"));
            var expectedSubtractGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "ComplexCalculator_Subtract_Generated.g.cs"));
            
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "ComplexCalculator.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "ComplexCalculator.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "ComplexCalculator_Add_Generated.g.cs",
                            SourceText.From(expectedAddGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "ComplexCalculator_Subtract_Generated.g.cs",
                            SourceText.From(expectedSubtractGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("ComplexCalculator_Add_Generated.g.cs", expectedAddGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("ComplexCalculator_Subtract_Generated.g.cs", expectedSubtractGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task TestInvalidGlobalRuntime_ShouldError()
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestExecutableServerlessApp", "Sub1", "Functions.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Sub1", "Functions.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "Startup.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Startup.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "Services", "SimpleCalculatorService.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Services", "SimpleCalculatorService.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"))),
                        SourceText.From(InvalidAssemblyAttributeString),
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0112", DiagnosticSeverity.Error).WithMessage("The runtime selected in the Amazon.Lambda.Annotations.LambdaGlobalPropertiesAttribute is not a supported value. The valid values are: dotnet6, provided.al2, provided.al2023, dotnet8, dotnet10"),
                    }
                }
            };

            foreach (var file in Directory.GetFiles(
                         Path.Combine("Amazon.Lambda.RuntimeSupport"),
                         "*.cs", SearchOption.AllDirectories))
            {
                test.TestState.Sources.Add((file, await File.ReadAllTextAsync(file)));
            }

            await test.RunAsync();
        }

        [Fact]
        public async Task VerifyFunctionInSubNamespace()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "subnamespace.template"));
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Functions_ToUpper_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "Sub1", "Functions.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "Sub1", "Functions.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "Functions_ToUpper_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Functions_ToUpper_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyExecutableAssemblyWithZipAndHandler()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "subnamespace_executable.template"));
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Functions_AsyncStartupToLower_Generated.g.cs"));
            var expectedProgram = await ReadSnapshotContent(Path.Combine("Snapshots", "ProgramZipOutput.g.cs"));

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        (Path.Combine("TestExecutableServerlessApp", "Sub1", "FunctionsZipOutput.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Sub1", "FunctionsZipOutput.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "Startup.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Startup.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "Services", "SimpleCalculatorService.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Services", "SimpleCalculatorService.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "FunctionsZipOutput_ToLower_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Program.g.cs",
                            SourceText.From(expectedProgram, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("FunctionsZipOutput_ToLower_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),
                    }
                }
            };

            foreach (var file in Directory.GetFiles(
                         Path.Combine("Amazon.Lambda.RuntimeSupport"),
                         "*.cs", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);

                // Don't include RuntimeSupport's entry point.
                if (file.EndsWith("Program.cs") && content.Contains("Task Main(string[] args)"))
                    continue;
                
                test.TestState.Sources.Add((file, await File.ReadAllTextAsync(file)));
            }

            await test.RunAsync();
        }

        [Fact]
        public async Task VerifyExecutableAssembly()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "subnamespace_executableimage.template"));
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Functions_AsyncStartupToUpper_Generated.g.cs"));
            var expectedProgramGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Program.g.cs"));
            
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        (Path.Combine("TestExecutableServerlessApp", "Sub1", "Functions.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Sub1", "Functions.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "Startup.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Startup.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "Services", "SimpleCalculatorService.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Services", "SimpleCalculatorService.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "Functions_ToUpper_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Program.g.cs",
                            SourceText.From(expectedProgramGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Functions_ToUpper_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),
                    }
                }
            };

            foreach (var file in Directory.GetFiles(
                         Path.Combine("Amazon.Lambda.RuntimeSupport"),
                         "*.cs", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);

                // Don't include RuntimeSupport's entry point.
                if (file.EndsWith("Program.cs") && content.Contains("Task Main(string[] args)"))
                    continue;
                
                test.TestState.Sources.Add((file, await File.ReadAllTextAsync(file)));
            }

            await test.RunAsync();
        }

        [Fact]
        public async Task VerifyExecutableAssemblyWithParameterlessConstructor()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "parameterless.template"));
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "ParameterlessMethods_ToUpper_Generated.g.cs"));
            var expectedProgramGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "ProgramParameterless.g.cs"));

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        (Path.Combine("TestExecutableServerlessApp", "ParameterlessMethods.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "ParameterlessMethods.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "ParameterlessMethods_NoParameter_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Program.g.cs",
                            SourceText.From(expectedProgramGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("ParameterlessMethods_NoParameter_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),
                    }
                }
            };

            foreach (var file in Directory.GetFiles(
                         Path.Combine("Amazon.Lambda.RuntimeSupport"),
                         "*.cs", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);

                // Don't include RuntimeSupport's entry point.
                if (file.EndsWith("Program.cs") && content.Contains("Task Main(string[] args)"))
                    continue;
                
                test.TestState.Sources.Add((file, await File.ReadAllTextAsync(file)));
            }

            await test.RunAsync();
        }

        [Fact]
        public async Task VerifyExecutableAssemblyWithParameterlessConstructorAndResponse()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "parameterlesswithresponse.template"));
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "ParameterlessMethodWithResponse_ToUpper_Generated.g.cs"));
            var expectedProgramGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "ProgramParameterlessWithResponse.g.cs"));

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        (Path.Combine("TestExecutableServerlessApp", "ParameterlessMethodWithResponse.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "ParameterlessMethodWithResponse.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "ParameterlessMethodWithResponse_NoParameterWithResponse_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Program.g.cs",
                            SourceText.From(expectedProgramGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("ParameterlessMethodWithResponse_NoParameterWithResponse_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),
                    }
                }
            };

            foreach (var file in Directory.GetFiles(
                         Path.Combine("Amazon.Lambda.RuntimeSupport"),
                         "*.cs", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);

                // Don't include RuntimeSupport's entry point.
                if (file.EndsWith("Program.cs") && content.Contains("Task Main(string[] args)"))
                    continue;
                
                test.TestState.Sources.Add((file, await File.ReadAllTextAsync(file)));
            }

            await test.RunAsync();
        }

        [Fact]
        public async Task VerifyExecutableAssembly_WithNullAttributeValues_ShouldComplete()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "subnamespace.template"));
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "Functions_ToUpper_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "Sub1", "Functions.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "Sub1", "Functions.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        SourceText.From(NullAssemblyAttributeString)
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "Functions_ToUpper_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Functions_ToUpper_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyExecutableAssemblyWithMultipleHandler()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "greeter_executable.template"));
            var expectedSayHello = await ReadSnapshotContent(Path.Combine("Snapshots", "GreeterExecutable_SayHello_Generated.g.cs"));
            var expectedSayHelloAsync = await ReadSnapshotContent(Path.Combine("Snapshots", "GreeterExecutable_SayHelloAsync_Generated.g.cs"));
            var expectedProgramGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "ProgramMultiHandler.g.cs"));
            
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        (Path.Combine("TestExecutableServerlessApp", "Greeter.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Greeter.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "Startup.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Startup.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "Services", "SimpleCalculatorService.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "Services", "SimpleCalculatorService.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "Greeter_SayHello_Generated.g.cs",
                            SourceText.From(expectedSayHello, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Greeter_SayHelloAsync_Generated.g.cs",
                            SourceText.From(expectedSayHelloAsync, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Program.g.cs",
                            SourceText.From(expectedProgramGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Greeter_SayHello_Generated.g.cs", expectedSayHello),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Greeter_SayHelloAsync_Generated.g.cs", expectedSayHelloAsync),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),
                    }
                }
            };

            foreach (var file in Directory.GetFiles(
                         Path.Combine("Amazon.Lambda.RuntimeSupport"),
                         "*.cs", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);

                // Don't include RuntimeSupport's entry point.
                if (file.EndsWith("Program.cs") && content.Contains("Task Main(string[] args)"))
                    continue;
                
                test.TestState.Sources.Add((file, await File.ReadAllTextAsync(file)));
            }

            await test.RunAsync();
        }

        [Fact]
        public async Task VerifySourceGeneratorSerializerWithHttpResultsBody()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "sourcegeneratorserializationexample.template"));
            var expectedFunctionContent = await ReadSnapshotContent(Path.Combine("Snapshots", "SourceGenerationSerializationExample_GetPerson_Generated.g.cs"));
            var expectedProgramGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "ProgramSourceGeneratorSerializationExample.g.cs"));

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources =
                    {
                        (Path.Combine("TestExecutableServerlessApp", "SourceGenerationSerializationExample.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "SourceGenerationSerializationExample.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaGlobalPropertiesAttribute.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "SourceGenerationSerializationExample_GetPerson_Generated.g.cs",
                            SourceText.From(expectedFunctionContent, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Program.g.cs",
                            SourceText.From(expectedProgramGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SourceGenerationSerializationExample_GetPerson_Generated.g.cs", expectedFunctionContent),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),

                        // The test framework doesn't appear to also execute the System.Text.Json source generator so Annotations generated code relying on the generated System.Text.Json code does not exist
                        // so we get compile errors. In an real world scenario they are both run and the applicaton compiles correctly.
                        DiagnosticResult.CompilerError("CS0534").WithSpan($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}SourceGenerationSerializationExample.cs", 26, 26, 26, 54).WithArguments("TestExecutableServerlessApp.HttpApiJsonSerializerContext", "System.Text.Json.Serialization.JsonSerializerContext.GeneratedSerializerOptions.get"),
                        DiagnosticResult.CompilerError("CS0534").WithSpan($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}SourceGenerationSerializationExample.cs", 26, 26, 26, 54).WithArguments("TestExecutableServerlessApp.HttpApiJsonSerializerContext", "System.Text.Json.Serialization.JsonSerializerContext.GetTypeInfo(System.Type)"),
                        DiagnosticResult.CompilerError("CS7036").WithSpan($"TestExecutableServerlessApp{Path.DirectorySeparatorChar}SourceGenerationSerializationExample.cs", 26, 26, 26, 54).WithArguments("options", "System.Text.Json.Serialization.JsonSerializerContext.JsonSerializerContext(System.Text.Json.JsonSerializerOptions?)"),
                    }
                }
            };

            foreach (var file in Directory.GetFiles(
                         Path.Combine("Amazon.Lambda.RuntimeSupport"),
                         "*.cs", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(file);

                // Don't include RuntimeSupport's entry point.
                if (file.EndsWith("Program.cs") && content.Contains("Task Main(string[] args)"))
                    continue;

                test.TestState.Sources.Add((file, content));
            }

            await test.RunAsync();
        }

        [Fact]
        public async Task VerifyFunctionReturnVoid()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "voidexample.template"));
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "VoidExample_VoidReturn_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "VoidExample.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "VoidExample.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "VoidExample_VoidReturn_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("VoidExample_VoidReturn_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyNoErrorWithIntrinsicInTemplate()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "intrinsicexample.template"), false);
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "IntrinsicExample_HasIntrinsic_Generated.g.cs"));
            await File.WriteAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"), expectedTemplateContent);

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "IntrinsicExample.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "IntrinsicExample.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "IntrinsicExample_HasIntrinsic_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("IntrinsicExample_HasIntrinsic_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyFunctionReturnTask()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "taskexample.template"));
            var expectedSubNamespaceGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "TaskExample_TaskReturn_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "TaskExample.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "TaskExample.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "TaskExample_TaskReturn_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("TaskExample_TaskReturn_Generated.g.cs", expectedSubNamespaceGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyFunctionDynamic()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "dynamicexample.template"));
            var expectedSubNamespaceGenerated_DynamicReturn = await ReadSnapshotContent(Path.Combine("Snapshots", "DynamicExample_DynamicReturn_Generated.g.cs"));
            var expectedSubNamespaceGenerated_DynamicInput = await ReadSnapshotContent(Path.Combine("Snapshots", "DynamicExample_DynamicInput_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "DynamicExample.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "DynamicExample.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "DynamicExample_DynamicReturn_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated_DynamicReturn, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "DynamicExample_DynamicInput_Generated.g.cs",
                            SourceText.From(expectedSubNamespaceGenerated_DynamicInput, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("DynamicExample_DynamicReturn_Generated.g.cs", expectedSubNamespaceGenerated_DynamicReturn),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("DynamicExample_DynamicInput_Generated.g.cs", expectedSubNamespaceGenerated_DynamicInput)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task CustomizeResponses()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "customizeResponse.template"));
            var expectedOkResponseWithHeaderGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomizeResponseExamples_OkResponseWithHeader_Generated.g.cs"));
            var expectedNotFoundResponseWithHeaderV2Generated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomizeResponseExamples_NotFoundResponseWithHeaderV2_Generated.g.cs"));
            var expectedNotFoundResponseWithHeaderV1Generated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomizeResponseExamples_NotFoundResponseWithHeaderV1_Generated.g.cs"));

            var expectedOkResponseWithHeaderAsyncGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomizeResponseExamples_OkResponseWithHeaderAsync_Generated.g.cs"));
            var expectedNotFoundResponseWithHeaderV2AsyncGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomizeResponseExamples_NotFoundResponseWithHeaderV2Async_Generated.g.cs"));
            var expectedNotFoundResponseWithHeaderV1AsyncGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomizeResponseExamples_NotFoundResponseWithHeaderV1Async_Generated.g.cs"));
            var expectedOkResponseWithCustomSerializerGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomizeResponseExamples_OkResponseWithCustomSerializer_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "CustomizeResponseExamples.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "CustomizeResponseExamples.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomizeResponseExamples_OkResponseWithHeader_Generated.g.cs",
                            SourceText.From(expectedOkResponseWithHeaderGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomizeResponseExamples_OkResponseWithHeaderAsync_Generated.g.cs",
                            SourceText.From(expectedOkResponseWithHeaderAsyncGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomizeResponseExamples_NotFoundResponseWithHeaderV2_Generated.g.cs",
                            SourceText.From(expectedNotFoundResponseWithHeaderV2Generated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomizeResponseExamples_NotFoundResponseWithHeaderV2Async_Generated.g.cs",
                            SourceText.From(expectedNotFoundResponseWithHeaderV2AsyncGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomizeResponseExamples_NotFoundResponseWithHeaderV1_Generated.g.cs",
                            SourceText.From(expectedNotFoundResponseWithHeaderV1Generated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomizeResponseExamples_NotFoundResponseWithHeaderV1Async_Generated.g.cs",
                            SourceText.From(expectedNotFoundResponseWithHeaderV1AsyncGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomizeResponseExamples_OkResponseWithCustomSerializer_Generated.g.cs",
                            SourceText.From(expectedOkResponseWithCustomSerializerGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomizeResponseExamples_OkResponseWithHeader_Generated.g.cs", expectedOkResponseWithHeaderGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomizeResponseExamples_OkResponseWithHeaderAsync_Generated.g.cs", expectedOkResponseWithHeaderAsyncGenerated),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomizeResponseExamples_NotFoundResponseWithHeaderV2_Generated.g.cs", expectedNotFoundResponseWithHeaderV2Generated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomizeResponseExamples_NotFoundResponseWithHeaderV2Async_Generated.g.cs", expectedNotFoundResponseWithHeaderV2AsyncGenerated),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomizeResponseExamples_NotFoundResponseWithHeaderV1_Generated.g.cs", expectedNotFoundResponseWithHeaderV1Generated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomizeResponseExamples_NotFoundResponseWithHeaderV1Async_Generated.g.cs", expectedNotFoundResponseWithHeaderV1AsyncGenerated),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomizeResponseExamples_OkResponseWithCustomSerializer_Generated.g.cs", expectedOkResponseWithCustomSerializerGenerated),
                        // The test framework doesn't appear to also execute the System.Text.Json source generator so Annotations generated code relying on the generated System.Text.Json code does not exist
                        // so we get compile errors. In an real world scenario they are both run and the applicaton compiles correctly.
                        DiagnosticResult.CompilerError("CS0117").WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}CustomizeResponseExamples.cs", 99, 65, 99, 79).WithArguments("System.Text.Json.JsonNamingPolicy", "SnakeCaseUpper"),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task InvalidReturnTypeIHttpResult()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "CustomizeResponseWithErrors.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "CustomizeResponseWithErrors.cs.error"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    ExpectedDiagnostics =
                    {
                         DiagnosticResult
                            .CompilerError("AWSLambda0105")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}CustomizeResponseWithErrors.cs", 14, 9, 21, 10)
                            .WithMessage("IHttpResult is not a valid return type for LambdaFunctions without HttpApiAttribute or RestApiAttribute attributes")
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task MissingResourePathMapping()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "MissingResourePathMapping.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "MissingResourePathMapping.cs.error"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    ExpectedDiagnostics =
                    {
                         DiagnosticResult.CompilerError("AWSLambda0107").WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}MissingResourePathMapping.cs", 15, 9, 20, 10)
                                            .WithMessage("Route template /add/{x}/{y} is invalid. Missing x parameters in method definition.")
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task VerifyApiFunctionUsingNullableParameters()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "nullreferenceexample.template"));
            var expectedCSharpContent = await ReadSnapshotContent(Path.Combine("Snapshots", "NullableReferenceTypeExample_NullableHeaderHttpApi_Generated.g.cs"));

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "NullableReferenceTypeExample.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "NullableReferenceTypeExample.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "NullableReferenceTypeExample_NullableHeaderHttpApi_Generated.g.cs",
                            SourceText.From(expectedCSharpContent, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("NullableReferenceTypeExample_NullableHeaderHttpApi_Generated.g.cs", expectedCSharpContent),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                },
            };

            await test.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyNoApiGatewayEventsReference()
        {
            var test = new VerifyCS.Test(VerifyCS.Test.ReferencesMode.NoApiGatewayEvents)
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "FromScratch", "NoApiGatewayEventsReference.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "FromScratch", "NoApiGatewayEventsReference.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0104", DiagnosticSeverity.Error)
                            .WithMessage("Your project has a missing required package dependency. Please add a reference to the following package: Amazon.Lambda.APIGatewayEvents.")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}FromScratch{Path.DirectorySeparatorChar}NoApiGatewayEventsReference.cs", 9, 9, 14, 10),
                    },
                },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task VerifyNoSerializerAttributeReference()
        {
            var test = new VerifyCS.Test()
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "FromScratch", "NoSerializerAttributeReference.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "FromScratch", "NoSerializerAttributeReference.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                    },
                    GeneratedSources =
                    {
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0108", DiagnosticSeverity.Error)
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}FromScratch{Path.DirectorySeparatorChar}NoSerializerAttributeReference.cs", 9, 9, 13, 10),
                    },
                },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task ComplexQueryParameters_AreNotSupported()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "ComplexQueryParameter.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "ComplexQueryParameter.cs.error"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    ExpectedDiagnostics =
                    {
                         DiagnosticResult.CompilerError("AWSLambda0109").WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}ComplexQueryParameter.cs", 11, 9, 16, 10)
                                            .WithMessage("Unsupported query parameter 'person' of type 'TestServerlessApp.Person' encountered. Only primitive .NET types and their corresponding enumerable can be used as query parameters.")
                    },
                }

            }.RunAsync();
        }

        [Fact]
        public async Task InvalidParameterAttributeNames_ThrowsDiagnosticErrors()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "InvalidParameterAttributeNames.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "InvalidParameterAttributeNames.cs.error"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    ExpectedDiagnostics =
                    {
                         DiagnosticResult.CompilerError("AWSLambda0110").WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}InvalidParameterAttributeNames.cs", 10, 9, 15, 10)
                                            .WithMessage("Invalid parameter attribute name 'This is a name' for method parameter 'name' encountered. Valid values can only contain uppercase and lowercase alphanumeric characters, periods (.), hyphens (-), underscores (_) and dollar signs ($)."),

                          DiagnosticResult.CompilerError("AWSLambda0110").WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}InvalidParameterAttributeNames.cs", 18, 9, 23, 10)
                                            .WithMessage("Invalid parameter attribute name 'System.Diagnostics.Process.Start(\"CMD.exe\",\"whoami\");' for method parameter 'test' encountered. Valid values can only contain uppercase and lowercase alphanumeric characters, periods (.), hyphens (-), underscores (_) and dollar signs ($)."),

                          DiagnosticResult.CompilerError("AWSLambda0110").WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}InvalidParameterAttributeNames.cs", 26, 9, 31, 10)
                                            .WithMessage("Invalid parameter attribute name 'first@name' for method parameter 'firstName' encountered. Valid values can only contain uppercase and lowercase alphanumeric characters, periods (.), hyphens (-), underscores (_) and dollar signs ($).")
                    },
                }

            }.RunAsync();
        }

        /// <summary>
        /// Verifies that we set 'dotnet8' in the CFN Lambda runtime for a project targeting .NET 8
        /// </summary>
        [Fact]
        public async Task ToUpper_Net8()
        {
            var expectedFunctionContent = await ReadSnapshotContent(Path.Combine("Snapshots", "Functions_ToUpper_Generated_NET8.g.cs"));
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "net8.template"));

            await new VerifyCS.Test(targetFramework: VerifyCS.Test.TargetFramework.Net80)
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp.NET8", "Functions.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp.NET8", "Functions.cs"))),
                        (Path.Combine("TestServerlessApp.NET8", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp.NET8", "AssemblyAttributes.cs"))),

                    },
                    GeneratedSources =
                    {
                        (typeof(SourceGenerator.Generator),
                            "Functions_ToUpper_Generated.g.cs",
                            SourceText.From(expectedFunctionContent, Encoding.UTF8, SourceHashAlgorithm.Sha256))
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Functions_ToUpper_Generated.g.cs", expectedFunctionContent),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp.NET8{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp.NET8", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyInvalidSQSEvents_ThrowsCompilationErrors()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "SQSEventExamples", "InvalidSQSEvents.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "SQSEventExamples", "InvalidSQSEvents.cs.error"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "SQS", "SQSEventAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "SQS", "SQSEventAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    ExpectedDiagnostics =
                    {
                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 15, 9, 20, 10)
                            .WithArguments("BatchSize = 0. It must be between 1 and 10000"),

                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 15, 9, 20, 10)
                            .WithArguments("MaximumBatchingWindowInSeconds = 302. It must be between 0 and 300"),

                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 15, 9, 20, 10)
                            .WithArguments("MaximumConcurrency = 1. It must be between 2 and 1000"),

                        DiagnosticResult.CompilerError("AWSLambda0117")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 22, 9, 27, 10)
                            .WithArguments("When using the SQSEventAttribute, the Lambda method can accept at most 2 parameters. " +
                            "The first parameter is required and must be of type Amazon.Lambda.SQSEvents.SQSEvent. " +
                            "The second parameter is optional and must be of type Amazon.Lambda.Core.ILambdaContext."),

                        DiagnosticResult.CompilerError("AWSLambda0117")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 29, 9, 35, 10)
                            .WithArguments("When using the SQSEventAttribute, the Lambda method can return either " +
                            "void, System.Threading.Tasks.Task, Amazon.Lambda.SQSEvents.SQSBatchResponse or Task<Amazon.Lambda.SQSEvents.SQSBatchResponse>"),

                        DiagnosticResult
                            .CompilerError("AWSLambda0102")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 37, 9, 43, 10)
                            .WithMessage("Multiple event attributes on LambdaFunction are not supported"),

                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 45, 9, 50, 10)
                            .WithArguments("Queue = test-queue. The SQS queue ARN is invalid. The ARN format is 'arn:<partition>:sqs:<region>:<account-id>:<queue-name>'"),

                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 52, 9, 57, 10)
                            .WithArguments("ResourceName = sqs-event-source. It must only contain alphanumeric characters and must not be an empty string"),

                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 59, 9, 64, 10)
                            .WithArguments("ResourceName = . It must only contain alphanumeric characters and must not be an empty string"),

                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 66, 9, 71, 10)
                            .WithArguments("MaximumBatchingWindowInSeconds is not set or set to a value less than 1. It must be set to at least 1 when BatchSize is greater than 10"),

                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 73, 9, 78, 10)
                            .WithArguments("MaximumBatchingWindowInSeconds is not set or set to a value less than 1. It must be set to at least 1 when BatchSize is greater than 10"),

                        DiagnosticResult.CompilerError("AWSLambda0116")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 80, 9, 85, 10)
                            .WithArguments("BatchSize = 100. It must be less than or equal to 10 when the event source mapping is for a FIFO queue"),
            
                        DiagnosticResult.CompilerError("AWSLambda0116")
                        .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}SQSEventExamples{Path.DirectorySeparatorChar}InvalidSQSEvents.cs", 80, 9, 85, 10)
                        .WithArguments("MaximumBatchingWindowInSeconds must not be set when the event source mapping is for a FIFO queue")
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task VerifyValidSQSEvents()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "sqsEvents.template"));
            var validSqsEventsProcessMessagesGeneratedContent = await ReadSnapshotContent(Path.Combine("Snapshots", "SQS", "ValidSQSEvents_ProcessMessages_Generated.g.cs"));
            var validSqsEventsProcessMessagesWithReservedParameterNameGeneratedContent = await ReadSnapshotContent(Path.Combine("Snapshots", "SQS", "ValidSQSEvents_ProcessMessagesWithReservedParameterName_Generated.g.cs"));
            var validSqsEventsProcessMessagesWithBatchFailureReportingGeneratedContent = await ReadSnapshotContent(Path.Combine("Snapshots", "SQS", "ValidSQSEvents_ProcessMessagesWithBatchFailureReporting_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "SQSEventExamples", "ValidSQSEvents.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "SQSEventExamples", "ValidSQSEvents.cs.txt"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "SQS", "SQSEventAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "SQS", "SQSEventAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "ValidSQSEvents_ProcessMessages_Generated.g.cs",
                            SourceText.From(validSqsEventsProcessMessagesGeneratedContent, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "ValidSQSEvents_ProcessMessagesWithReservedParameterName_Generated.g.cs",
                            SourceText.From(validSqsEventsProcessMessagesWithReservedParameterNameGeneratedContent, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "ValidSQSEvents_ProcessMessagesWithBatchFailureReporting_Generated.g.cs",
                            SourceText.From(validSqsEventsProcessMessagesWithBatchFailureReportingGeneratedContent, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info)
                        .WithArguments("ValidSQSEvents_ProcessMessages_Generated.g.cs", validSqsEventsProcessMessagesGeneratedContent),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info)
                        .WithArguments("ValidSQSEvents_ProcessMessagesWithReservedParameterName_Generated.g.cs", validSqsEventsProcessMessagesWithReservedParameterNameGeneratedContent),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info)
                        .WithArguments("ValidSQSEvents_ProcessMessagesWithBatchFailureReporting_Generated.g.cs", validSqsEventsProcessMessagesWithBatchFailureReportingGeneratedContent),

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info)
                        .WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task ExceededMaximumHandlerLength()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "PlaceholderClass.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "PlaceholderClass.cs"))),
                        (Path.Combine("TestServerlessApp", "ExceededMaximumHandlerLength.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "ExceededMaximumHandlerLength.cs.error"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    ExpectedDiagnostics =
                    {
                         DiagnosticResult
                            .CompilerError("AWSLambda0118")
                            .WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}ExceededMaximumHandlerLength.cs", 9, 9, 13, 10)
                            .WithArguments("TestProject::TestServerlessApp.ExceededMaximumHandlerLength_SayHelloXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX_Generated::SayHelloXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX")
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task HostBuilder()
        {
            var expectedAddGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "HostBuilderFunctions_Add_Generated.g.cs"));
            var expectedTemplate = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "hostbuild.serverless.template"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestHostBuilderApp", "HostBuilderFunctions.cs"), await File.ReadAllTextAsync(Path.Combine("TestHostBuilderApp", "HostBuilderFunctions.cs"))),
                        (Path.Combine("TestHostBuilderApp", "Startup.cs"), await File.ReadAllTextAsync(Path.Combine("TestHostBuilderApp", "Startup.cs"))),
                        (Path.Combine("TestHostBuilderApp", "CalculatorService", "ICalculatorService.cs"), await File.ReadAllTextAsync(Path.Combine("TestHostBuilderApp", "CalculatorService", "ICalculatorService.cs"))),
                        (Path.Combine("TestHostBuilderApp", "CalculatorService", "CalculatorService.cs"), await File.ReadAllTextAsync(Path.Combine("TestHostBuilderApp", "CalculatorService", "CalculatorService.cs"))),

                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "HostBuilderFunctions_Add_Generated.g.cs",
                            SourceText.From(expectedAddGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("HostBuilderFunctions_Add_Generated.g.cs", expectedAddGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestHostBuilderApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplate),
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CustomAuthorizerRestTest()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "authorizerRest.template"));
            var expectedRestAuthorizerGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomAuthorizerRestExample_RestAuthorizer_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "CustomAuthorizerRestExample.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "CustomAuthorizerRestExample.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "RestApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomAuthorizerRestExample_RestAuthorizer_Generated.g.cs",
                            SourceText.From(expectedRestAuthorizerGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomAuthorizerRestExample_RestAuthorizer_Generated.g.cs", expectedRestAuthorizerGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task CustomAuthorizerHttpApiTest()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "authorizerHttpApi.template"));
            var expectedRestAuthorizerGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomAuthorizerHttpApiExample_HttpApiAuthorizer_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "CustomAuthorizerHttpApiExample.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "CustomAuthorizerHttpApiExample.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomAuthorizerHttpApiExample_HttpApiAuthorizer_Generated.g.cs",
                            SourceText.From(expectedRestAuthorizerGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomAuthorizerHttpApiExample_HttpApiAuthorizer_Generated.g.cs", expectedRestAuthorizerGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task CustomAuthorizerHttpApiV1Test()
        {
            var expectedTemplateContent = await ReadSnapshotContent(Path.Combine("Snapshots", "ServerlessTemplates", "authorizerHttpApiV1.template"));
            var expectedHttpApiV1AuthorizerGenerated = await ReadSnapshotContent(Path.Combine("Snapshots", "CustomAuthorizerHttpApiV1Example_HttpApiV1Authorizer_Generated.g.cs"));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "CustomAuthorizerHttpApiV1Example.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "CustomAuthorizerHttpApiV1Example.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "APIGateway", "HttpApiAttribute.cs"))),
                        (Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "AssemblyAttributes.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "CustomAuthorizerHttpApiV1Example_HttpApiV1Authorizer_Generated.g.cs",
                            SourceText.From(expectedHttpApiV1AuthorizerGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("CustomAuthorizerHttpApiV1Example_HttpApiV1Authorizer_Generated.g.cs", expectedHttpApiV1AuthorizerGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        public void Dispose()
        {
            File.Delete(Path.Combine("TestServerlessApp", "serverless.template"));
        }

        private async static Task<string> ReadSnapshotContent(string snapshotPath, bool trimContent = true)
        {
            var content = (await File.ReadAllTextAsync(snapshotPath));

            // YAML serverless.template, when generated, has extra line at the end.
            if (trimContent)
                content = content.Trim(); // Some Visual Studio update has a default setting, which is causing extra line to be added at the end when modified manually.

            return content.ToEnvironmentLineEndings().ApplyReplacements();
        }

        private static string InvalidAssemblyAttributeString = "using Amazon.Lambda.Annotations;" +
                                                               "using Amazon.Lambda.Core;" +
                                                               "[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]" +
                                                               "[assembly: LambdaGlobalProperties(GenerateMain = true, Runtime = \"notavalidruntime\")]";

        private static string NullAssemblyAttributeString = "using Amazon.Lambda.Annotations;" +
                                                               "using Amazon.Lambda.Core;" +
                                                               "[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]" +
                                                               "[assembly: LambdaGlobalProperties(Runtime = null)]";
    }
}
