using System;
using System.Linq;
using System.Text;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
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
        private readonly IFileManager _fileManager = new FileManager();
        private readonly IDirectoryManager _directoryManager = new DirectoryManager();

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
            var diagnosticReporter = new DiagnosticReporter(context);

            try
            {
                // retrieve the populated receiver
                if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
                {
                    return;
                }

                // Check to see if any of the current syntax trees has any error diagnostics. If so
                // Skip generation. We only want to sync the CloudFormation template if the project
                // can compile.
                foreach(var syntaxTree in context.Compilation.SyntaxTrees)
                {
                    if(syntaxTree.GetDiagnostics().Any(x => x.Severity == DiagnosticSeverity.Error))
                    {
                        return;
                    }
                }


                // If no project directory was detected then skip the generator.
                // This is most likely to happen when the project is empty and doesn't have any classes in it yet.
                if(string.IsNullOrEmpty(receiver.ProjectDirectory))
                {
                    return;
                }

                var semanticModelProvider = new SemanticModelProvider(context);
                if (receiver.StartupClasses.Count > 1)
                {
                    foreach (var startup in receiver.StartupClasses)
                    {
                        // If there are more than one startup class, report them as errors
                        diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MultipleStartupNotAllowed,
                            Location.Create(startup.SyntaxTree, startup.Span),
                            startup.SyntaxTree.FilePath));
                    }
                }

                var configureMethodModel = semanticModelProvider.GetConfigureMethodModel(receiver.StartupClasses.FirstOrDefault());

                var annotationReport = new AnnotationReport();

                var templateHandler = new CloudFormationTemplateHandler(_fileManager, _directoryManager);

                foreach (var lambdaMethod in receiver.LambdaMethods)
                {
                    var lambdaMethodModel = semanticModelProvider.GetMethodSemanticModel(lambdaMethod);

                    // Check for necessary references
                    if (lambdaMethodModel.HasAttribute(context, TypeFullNames.RestApiAttribute)
                        || lambdaMethodModel.HasAttribute(context, TypeFullNames.HttpApiAttribute))
                    {
                        // Check for arbitrary type from "Amazon.Lambda.APIGatewayEvents"
                        if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.APIGatewayEvents") == null)
                        {
                            diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MissingDependencies,
                                lambdaMethod.GetLocation(),
                                "Amazon.Lambda.APIGatewayEvents"));
                        }
                    }

                    var model = LambdaFunctionModelBuilder.Build(lambdaMethodModel, configureMethodModel, context);

                    // If there are more than one event, report them as errors
                    if (model.LambdaMethod.Events.Count > 1)
                    {
                        foreach (var attribute in lambdaMethodModel.GetAttributes().Where(attribute => TypeFullNames.Events.Contains(attribute.AttributeClass.ToDisplayString())))
                        {
                            diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MultipleEventsNotSupported,
                                Location.Create(attribute.ApplicationSyntaxReference.SyntaxTree, attribute.ApplicationSyntaxReference.Span),
                                DiagnosticSeverity.Error));
                        }

                        // Skip multi-event lambda method from processing and check remaining lambda methods for diagnostics
                        continue;
                    }

                    var template = new LambdaFunctionTemplate(model);
                    var sourceText = template.TransformText().ToEnvironmentLineEndings();
                    context.AddSource($"{model.GeneratedMethod.ContainingType.Name}.g.cs", SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));

                    // report every generated file to build output
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.CodeGeneration, Location.None, $"{model.GeneratedMethod.ContainingType.Name}.g.cs", sourceText));

                    annotationReport.LambdaFunctions.Add(model);
                }

                // Run the CloudFormation sync if any LambdaMethods exists. Also run if no LambdaMethods exists but there is a
                // CloudFormation template in case orphaned functions in the template need to be removed.
                // Both checks are required because if there is no template but there are LambdaMethods the CF template the template will be created.
                if (receiver.LambdaMethods.Any() || templateHandler.DoesTemplateExist(receiver.ProjectDirectory))
                {
                    annotationReport.CloudFormationTemplatePath = templateHandler.FindTemplate(receiver.ProjectDirectory);
                    annotationReport.ProjectRootDirectory = receiver.ProjectDirectory;
                    annotationReport.IsTelemetrySuppressed = ProjectFileHandler.IsTelemetrySuppressed(receiver.ProjectPath, _fileManager);

                    var templateFormat = templateHandler.DetermineTemplateFormat(annotationReport.CloudFormationTemplatePath);
                    ITemplateWriter templateWriter;
                    if (templateFormat == CloudFormationTemplateFormat.Json)
                    {
                        templateWriter = new JsonWriter();
                    }
                    else
                    {
                        templateWriter = new YamlWriter();
                    }
                    var cloudFormationWriter = new CloudFormationWriter(_fileManager, _directoryManager, templateWriter, diagnosticReporter);
                    cloudFormationWriter.ApplyReport(annotationReport);
                }

            }
            catch (Exception e)
            {
                // this is a generator failure, report this as error
                diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.UnhandledException, Location.None, e.PrettyPrint()));
#if DEBUG
                throw;
#endif
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver(_fileManager, _directoryManager));
        }
    }
}