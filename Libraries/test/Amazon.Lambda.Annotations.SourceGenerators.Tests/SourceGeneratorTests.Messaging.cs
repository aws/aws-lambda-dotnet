using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using VerifyCS = Amazon.Lambda.Annotations.SourceGenerators.Tests.CSharpSourceGeneratorVerifier<Amazon.Lambda.Annotations.SourceGenerator.Generator>;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public partial class SourceGeneratorTests
    {
        [Fact(DisplayName = "MessagingTest")]
        public async Task Messaging()
        {
            var expectedTemplateContent = File.ReadAllText(Path.Combine("Snapshots", "ServerlessTemplates", "messaging.template")).ToEnvironmentLineEndings();
            var expectedMessageHandlerAsyncGenerated = File.ReadAllText(Path.Combine("Snapshots", "Messaging_MessageHandlerForPreExisingQueue_Generated.g.cs")).ToEnvironmentLineEndings();

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        (Path.Combine("TestServerlessApp", "Messaging.cs"), File.ReadAllText(Path.Combine("TestServerlessApp", "Messaging.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaFunctionAttribute.cs"))),
                        (Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"), File.ReadAllText(Path.Combine("Amazon.Lambda.Annotations", "LambdaStartupAttribute.cs"))),
                    },
                    GeneratedSources =
                    {
                        (
                            typeof(SourceGenerator.Generator),
                            "Messaging_MessageHandlerForPreExisingQueue_Generated.g.cs",
                            SourceText.From(expectedMessageHandlerAsyncGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Messaging_MessageHandlerForPreExisingQueue_Generated.g.cs", expectedMessageHandlerAsyncGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
            }.RunAsync();

            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

    }
}
