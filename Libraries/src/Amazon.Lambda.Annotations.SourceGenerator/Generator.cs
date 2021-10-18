using System.Diagnostics;
using System.Text;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Templates;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public Generator()
        {
#if DEBUG
            // if (!Debugger.IsAttached)
            // {
            //     Debugger.Launch();
            // }
#endif
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // retrieve the populated receiver
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            {
                return;
            }

            var semanticModelProvider = new SemanticModelProvider(context);
            var configureMethodModel = semanticModelProvider.GetConfigureMethodModel(receiver.StartupClass);

            var annotationReport = new AnnotationReport();
            var templateFinder = new CloudFormationTemplateFinder(new FileManager(), new DirectoryManager());
            var projectRootDirectory = string.Empty;

            foreach (var lambdaMethod in receiver.LambdaMethods)
            {
                var lambdaMethodModel = semanticModelProvider.GetMethodSemanticModel(lambdaMethod);
                var model = LambdaFunctionModelBuilder.Build(lambdaMethodModel, configureMethodModel, context);
                var template = new LambdaFunctionTemplate(model);
                var sourceText = template.TransformText();
                context.AddSource($"{model.GeneratedMethod.ContainingType.Name}.g.cs", SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));

                annotationReport.LambdaFunctions.Add(model);

                if (string.IsNullOrEmpty(projectRootDirectory))
                    projectRootDirectory = templateFinder.DetermineProjectRootDirectory(lambdaMethod.SyntaxTree.FilePath);
            }

            annotationReport.CloudFormationTemplatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(new FileManager(), new JsonWriter());
            cloudFormationJsonWriter.ApplyReport(annotationReport);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }
    }
}