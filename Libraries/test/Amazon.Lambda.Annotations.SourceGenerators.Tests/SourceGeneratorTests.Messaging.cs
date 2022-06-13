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
using Xunit.Sdk;
using VerifyCS = Amazon.Lambda.Annotations.SourceGenerators.Tests.CSharpSourceGeneratorVerifier<Amazon.Lambda.Annotations.SourceGenerator.Generator>;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public partial class SourceGeneratorTests
    {
        [Fact(DisplayName = nameof(MessageHandlerForPreExistingQueue))]
        public async Task MessageHandlerForPreExistingQueue()
        {
            var expectedTemplateContent = File.ReadAllText(Path.Combine("Snapshots", "ServerlessTemplates", "messaging.template")).ToEnvironmentLineEndings();
            var expectedMessageHandlerForPreExistingQueueGenerated = File.ReadAllText(Path.Combine("Snapshots", "Messaging_MessageHandlerForPreExistingQueue_Generated.g.cs")).ToEnvironmentLineEndings();
            var expectedMessageHandlerForNewQueueGenerated = File.ReadAllText(Path.Combine("Snapshots", "Messaging_MessageHandlerForNewQueue_Generated.g.cs")).ToEnvironmentLineEndings();

            try
            {
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
                                "Messaging_MessageHandlerForPreExistingQueue_Generated.g.cs",
                                SourceText.From(expectedMessageHandlerForPreExistingQueueGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                            ),
                            (
                                typeof(SourceGenerator.Generator),
                                "Messaging_MessageHandlerForNewQueue_Generated.g.cs",
                                SourceText.From(expectedMessageHandlerForNewQueueGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                            )
                        },
                        ExpectedDiagnostics =
                        {
                            new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Messaging_MessageHandlerForPreExistingQueue_Generated.g.cs", expectedMessageHandlerForPreExistingQueueGenerated),
                            new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Messaging_MessageHandlerForNewQueue_Generated.g.cs", expectedMessageHandlerForNewQueueGenerated),
                            new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                        }
                    }
                }.RunAsync();

            }
            finally
            {
                var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
                Assert.Equal(expectedTemplateContent, actualTemplateContent);
            }
        }
    }
}
