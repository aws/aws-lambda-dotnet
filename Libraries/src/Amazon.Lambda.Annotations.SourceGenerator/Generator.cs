using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Templates;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        private readonly IFileManager _fileManager = new FileManager();
        private readonly IDirectoryManager _directoryManager = new DirectoryManager();

        /// <summary>
        /// Maps .NET TargetFramework values to the corresponding Lambda runtime CloudFormation value
        /// </summary>
        internal static readonly Dictionary<string, string> _targetFrameworksToRuntimes = new Dictionary<string, string>(2)
        {
            { "net6.0", "dotnet6" },
            { "net8.0", "dotnet8" },
            { "net10.0", "dotnet10" }
        };

        internal static readonly List<string> _allowedRuntimeValues = new List<string>(4)
        {
            "dotnet6",
            "provided.al2",
            "provided.al2023",
            "dotnet8",
            "dotnet10"
        };

        public Generator()
        {
#if DEBUG
          //  if (!Debugger.IsAttached)
          //  {
          //      Debugger.Launch();
          //  }
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
                foreach (var syntaxTree in context.Compilation.SyntaxTrees)
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

                var isExecutable = false;

                bool foundFatalError = false;

                var assemblyAttributes = context.Compilation.Assembly.GetAttributes();

                var globalPropertiesAttribute = assemblyAttributes
                    .FirstOrDefault(attr => attr.AttributeClass.Name == nameof(LambdaGlobalPropertiesAttribute));

                var defaultRuntime = "dotnet6";

                // Try to determine the target framework from the source generator context
                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.TargetFramework", out var targetFramework))
                {
                    if (_targetFrameworksToRuntimes.ContainsKey(targetFramework))
                    {
                        defaultRuntime = _targetFrameworksToRuntimes[targetFramework];
                    }
                }

                // The runtime specified in the global property has precedence over the one we determined from the TFM (if we did)
                if (globalPropertiesAttribute != null)
                {
                    var generateMain = globalPropertiesAttribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "GenerateMain").Value;
                    var runtimeAttributeValue = globalPropertiesAttribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Runtime").Value;
                    var runtime = runtimeAttributeValue.Value == null ? defaultRuntime : runtimeAttributeValue.Value.ToString();

                    if (!_allowedRuntimeValues.Contains(runtime))
                    {
                        diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.InvalidRuntimeSelection, Location.None));
                        return;
                    }

                    defaultRuntime = runtime;

                    isExecutable = generateMain.Value != null && bool.Parse(generateMain.Value.ToString());

                    if (isExecutable && context.Compilation.Options.OutputKind != OutputKind.ConsoleApplication)
                    {
                        diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.SetOutputTypeExecutable, Location.None));
                        return;
                    }
                }

                var configureHostBuilderMethodSymbol = semanticModelProvider.GetConfigureHostBuilderMethodModel(receiver.StartupClasses.FirstOrDefault());
                var configureServicesMethodSymbol = semanticModelProvider.GetConfigureServicesMethodModel(receiver.StartupClasses.FirstOrDefault());
                var configureMethodSymbol = configureServicesMethodSymbol;

                if (configureServicesMethodSymbol != null && configureHostBuilderMethodSymbol != null)
                {
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MultipleConfigureMethodsNotAllowed, configureServicesMethodSymbol.Locations.FirstOrDefault(), configureServicesMethodSymbol.Name, configureHostBuilderMethodSymbol.Name));
                }

                if (configureHostBuilderMethodSymbol != null)
                {
                    configureMethodSymbol = configureHostBuilderMethodSymbol;
                }

                // Check for methods that have secondary attributes (HttpApi, RestApi, HttpApiAuthorizer, etc.)
                // but are missing the required [LambdaFunction] attribute
                foreach (var (method, attributeName) in receiver.MethodsWithMissingLambdaFunction)
                {
                    diagnosticReporter.Report(Diagnostic.Create(
                        DiagnosticDescriptors.MissingLambdaFunctionAttribute,
                        Location.Create(method.SyntaxTree, method.Span),
                        attributeName));
                    foundFatalError = true;
                }

                var annotationReport = new AnnotationReport();

                var templateHandler = new CloudFormationTemplateHandler(_fileManager, _directoryManager);

                var lambdaModels = new List<LambdaFunctionModel>();

                foreach (var lambdaMethodDeclarationSyntax in receiver.LambdaMethods)
                {
                    var lambdaMethodSymbol = semanticModelProvider.GetMethodSemanticModel(lambdaMethodDeclarationSyntax);
                    var lambdaMethodLocation = lambdaMethodDeclarationSyntax.GetLocation();

                    var lambdaFunctionModel = LambdaFunctionModelBuilder.BuildAndValidate(lambdaMethodSymbol, lambdaMethodLocation, configureMethodSymbol, context, isExecutable, defaultRuntime, diagnosticReporter);
                    if (!lambdaFunctionModel.IsValid)
                    {
                        // If the model is not valid then skip it from further processing
                        foundFatalError = true;
                        continue;
                    }

                    // Check for authorizer attributes on this Lambda function
                    var authorizerModel = ExtractAuthorizerModel(lambdaMethodSymbol, lambdaFunctionModel.ResourceName);
                    if (authorizerModel != null)
                    {
                        annotationReport.Authorizers.Add(authorizerModel);
                    }

                    var template = new LambdaFunctionTemplate(lambdaFunctionModel);

                    string sourceText;
                    try
                    {
                        sourceText = template.TransformText().ToEnvironmentLineEndings();
                        context.AddSource($"{lambdaFunctionModel.GeneratedMethod.ContainingType.Name}.g.cs", SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));
                    }
                    catch (Exception e) when (e is NotSupportedException || e is InvalidOperationException)
                    {
                        diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.CodeGenerationFailed, Location.Create(lambdaMethodDeclarationSyntax.SyntaxTree, lambdaMethodDeclarationSyntax.Span), e.Message));
                        return;
                    }

                    // report every generated file to build output
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.CodeGeneration, Location.None, $"{lambdaFunctionModel.GeneratedMethod.ContainingType.Name}.g.cs", sourceText));

                    lambdaModels.Add(lambdaFunctionModel);
                    annotationReport.LambdaFunctions.Add(lambdaFunctionModel);
                }

                if (isExecutable)
                {
                    var executableAssembly = GenerateExecutableAssemblySource(
                        context,
                        diagnosticReporter,
                        receiver,
                        lambdaModels);

                    if (executableAssembly == null)
                    {
                        foundFatalError = true;
                        return;
                    }

                    context.AddSource("Program.g.cs", SourceText.From(executableAssembly.TransformText().ToEnvironmentLineEndings(), Encoding.UTF8, SourceHashAlgorithm.Sha256));
                }

                // Validate authorizer models and references before syncing CloudFormation template
                foreach (var authorizerModel in annotationReport.Authorizers)
                {
                    var attributeName = authorizerModel.AuthorizerType == AuthorizerType.HttpApi
                        ? "HttpApiAuthorizer"
                        : "RestApiAuthorizer";
                    if (!ValidateAuthorizerModel(authorizerModel, attributeName, Location.None, diagnosticReporter))
                    {
                        foundFatalError = true;
                    }
                }

                if (!ValidateAuthorizerReferences(annotationReport, diagnosticReporter))
                {
                    foundFatalError = true;
                }

                // Run the CloudFormation sync if any LambdaMethods exists. Also run if no LambdaMethods exists but there is a
                // CloudFormation template in case orphaned functions in the template need to be removed.
                // Both checks are required because if there is no template but there are LambdaMethods the CF template the template will be created.
                if (!foundFatalError && (receiver.LambdaMethods.Any() || templateHandler.DoesTemplateExist(receiver.ProjectDirectory)))
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

        private static ExecutableAssembly GenerateExecutableAssemblySource(
            GeneratorExecutionContext context,
            DiagnosticReporter diagnosticReporter,
            SyntaxReceiver receiver,
            List<LambdaFunctionModel> lambdaModels)
        {
            // Check 'Amazon.Lambda.RuntimeSupport' is referenced
            if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.RuntimeSupport") == null)
            {
                diagnosticReporter.Report(
                    Diagnostic.Create(
                        DiagnosticDescriptors.MissingDependencies,
                        Location.None,
                        "Amazon.Lambda.RuntimeSupport"));

                return null;
            }

            foreach (var methodDeclaration in receiver.MethodDeclarations)
            {
                var model = context.Compilation.GetSemanticModel(methodDeclaration.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;

                // Check to see if a static main method exists in the same namespace that has 0 or 1 parameters
                if (symbol.Name != "Main" || !symbol.IsStatic || symbol.ContainingNamespace.Name != lambdaModels[0].LambdaMethod.ContainingAssembly || (symbol.Parameters.Length > 1))
                    continue;

                diagnosticReporter.Report(
                    Diagnostic.Create(
                        DiagnosticDescriptors.MainMethodExists,
                        Location.None));

                return null;
            }

            if (lambdaModels.Count == 0)
            {
                diagnosticReporter.Report(
                    Diagnostic.Create(
                        DiagnosticDescriptors.ExecutableWithNoFunctions,
                        Location.None,
                        "Amazon.Lambda.Annotations"));

                return null;
            }

            return new ExecutableAssembly(
                lambdaModels,
                lambdaModels[0].LambdaMethod.ContainingNamespace);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver(_fileManager, _directoryManager));
        }

        /// <summary>
        /// Extracts authorizer model from method symbol if it has HttpApiAuthorizer or RestApiAuthorizer attribute.
        /// </summary>
        /// <param name="methodSymbol">The method symbol to check for authorizer attributes</param>
        /// <param name="lambdaResourceName">The CloudFormation resource name for the Lambda function</param>
        /// <returns>AuthorizerModel if an authorizer attribute is found, null otherwise</returns>
        private static AuthorizerModel ExtractAuthorizerModel(IMethodSymbol methodSymbol, string lambdaResourceName)
        {
            foreach (var attribute in methodSymbol.GetAttributes())
            {
                var attributeFullName = attribute.AttributeClass?.ToDisplayString();

                if (attributeFullName == TypeFullNames.HttpApiAuthorizerAttribute)
                {
                    return HttpApiAuthorizerAttributeBuilder.BuildModel(attribute, lambdaResourceName);
                }

                if (attributeFullName == TypeFullNames.RestApiAuthorizerAttribute)
                {
                    return RestApiAuthorizerAttributeBuilder.BuildModel(attribute, lambdaResourceName);
                }
            }

            return null;
        }

        /// <summary>
        /// Validates an authorizer model.
        /// </summary>
        /// <param name="model">The authorizer model to validate</param>
        /// <param name="attributeName">The name of the attribute for error messages</param>
        /// <param name="methodLocation">The location of the method for diagnostic reporting</param>
        /// <param name="diagnosticReporter">The diagnostic reporter for validation errors</param>
        /// <returns>True if valid, false otherwise</returns>
        private static bool ValidateAuthorizerModel(AuthorizerModel model, string attributeName, Location methodLocation, DiagnosticReporter diagnosticReporter)
        {
            var isValid = true;

            // Validate Name is provided
            if (string.IsNullOrEmpty(model.Name))
            {
                diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.AuthorizerMissingName, methodLocation, attributeName));
                isValid = false;
            }

            // Validate ResultTtlInSeconds
            if (model.ResultTtlInSeconds < 0 || model.ResultTtlInSeconds > 3600)
            {
                diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.InvalidAuthorizerResultTtl, methodLocation, model.ResultTtlInSeconds.ToString()));
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// Validates authorizer references in lambda functions.
        /// </summary>
        /// <param name="annotationReport">The annotation report containing all functions and authorizers</param>
        /// <param name="diagnosticReporter">The diagnostic reporter for validation errors</param>
        /// <returns>True if all authorizer references are valid, false otherwise</returns>
        private static bool ValidateAuthorizerReferences(AnnotationReport annotationReport, DiagnosticReporter diagnosticReporter)
        {
            var isValid = true;

            // Build lookups for authorizers by type
            var httpApiAuthorizers = annotationReport.Authorizers
                .Where(a => a.AuthorizerType == AuthorizerType.HttpApi)
                .ToDictionary(a => a.Name, a => a);
            var restApiAuthorizers = annotationReport.Authorizers
                .Where(a => a.AuthorizerType == AuthorizerType.RestApi)
                .ToDictionary(a => a.Name, a => a);

            // Check for duplicate authorizer names within the same API type
            var httpApiAuthorizerNames = annotationReport.Authorizers
                .Where(a => a.AuthorizerType == AuthorizerType.HttpApi)
                .GroupBy(a => a.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var duplicateName in httpApiAuthorizerNames)
            {
                diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.DuplicateAuthorizerName, Location.None, duplicateName));
                isValid = false;
            }

            var restApiAuthorizerNames = annotationReport.Authorizers
                .Where(a => a.AuthorizerType == AuthorizerType.RestApi)
                .GroupBy(a => a.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var duplicateName in restApiAuthorizerNames)
            {
                diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.DuplicateAuthorizerName, Location.None, duplicateName));
                isValid = false;
            }

            // Validate authorizer references in functions
            foreach (var function in annotationReport.LambdaFunctions)
            {
                var authorizerName = function.Authorizer;
                if (string.IsNullOrEmpty(authorizerName))
                {
                    continue;
                }

                // Check if this function uses HttpApi or RestApi
                var usesHttpApi = function.Attributes.Any(a => a is AttributeModel<HttpApiAttribute>);
                var usesRestApi = function.Attributes.Any(a => a is AttributeModel<RestApiAttribute>);

                if (usesHttpApi)
                {
                    if (!httpApiAuthorizers.ContainsKey(authorizerName))
                    {
                        // Check if it exists as a REST API authorizer (type mismatch)
                        if (restApiAuthorizers.ContainsKey(authorizerName))
                        {
                            diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.HttpApiAuthorizerTypeMismatch, Location.None, authorizerName));
                        }
                        else
                        {
                            diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.HttpApiAuthorizerNotFound, Location.None, authorizerName));
                        }
                        isValid = false;
                    }
                    else
                    {
                        // Check for payload format version mismatch between authorizer and endpoint
                        var matchedAuthorizer = httpApiAuthorizers[authorizerName];
                        var httpApiAttr = function.Attributes
                            .OfType<AttributeModel<HttpApiAttribute>>()
                            .Select(a => a.Data)
                            .FirstOrDefault();

                        if (httpApiAttr != null)
                        {
                            var endpointVersion = httpApiAttr.Version;
                            var authorizerVersion = matchedAuthorizer.AuthorizerPayloadFormatVersion;

                            // Compare: HttpApiVersion.V1 corresponds to AuthorizerPayloadFormatVersion.V1, etc.
                            if ((endpointVersion == HttpApiVersion.V1 && authorizerVersion != AuthorizerPayloadFormatVersion.V1) ||
                                (endpointVersion == HttpApiVersion.V2 && authorizerVersion != AuthorizerPayloadFormatVersion.V2))
                            {
                                var authorizerVersionStr = authorizerVersion == AuthorizerPayloadFormatVersion.V1 ? "1.0" : "2.0";
                                var endpointVersionStr = endpointVersion == HttpApiVersion.V1 ? "V1" : "V2";
                                diagnosticReporter.Report(Diagnostic.Create(
                                    DiagnosticDescriptors.AuthorizerPayloadVersionMismatch,
                                    Location.None,
                                    authorizerName,
                                    authorizerVersionStr,
                                    endpointVersionStr));
                            }
                        }
                    }
                }

                if (usesRestApi)
                {
                    if (!restApiAuthorizers.ContainsKey(authorizerName))
                    {
                        // Check if it exists as an HTTP API authorizer (type mismatch)
                        if (httpApiAuthorizers.ContainsKey(authorizerName))
                        {
                            diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.RestApiAuthorizerTypeMismatch, Location.None, authorizerName));
                        }
                        else
                        {
                            diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.RestApiAuthorizerNotFound, Location.None, authorizerName));
                        }
                        isValid = false;
                    }
                }
            }

            return isValid;
        }
    }
}
