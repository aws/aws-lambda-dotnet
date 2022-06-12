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
            var expectedMessageHandlerAsyncGenerated = File.ReadAllText(Path.Combine("Snapshots", "Messaging_MessageHandler_Generated.g.cs")).ToEnvironmentLineEndings();

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
                            "Messaging_MessageHandler_Generated.g.cs",
                            SourceText.From(expectedMessageHandlerAsyncGenerated, Encoding.UTF8, SourceHashAlgorithm.Sha256)
                        )
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments("Messaging_MessageHandler_Generated.g.cs", expectedMessageHandlerAsyncGenerated),
                        new DiagnosticResult("AWSLambda0103", DiagnosticSeverity.Info).WithArguments($"TestServerlessApp{Path.DirectorySeparatorChar}serverless.template", expectedTemplateContent)
                    }
                }
                }.RunAsync();

            }
            catch (EqualWithMessageException e)
            {
                // the test result sucks to see what's wrong, re-assert the expected vs actual
                // to get a reasonably readable result
                //
                // e.g.
                //
                /*
 MessagingTest
   Source: SourceGeneratorTests.Messaging.cs line 19
   Duration: 1.7 sec

  Message: 
Assert.Equal() Failure
                                 ↓ (pos 80)
Expected: ···verless.template", "    {\r\n  \"AWSTemplateFormatVersion\": ···
Actual:   ···verless.template", "{\r\n  \"AWSTemplateFormatVersion\": \"20···
                                 ↑ (pos 80)

  Stack Trace: 
SourceGeneratorTests.Messaging() line 55
--- End of stack trace from previous location where exception was thrown ---
                 */
                Assert.Equal(e.Expected, e.Actual);
            }
            var actualTemplateContent = File.ReadAllText(Path.Combine("TestServerlessApp", "serverless.template"));
            Assert.Equal(expectedTemplateContent, actualTemplateContent);
        }

    }
}
