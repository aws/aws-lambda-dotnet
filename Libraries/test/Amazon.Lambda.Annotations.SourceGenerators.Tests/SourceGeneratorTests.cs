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

                        ("Greeter.cs", File.ReadAllText(Path.Combine("TestServerlessApp", "Greeter.cs"))),
                        ("LambdaFunctionAttribute.cs", File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        ("LambdaStartupAttribute.cs", File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "Greeter_SayHello_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Generated", "Greeter_SayHello_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "Greeter_SayHelloAsync_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Generated", "Greeter_SayHelloAsync_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    }
                }
            }.RunAsync();
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

                        ("SimpleCalculator.cs", File.ReadAllText(Path.Combine("TestServerlessApp", "SimpleCalculator.cs"))),
                        ("Startup.cs", File.ReadAllText(Path.Combine("TestServerlessApp", "Startup.cs"))),
                        ("Services.SimpleCalculatorService.cs", File.ReadAllText(Path.Combine("TestServerlessApp", "Services", "SimpleCalculatorService.cs"))),
                        ("LambdaFunctionAttribute.cs", File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        ("LambdaStartupAttribute.cs", File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                        ("FromServicesAttribute.cs", File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "FromServicesAttribute.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Add_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Generated", "SimpleCalculator_Add_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Subtract_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Generated", "SimpleCalculator_Subtract_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_Multiply_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Generated", "SimpleCalculator_Multiply_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        ),
                        (
                            typeof(SourceGenerator.Generator),
                            "SimpleCalculator_DivideAsync_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Generated", "SimpleCalculator_DivideAsync_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )

                    }
                }
            }.RunAsync();
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

                        ("ComplexCalculator.cs", File.ReadAllText(Path.Combine("TestServerlessApp", "ComplexCalculator.cs"))),
                        ("LambdaFunctionAttribute.cs", File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        ("LambdaStartupAttribute.cs", File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "ComplexCalculator_Add_Generated.g.cs",
                            SourceText.From(File.ReadAllText(Path.Combine("Generated", "ComplexCalculator_Add_Generated.g.cs")), Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    }
                }
            }.RunAsync();
        }
    }
}