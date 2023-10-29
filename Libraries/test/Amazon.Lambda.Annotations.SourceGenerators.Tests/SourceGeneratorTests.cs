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
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "greeter.template"))).ToEnvironmentLineEndings();
            var expectedSayHelloGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Greeter_SayHello_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedSayHelloAsyncGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Greeter_SayHelloAsync_Generated.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
                        DiagnosticResult.CompilerError("AWSLambda0104")
                            .WithMessage("Your project is configured to output an executable and generate a static Main method, but you have not configured any methods with the 'LambdaFunction' attribute."),
                        DiagnosticResult.CompilerError("CS5001")
                            .WithMessage("Program does not contain a static 'Main' method suitable for an entry point"),
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();
        }

        [Fact]
        public async Task GeneratorDoesNotRunDueToCompileError()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "greeter.template"))).ToEnvironmentLineEndings();
            var expectedSayHelloGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Greeter_SayHello_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedSayHelloAsyncGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Greeter_SayHelloAsync_Generated.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();
        }

        [Fact]
        public async Task SimpleCalculator()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "simpleCalculator.template"))).ToEnvironmentLineEndings();
            var expectedAddGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "SimpleCalculator_Add_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedSubtractGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "SimpleCalculator_Subtract_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedMultiplyGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "SimpleCalculator_Multiply_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedDivideAsyncGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "SimpleCalculator_DivideAsync_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedPiGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "SimpleCalculator_Pi_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedRandomGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "SimpleCalculator_Random_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedRandomsGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "SimpleCalculator_Randoms_Generated.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task ComplexCalculator()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "complexCalculator.template"))).ToEnvironmentLineEndings();
            var expectedAddGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ComplexCalculator_Add_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedSubtractGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ComplexCalculator_Subtract_Generated.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributesInvalidRuntime.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributesInvalidRuntime.cs"))),
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0112", DiagnosticSeverity.Error).WithMessage("The runtime selected in the Amazon.Lambda.Annotations.LambdaGlobalPropertiesAttribute is not a supported value It should be set to either 'dotnet6' or 'provided.al2'."),
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "subnamespace.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Functions_ToUpper_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedProgramGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Program.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyExecutableAssemblyWithZipAndHandler()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "subnamespace_executable.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Functions_AsyncStartupToLower_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedProgram = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ProgramZipOutput.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "subnamespace_executableimage.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Functions_AsyncStartupToUpper_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedProgramGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Program.g.cs"))).ToEnvironmentLineEndings();
            
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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "parameterless.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ParameterlessMethods_ToUpper_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedProgramGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ProgramParameterless.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "subnamespace.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Functions_ToUpper_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedProgramGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "Program.g.cs"))).ToEnvironmentLineEndings();

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "Sub1", "Functions.cs"), await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "Sub1", "Functions.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), await File.ReadAllTextAsync(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("TestExecutableServerlessApp", "AssemblyAttributeNullValues.cs"), await File.ReadAllTextAsync(Path.Combine("TestExecutableServerlessApp", "AssemblyAttributeNullValues.cs"))),
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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyExecutableAssemblyWithMultipleHandler()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "greeter_executable.template"))).ToEnvironmentLineEndings();
            var expectedSayHello = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "GreeterExecutable_SayHello_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedSayHelloAsync = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "GreeterExecutable_SayHelloAsync_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedProgramGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ProgramMultiHandler.g.cs"))).ToEnvironmentLineEndings();
            
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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
        public async Task VerifyFunctionReturnVoid()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "voidexample.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "VoidExample_VoidReturn_Generated.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyNoErrorWithIntrinsicInTemplate()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "intrinsicexample.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "IntrinsicExample_HasIntrinsic_Generated.g.cs"))).ToEnvironmentLineEndings();
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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyFunctionReturnTask()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "taskexample.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "TaskExample_TaskReturn_Generated.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task VerifyFunctionDynamic()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "dynamicexample.template"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated_DynamicReturn = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "DynamicExample_DynamicReturn_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedSubNamespaceGenerated_DynamicInput = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "DynamicExample_DynamicInput_Generated.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }
            }.RunAsync();

            var actualTemplateContent = await File.ReadAllTextAsync(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task CustomizeResponses()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "customizeResponse.template"))).ToEnvironmentLineEndings();
            var expectedOkResponseWithHeaderGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "CustomizeResponseExamples_OkResponseWithHeader_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedNotFoundResponseWithHeaderV2Generated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "CustomizeResponseExamples_NotFoundResponseWithHeaderV2_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedNotFoundResponseWithHeaderV1Generated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "CustomizeResponseExamples_NotFoundResponseWithHeaderV1_Generated.g.cs"))).ToEnvironmentLineEndings();

            var expectedOkResponseWithHeaderAsyncGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "CustomizeResponseExamples_OkResponseWithHeaderAsync_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedNotFoundResponseWithHeaderV2AsyncGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "CustomizeResponseExamples_NotFoundResponseWithHeaderV2Async_Generated.g.cs"))).ToEnvironmentLineEndings();
            var expectedNotFoundResponseWithHeaderV1AsyncGenerated = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "CustomizeResponseExamples_NotFoundResponseWithHeaderV1Async_Generated.g.cs"))).ToEnvironmentLineEndings();

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

                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
                         DiagnosticResult.CompilerError("AWSLambda0105").WithSpan($"TestServerlessApp{Path.DirectorySeparatorChar}CustomizeResponseWithErrors.cs", 14, 9, 21, 10).WithArguments("Error")
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }

            }.RunAsync();
        }

        [Fact]
        public async Task VerifyApiFunctionUsingNullableParameters()
        {
            var expectedTemplateContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "ServerlessTemplates", "nullreferenceexample.template"))).ToEnvironmentLineEndings();
            var expectedCSharpContent = (await File.ReadAllTextAsync(Path.Combine("Snapshots", "NullableReferenceTypeExample_NullableHeaderHttpApi_Generated.g.cs"))).ToEnvironmentLineEndings();

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
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60,                    
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
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
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
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
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
                                            .WithMessage("Unsupported query paramter 'person' of type 'TestServerlessApp.Person' encountered. Only primitive .NET types and their corresponding enumerables can be used as query parameters.")
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
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
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net60
                }

            }.RunAsync();
        }

        public void Dispose()
        {
            File.Delete(Path.Combine("TestServerlessApp", "serverless.template"));
        }
    }
}