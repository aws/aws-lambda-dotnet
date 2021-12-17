using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Templates;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        private readonly IFileManager _fileManager = new FileManager();
        private readonly IDirectoryManager _directoryManager = new DirectoryManager();
        private readonly IJsonWriter _jsonWriter = new JsonWriter();

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

                // If there are no Lambda methods, return early
                if (!receiver.LambdaMethods.Any())
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

                var templateFinder = new CloudFormationTemplateFinder(_fileManager, _directoryManager);
                var projectRootDirectory = string.Empty;

                foreach (var lambdaMethod in receiver.LambdaMethods)
                {
                    var lambdaMethodModel = semanticModelProvider.GetMethodSemanticModel(lambdaMethod);
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

                    if (string.IsNullOrEmpty(projectRootDirectory))
                        projectRootDirectory = templateFinder.DetermineProjectRootDirectory(lambdaMethod.SyntaxTree.FilePath);
                }

                annotationReport.CloudFormationTemplatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);
                annotationReport.ProjectRootDirectory = projectRootDirectory;
                var cloudFormationJsonWriter = new CloudFormationJsonWriter(_fileManager, _directoryManager,_jsonWriter, diagnosticReporter);
                cloudFormationJsonWriter.ApplyReport(annotationReport);
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
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }
    }
}