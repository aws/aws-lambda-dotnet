using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public Generator()
        {
#if DEBUG
            //if (!Debugger.IsAttached)
            //{
            //    Debugger.Launch();
            //}
#endif
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // retrieve the populated receiver
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            {
                return;
            }

            var annotationReport = new AnnotationReport();
            var fileSystem = new FileSystem();
            var templateFinder = new CloudFormationTemplateFinder(fileSystem);
            var projectRootDirectory = string.Empty;

            foreach (var lambdaFunction in receiver.LambdaFunctions)
            {
                var lambdaFunctionModel = new LambdaFunctionModel();
                // the lambdaFunctionModel property values will be set based on the source generator refactor that Ganesh is working on as per
                // https://github.com/aws/aws-lambda-dotnet/pull/931
                
                var codeGenerator = new LambdaFunctionCodeGenerator(lambdaFunction, receiver.StartupClass, context);
                var (hint, sourceText) = codeGenerator.GenerateSource();
                context.AddSource(hint, sourceText);
                
                annotationReport.LambdaFunctions.Add(lambdaFunctionModel);
                
                if (string.IsNullOrEmpty(projectRootDirectory))
                    projectRootDirectory = templateFinder.DetermineProjectRootDirectory(lambdaFunction.SyntaxTree.FilePath);
            }

            annotationReport.CloudFormationTemplatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(fileSystem);
            cloudFormationJsonWriter.ApplyReport(annotationReport);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }
    }
}
