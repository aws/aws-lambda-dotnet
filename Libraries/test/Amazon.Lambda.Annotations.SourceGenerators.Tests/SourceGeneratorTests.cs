using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using VerifyCS = Amazon.Lambda.Annotations.SourceGenerators.Tests.CSharpSourceGeneratorVerifier<Amazon.Lambda.Annotations.SourceGenerator.Generator>;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class SourceGeneratorTests
    {
        [Fact]
        public async Task Greeter()
        {
            var expectedTemplateContent = NormalizeNewLines(File.ReadAllText(Path.Combine("Snapshots", "ServerlessTemplates", "greeter.template")));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "Greeter.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "Greeter.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "Greeter_SayHello_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "Greeter_SayHello_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Greeter_SayHelloAsync_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "Greeter_SayHelloAsync_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Greeter_SayHello_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "Greeter_SayHello_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Greeter_SayHelloAsync_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "Greeter_SayHelloAsync_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task SimpleCalculator()
        {
            var expectedTemplateContent = NormalizeNewLines(File.ReadAllText(Path.Combine("Snapshots", "ServerlessTemplates", "simpleCalculator.template")));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {

                        (Path.Combine("TestServerlessApp", "SimpleCalculator.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "SimpleCalculator.cs"))),
                        (Path.Combine("TestServerlessApp", "Startup.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "Startup.cs"))),
                        (Path.Combine("TestServerlessApp", "Services", "SimpleCalculatorService.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "Services", "SimpleCalculatorService.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "FromServicesAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "FromServicesAttribute.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Add_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Add_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Subtract_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Subtract_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Multiply_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Multiply_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_DivideAsync_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_DivideAsync_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Pi_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Pi_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Random_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Random_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Randoms_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Randoms_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Add_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Add_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Subtract_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Subtract_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Multiply_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Multiply_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_DivideAsync_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_DivideAsync_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Pi_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Pi_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Random_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Random_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("SimpleCalculator_Randoms_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "SimpleCalculator_Randoms_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(NormalizeNewLines(expectedTemplateContent), actualTemplateContent);
        }

        [Fact]
        public async Task ComplexCalculator()
        {
            var expectedTemplateContent = NormalizeNewLines(File.ReadAllText(Path.Combine("Snapshots", "ServerlessTemplates", "complexCalculator.template")));

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {

                        (Path.Combine("TestServerlessApp", "ComplexCalculator.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "ComplexCalculator.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "ComplexCalculator_Add_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "ComplexCalculator_Add_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "ComplexCalculator_Subtract_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Snapshots", "ComplexCalculator_Subtract_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("ComplexCalculator_Add_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "ComplexCalculator_Add_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("ComplexCalculator_Subtract_Generated.g.cs", File.ReadAllText(Path.Combine("Snapshots", "ComplexCalculator_Subtract_Generated.g.cs"))),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(NormalizeNewLines(expectedTemplateContent), actualTemplateContent);
        }

        private string NormalizeNewLines(string expectedTemplateContent)
        {
            return expectedTemplateContent.Replace("\r\n", Environment.NewLine).
                Replace("\n", Environment.NewLine).
                Replace("\r\r\n", Environment.NewLine);
        }
    }
}