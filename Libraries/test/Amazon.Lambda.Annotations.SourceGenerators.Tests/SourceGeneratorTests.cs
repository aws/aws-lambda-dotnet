using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
                    }
                }
            }.RunAsync();

            var expectedTemplateContent = File.ReadAllText(Path.Combine("Snapshots", "ServerlessTemplates", "greeter.template"));
            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            VerifyServerlessTemplateSnapshot(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task SimpleCalculator()
        {
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
                        )

                    }
                }
            }.RunAsync();

            var expectedTemplateContent = File.ReadAllText(Path.Combine("Snapshots", "ServerlessTemplates", "simpleCalculator.template"));
            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            VerifyServerlessTemplateSnapshot(expectedTemplateContent, actualTemplateContent);
        }

        [Fact]
        public async Task ComplexCalculator()
        {
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
                    }
                }
            }.RunAsync();

            var expectedTemplateContent = File.ReadAllText(Path.Combine("Snapshots", "ServerlessTemplates", "complexCalculator.template"));
            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            VerifyServerlessTemplateSnapshot(expectedTemplateContent, actualTemplateContent);
        }

        private void VerifyServerlessTemplateSnapshot(string expectedTemplateContent, string actualTemplateContent)
        {
            expectedTemplateContent = expectedTemplateContent.Replace("\r\n", Environment.NewLine).
                Replace("\n", Environment.NewLine).
                Replace("\r\r\n", Environment.NewLine);
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }
    }
}