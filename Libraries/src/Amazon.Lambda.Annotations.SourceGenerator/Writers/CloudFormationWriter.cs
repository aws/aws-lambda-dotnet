using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SQS;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using AuthorizerType = Amazon.Lambda.Annotations.SourceGenerator.Models.AuthorizerType;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    /// <summary>
    /// This class contains methods to manipulate the AWS serverless template.
    /// It takes the metadata captured by <see cref="AnnotationReport"/> and writes it to the AWS SAM template.
    /// <see href="https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/sam-resource-function.html">see here</see> to know more about configurable properties for AWS::Serverless::Function
    /// <see href="https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/test/TestServerlessApp/serverless.template">see here</see> for an actual serverless template.
    /// </summary>
    public class CloudFormationWriter : IAnnotationReportWriter
    {
        private const string CREATION_TOOL = "Amazon.Lambda.Annotations";
        private const string PARAMETERS = "Parameters";
        private const string GET_ATTRIBUTE = "Fn::GetAtt";
        private const string REF = "Ref";
        private const string HTTP_API_RESOURCE_NAME = "AnnotationsHttpApi";
        private const string REST_API_RESOURCE_NAME = "AnnotationsRestApi";

        // Constants related to the message we append to the CloudFormation template description
        private const string BASE_DESCRIPTION = "This template is partially managed by Amazon.Lambda.Annotations";
        private const string END_OF_VESRION_IN_DESCRIPTION = ").";

        private readonly IFileManager _fileManager;
        private readonly IDirectoryManager _directoryManager;
        private readonly ITemplateWriter _templateWriter;
        private readonly IDiagnosticReporter _diagnosticReporter;

        public CloudFormationWriter(IFileManager fileManager, IDirectoryManager directoryManager, ITemplateWriter templateWriter, IDiagnosticReporter diagnosticReporter)
        {
            _fileManager = fileManager;
            _directoryManager = directoryManager;
            _diagnosticReporter = diagnosticReporter;
            _templateWriter = templateWriter;
        }

        /// <summary>
        /// It takes the metadata captured by <see cref="AnnotationReport"/> and writes it to the AWS SAM template.
        /// </summary>
        public void ApplyReport(AnnotationReport report)
        {
            var originalContent = _fileManager.ReadAllText(report.CloudFormationTemplatePath);
            var templateDirectory = _directoryManager.GetDirectoryName(report.CloudFormationTemplatePath);
            var relativeProjectUri = _directoryManager.GetRelativePath(templateDirectory, report.ProjectRootDirectory);

            if (string.IsNullOrEmpty(originalContent))
                CreateNewTemplate();
            else
                _templateWriter.Parse(originalContent);

            ProcessTemplateDescription(report);

            // Build authorizer lookup for processing events with Auth configuration
            var authorizerLookup = new Dictionary<string, AuthorizerModel>(StringComparer.Ordinal);
            foreach (var authorizer in report.Authorizers)
            {
                if (authorizerLookup.ContainsKey(authorizer.Name))
                {
                    _diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.DuplicateAuthorizerName, Location.None, authorizer.Name));
                    continue;
                }
                authorizerLookup[authorizer.Name] = authorizer;
            }

            // Process authorizers first (they need to exist before functions reference them)
            ProcessAuthorizers(report.Authorizers);

            var processedLambdaFunctions = new HashSet<string>();

            foreach (var lambdaFunction in report.LambdaFunctions)
            {
                if (!ShouldProcessLambdaFunction(lambdaFunction))
                    continue;
                ProcessLambdaFunction(lambdaFunction, relativeProjectUri, authorizerLookup);
                processedLambdaFunctions.Add(lambdaFunction.ResourceName);
            }

            RemoveOrphanedLambdaFunctions(processedLambdaFunctions);
            RemoveOrphanedAuthorizers(report.Authorizers);
            var content = _templateWriter.GetContent();
            _fileManager.WriteAllText(report.CloudFormationTemplatePath, content);

            _diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.CodeGeneration, Location.None, $"{report.CloudFormationTemplatePath}", content));
        }

        /// <summary>
        /// Determines if the Lambda function and its properties should be written to the serverless template.
        /// It checks the 'Resources.FUNCTION_NAME' path in the serverless template.
        /// If the path does not exist, then the function should be processed and its properties must be persisted.
        /// If the path exists, the function will only be processed if 'Resources.FUNCTION_NAME.Metadata.Tool' == 'Amazon.Lambda.Annotations'/>
        /// </summary>
        private bool ShouldProcessLambdaFunction(ILambdaFunctionSerializable lambdaFunction)
        {
            var lambdaFunctionPath = $"Resources.{lambdaFunction.ResourceName}";

            if (!_templateWriter.Exists(lambdaFunctionPath))
                return true;

            var creationTool = _templateWriter.GetToken<string>($"{lambdaFunctionPath}.Metadata.Tool", string.Empty);
            return string.Equals(creationTool, CREATION_TOOL, StringComparison.Ordinal);
        }


        /// <summary>
        /// Captures different properties specified by <see cref="ILambdaFunctionSerializable"/> and attributes specified by <see cref="AttributeModel{T}"/>
        /// and writes it to the serverless template.
        /// </summary>
        private void ProcessLambdaFunction(ILambdaFunctionSerializable lambdaFunction, string relativeProjectUri, Dictionary<string, AuthorizerModel> authorizerLookup)
        {
            var lambdaFunctionPath = $"Resources.{lambdaFunction.ResourceName}";
            var propertiesPath = $"{lambdaFunctionPath}.Properties";

            if (!_templateWriter.Exists(lambdaFunctionPath))
                ApplyLambdaFunctionDefaults(lambdaFunctionPath, propertiesPath, lambdaFunction.Runtime);

            ProcessLambdaFunctionProperties(lambdaFunction, propertiesPath, relativeProjectUri);
            ProcessLambdaFunctionEventAttributes(lambdaFunction, authorizerLookup);
        }

        /// <summary>
        /// Captures different properties specified by <see cref="ILambdaFunctionSerializable"/> and writes it to the serverless template
        /// All properties are specified under 'Resources.FUNCTION_NAME.Properties' path.
        /// </summary>
        private void ProcessLambdaFunctionProperties(ILambdaFunctionSerializable lambdaFunction, string propertiesPath, string relativeProjectUri)
        {
            if (lambdaFunction.Timeout > 0)
                _templateWriter.SetToken($"{propertiesPath}.Timeout", lambdaFunction.Timeout);

            if (lambdaFunction.MemorySize > 0)
                _templateWriter.SetToken($"{propertiesPath}.MemorySize", lambdaFunction.MemorySize);

            if (!string.IsNullOrEmpty(lambdaFunction.Role))
            {
                ProcessLambdaFunctionRole(lambdaFunction, $"{propertiesPath}.Role");
                _templateWriter.RemoveToken($"{propertiesPath}.Policies");
            }

            if (!string.IsNullOrEmpty(lambdaFunction.Policies))
            {
                var policyArray = lambdaFunction.Policies.Split(',').Select(x => _templateWriter.GetValueOrRef(x.Trim())).ToList();
                _templateWriter.SetToken($"{propertiesPath}.Policies", policyArray, TokenType.List);
                _templateWriter.RemoveToken($"{propertiesPath}.Role");
            }

            ProcessPackageTypeProperty(lambdaFunction, propertiesPath, relativeProjectUri);
        }

        /// <summary>
        /// Specifies the deployment package type in the serverless template.
        /// The package type property is specified under 'Resources.FUNCTION_NAME.Properties.PackageType' path.
        /// Depending on the package type, some non-relevant properties will be removed.
        /// </summary>
        private void ProcessPackageTypeProperty(ILambdaFunctionSerializable lambdaFunction, string propertiesPath, string relativeProjectUri)
        {
            _templateWriter.SetToken($"{propertiesPath}.PackageType", lambdaFunction.PackageType.ToString());

            switch (lambdaFunction.PackageType)
            {
                case LambdaPackageType.Zip:
                    _templateWriter.SetToken($"{propertiesPath}.CodeUri", relativeProjectUri);
                    _templateWriter.SetToken($"{propertiesPath}.Handler", lambdaFunction.Handler);
                    _templateWriter.SetToken($"{propertiesPath}.Runtime", lambdaFunction.Runtime);
                    _templateWriter.RemoveToken($"{propertiesPath}.ImageUri");
                    _templateWriter.RemoveToken($"{propertiesPath}.ImageConfig");
                    break;

                case LambdaPackageType.Image:
                    _templateWriter.SetToken($"{propertiesPath}.ImageUri", relativeProjectUri);
                    _templateWriter.SetToken($"{propertiesPath}.ImageConfig.Command", new List<string>{lambdaFunction.Handler}, TokenType.List);
                    _templateWriter.RemoveToken($"{propertiesPath}.Handler");
                    _templateWriter.RemoveToken($"{propertiesPath}.CodeUri");
                    _templateWriter.RemoveToken($"{propertiesPath}.Runtime");
                    break;

                default:
                    throw new InvalidEnumArgumentException($"The {nameof(lambdaFunction.PackageType)} does not match any supported enums of type {nameof(LambdaPackageType)}");
            }

            if (lambdaFunction.IsExecutable)
            {
                this._templateWriter.SetToken($"{propertiesPath}.Environment.Variables.ANNOTATIONS_HANDLER", lambdaFunction.MethodName);
            }
        }

        /// <summary>
        /// Writes all attributes captured at <see cref="ILambdaFunctionSerializable.Attributes"/> to the serverless template.
        /// It also removes all events that exist in the serverless template but were not encountered during the current source generation pass.
        /// All events are specified under 'Resources.FUNCTION_NAME.Properties.Events' path.
        /// </summary>
        private void ProcessLambdaFunctionEventAttributes(ILambdaFunctionSerializable lambdaFunction, Dictionary<string, AuthorizerModel> authorizerLookup)
        {
            var currentSyncedEvents = new List<string>();
            var currentSyncedEventProperties = new Dictionary<string, List<string>>();

            foreach (var attributeModel in lambdaFunction.Attributes)
            {
                string eventName;
                switch (attributeModel)
                {
                    case AttributeModel<HttpApiAttribute> httpApiAttributeModel:
                        eventName = ProcessHttpApiAttribute(lambdaFunction, httpApiAttributeModel.Data, currentSyncedEventProperties, authorizerLookup);
                        currentSyncedEvents.Add(eventName);
                        break;
                    case AttributeModel<RestApiAttribute> restApiAttributeModel:
                        eventName = ProcessRestApiAttribute(lambdaFunction, restApiAttributeModel.Data, currentSyncedEventProperties, authorizerLookup);
                        currentSyncedEvents.Add(eventName);
                        break;
                    case AttributeModel<SQSEventAttribute> sqsAttributeModel:
                        eventName = ProcessSqsAttribute(lambdaFunction, sqsAttributeModel.Data, currentSyncedEventProperties);
                        currentSyncedEvents.Add(eventName);
                        break;
                }
            }

            SynchronizeEventsAndProperties(currentSyncedEvents, currentSyncedEventProperties, lambdaFunction);
        }

        /// <summary>
        /// Writes all properties associated with <see cref="RestApiAttribute"/> to the serverless template.
        /// </summary>
        private string ProcessRestApiAttribute(ILambdaFunctionSerializable lambdaFunction, RestApiAttribute restApiAttribute, Dictionary<string, List<string>> syncedEventProperties, Dictionary<string, AuthorizerModel> authorizerLookup)
        {
            var eventName = $"Root{restApiAttribute.Method}";
            var eventPath = $"Resources.{lambdaFunction.ResourceName}.Properties.Events.{eventName}";

            _templateWriter.SetToken($"{eventPath}.Type", "Api");
            SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "Path", restApiAttribute.Template);
            SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "Method", restApiAttribute.Method.ToString().ToUpper());

            // Set Auth configuration if authorizer is specified
            // Use the authorizer name directly (not a CloudFormation Ref) since authorizers are defined inline in the API
            // Also set RestApiId to link to our explicit ServerlessRestApi resource where the authorizer is defined
            if (!string.IsNullOrEmpty(restApiAttribute.Authorizer) && authorizerLookup.TryGetValue(restApiAttribute.Authorizer, out var authorizer))
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "Auth.Authorizer", authorizer.Name);
            }

            // Always reference the shared API resource if it exists, so all REST API functions share one endpoint
            if (_templateWriter.Exists($"Resources.{REST_API_RESOURCE_NAME}"))
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, $"RestApiId.{REF}", REST_API_RESOURCE_NAME);
            }

            return eventName;
        }

        /// <summary>
        /// Writes all properties associated with <see cref="HttpApiAttribute"/> to the serverless template.
        /// </summary>
        private string ProcessHttpApiAttribute(ILambdaFunctionSerializable lambdaFunction, HttpApiAttribute httpApiAttribute, Dictionary<string, List<string>> syncedEventProperties, Dictionary<string, AuthorizerModel> authorizerLookup)
        {
            var eventName = $"Root{httpApiAttribute.Method}";
            var eventPath = $"Resources.{lambdaFunction.ResourceName}.Properties.Events.{eventName}";

            _templateWriter.SetToken($"{eventPath}.Type", "HttpApi");
            SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "Path", httpApiAttribute.Template);
            SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "Method", httpApiAttribute.Method.ToString().ToUpper());

            // Only set the PayloadFormatVersion for 1.0.
            // If no PayloadFormatVersion is specified then by default 2.0 is used.
            if (httpApiAttribute.Version == HttpApiVersion.V1)
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "PayloadFormatVersion", "1.0");

            // Set Auth configuration if authorizer is specified
            // Use the authorizer name directly (not a CloudFormation Ref) since authorizers are defined inline in the API
            // Also set ApiId to link to our explicit ServerlessHttpApi resource where the authorizer is defined
            if (!string.IsNullOrEmpty(httpApiAttribute.Authorizer) && authorizerLookup.TryGetValue(httpApiAttribute.Authorizer, out var authorizer))
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "Auth.Authorizer", authorizer.Name);
            }

            // Always reference the shared API resource if it exists, so all HTTP API functions share one endpoint
            if (_templateWriter.Exists($"Resources.{HTTP_API_RESOURCE_NAME}"))
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, $"ApiId.{REF}", HTTP_API_RESOURCE_NAME);
            }

            return eventName;
        }

        /// <summary>
        /// Processes all authorizers and writes them to the serverless template as inline authorizers within the API resources.
        /// AWS SAM expects authorizers to be defined within the Auth.Authorizers property of AWS::Serverless::HttpApi or AWS::Serverless::Api resources.
        /// </summary>
        private void ProcessAuthorizers(IList<AuthorizerModel> authorizers)
        {
            // Group authorizers by type
            var httpApiAuthorizers = authorizers.Where(a => a.AuthorizerType == AuthorizerType.HttpApi).ToList();
            var restApiAuthorizers = authorizers.Where(a => a.AuthorizerType == AuthorizerType.RestApi).ToList();

            // Process HTTP API authorizers (add to AnnotationsHttpApi resource)
            if (httpApiAuthorizers.Any())
            {
                ProcessHttpApiAuthorizers(httpApiAuthorizers);
            }

            // Process REST API authorizers (add to AnnotationsRestApi resource)
            if (restApiAuthorizers.Any())
            {
                ProcessRestApiAuthorizers(restApiAuthorizers);
            }
        }

        /// <summary>
        /// Writes HTTP API (API Gateway V2) authorizers to the AnnotationsHttpApi resource.
        /// SAM expects authorizers to be defined inline in the Auth.Authorizers property.
        /// </summary>
        private void ProcessHttpApiAuthorizers(IList<AuthorizerModel> authorizers)
        {
            const string httpApiResourcePath = "Resources." + HTTP_API_RESOURCE_NAME;
            
            // Create the AnnotationsHttpApi resource if it doesn't exist
            if (!_templateWriter.Exists(httpApiResourcePath))
            {
                _templateWriter.SetToken($"{httpApiResourcePath}.Type", "AWS::Serverless::HttpApi");
                _templateWriter.SetToken($"{httpApiResourcePath}.Metadata.Tool", CREATION_TOOL);
            }

            // Add each authorizer to the Auth.Authorizers map
            foreach (var authorizer in authorizers)
            {
                var authorizerPath = $"{httpApiResourcePath}.Properties.Auth.Authorizers.{authorizer.Name}";

                // FunctionArn - Reference to the Lambda function ARN
                _templateWriter.SetToken($"{authorizerPath}.FunctionArn.{GET_ATTRIBUTE}", new List<string> { authorizer.LambdaResourceName, "Arn" }, TokenType.List);

                // AuthorizerPayloadFormatVersion
                _templateWriter.SetToken($"{authorizerPath}.AuthorizerPayloadFormatVersion", authorizer.PayloadFormatVersion);

                // EnableSimpleResponses
                _templateWriter.SetToken($"{authorizerPath}.EnableSimpleResponses", authorizer.EnableSimpleResponses);

                // Identity.Headers - The header to use for identity source
                _templateWriter.SetToken($"{authorizerPath}.Identity.Headers", new List<string> { authorizer.IdentityHeader }, TokenType.List);

                // EnableFunctionDefaultPermissions tells SAM to automatically create the AWS::Lambda::Permission
                // for the authorizer Lambda function. Unlike AWS::Serverless::Api (REST API), AWS::Serverless::HttpApi
                // does NOT automatically create invoke permissions for Lambda authorizers defined in Auth.Authorizers.
                // https://github.com/aws/serverless-application-model/issues/2933
                _templateWriter.SetToken($"{authorizerPath}.EnableFunctionDefaultPermissions", true);

                // AuthorizerResultTtlInSeconds - always write this value so SAM does not apply its default TTL.
                // A value of 0 disables caching, which matches the attribute default.
                _templateWriter.SetToken($"{authorizerPath}.AuthorizerResultTtlInSeconds", authorizer.ResultTtlInSeconds);
            }
        }

        /// <summary>
        /// Writes REST API (API Gateway V1) authorizers to the AnnotationsRestApi resource.
        /// SAM expects authorizers to be defined inline in the Auth.Authorizers property.
        /// </summary>
        private void ProcessRestApiAuthorizers(IList<AuthorizerModel> authorizers)
        {
            const string restApiResourcePath = "Resources." + REST_API_RESOURCE_NAME;
            
            // Create the AnnotationsRestApi resource if it doesn't exist
            if (!_templateWriter.Exists(restApiResourcePath))
            {
                _templateWriter.SetToken($"{restApiResourcePath}.Type", "AWS::Serverless::Api");
                // REST API requires explicit stage name
                _templateWriter.SetToken($"{restApiResourcePath}.Properties.StageName", "Prod");
                _templateWriter.SetToken($"{restApiResourcePath}.Metadata.Tool", CREATION_TOOL);
            }

            // Add each authorizer to the Auth.Authorizers map
            foreach (var authorizer in authorizers)
            {
                var authorizerPath = $"{restApiResourcePath}.Properties.Auth.Authorizers.{authorizer.Name}";

                // FunctionArn - Reference to the Lambda function ARN using GetAtt
                _templateWriter.SetToken($"{authorizerPath}.FunctionArn.{GET_ATTRIBUTE}", new List<string> { authorizer.LambdaResourceName, "Arn" }, TokenType.List);

                // Identity.Header - The header to use for identity source
                _templateWriter.SetToken($"{authorizerPath}.Identity.Header", authorizer.IdentityHeader);

                // FunctionPayloadType - TOKEN or REQUEST
                if (authorizer.RestApiAuthorizerType == RestApiAuthorizerType.Token)
                {
                    _templateWriter.SetToken($"{authorizerPath}.FunctionPayloadType", "TOKEN");
                }
                else
                {
                    _templateWriter.SetToken($"{authorizerPath}.FunctionPayloadType", "REQUEST");
                }

                // AuthorizerResultTtlInSeconds - cache TTL for authorizer result (only when valid)
                // API Gateway REST Lambda authorizer TTL must be between 1 and 3600 seconds.
                var resultTtlInSeconds = authorizer.ResultTtlInSeconds;
                const int minTtlInSeconds = 1;
                const int maxTtlInSeconds = 3600;

                if (resultTtlInSeconds >= minTtlInSeconds &&
                    resultTtlInSeconds <= maxTtlInSeconds)
                {
                    _templateWriter.SetToken($"{authorizerPath}.AuthorizerResultTtlInSeconds", resultTtlInSeconds);
                }
            }
        }

        /// <summary>
        /// Removes orphaned authorizers from the serverless template.
        /// Authorizers are now defined inline within the API resources (AnnotationsHttpApi and AnnotationsRestApi).
        /// This method removes authorizers that were created by Lambda Annotations but no longer exist in the current compilation.
        /// It also cleans up legacy standalone authorizer resources (AWS::ApiGatewayV2::Authorizer, AWS::ApiGateway::Authorizer)
        /// and their associated Lambda permissions.
        /// </summary>
        private void RemoveOrphanedAuthorizers(IList<AuthorizerModel> currentAuthorizers)
        {
            if (!_templateWriter.Exists("Resources"))
            {
                return;
            }

            // Get current authorizer names by type
            var currentHttpApiAuthorizerNames = new HashSet<string>(
                currentAuthorizers.Where(a => a.AuthorizerType == AuthorizerType.HttpApi).Select(a => a.Name));
            var currentRestApiAuthorizerNames = new HashSet<string>(
                currentAuthorizers.Where(a => a.AuthorizerType == AuthorizerType.RestApi).Select(a => a.Name));

            // Clean up orphaned inline authorizers in AnnotationsHttpApi
            const string httpApiAuthorizersPath = "Resources." + HTTP_API_RESOURCE_NAME + ".Properties.Auth.Authorizers";
            if (_templateWriter.Exists(httpApiAuthorizersPath))
            {
                var httpApiCreationTool = _templateWriter.GetToken<string>($"Resources.{HTTP_API_RESOURCE_NAME}.Metadata.Tool", string.Empty);
                if (string.Equals(httpApiCreationTool, CREATION_TOOL, StringComparison.Ordinal))
                {
                    var existingAuthorizerNames = _templateWriter.GetKeys(httpApiAuthorizersPath);
                    foreach (var authorizerName in existingAuthorizerNames)
                    {
                        if (!currentHttpApiAuthorizerNames.Contains(authorizerName))
                        {
                            _templateWriter.RemoveToken($"{httpApiAuthorizersPath}.{authorizerName}");
                        }
                    }
                    
                    // Clean up empty Auth structure
                    _templateWriter.RemoveTokenIfNullOrEmpty(httpApiAuthorizersPath);
                    _templateWriter.RemoveTokenIfNullOrEmpty($"Resources.{HTTP_API_RESOURCE_NAME}.Properties.Auth");
                }
            }

            // Clean up orphaned inline authorizers in AnnotationsRestApi
            const string restApiAuthorizersPath = "Resources." + REST_API_RESOURCE_NAME + ".Properties.Auth.Authorizers";
            if (_templateWriter.Exists(restApiAuthorizersPath))
            {
                var restApiCreationTool = _templateWriter.GetToken<string>($"Resources.{REST_API_RESOURCE_NAME}.Metadata.Tool", string.Empty);
                if (string.Equals(restApiCreationTool, CREATION_TOOL, StringComparison.Ordinal))
                {
                    var existingAuthorizerNames = _templateWriter.GetKeys(restApiAuthorizersPath);
                    foreach (var authorizerName in existingAuthorizerNames)
                    {
                        if (!currentRestApiAuthorizerNames.Contains(authorizerName))
                        {
                            _templateWriter.RemoveToken($"{restApiAuthorizersPath}.{authorizerName}");
                        }
                    }
                    
                    // Clean up empty Auth structure
                    _templateWriter.RemoveTokenIfNullOrEmpty(restApiAuthorizersPath);
                    _templateWriter.RemoveTokenIfNullOrEmpty($"Resources.{REST_API_RESOURCE_NAME}.Properties.Auth");
                }
            }

            // Clean up legacy standalone authorizer resources and permissions from older versions
            var toRemove = new List<string>();
            foreach (var resourceName in _templateWriter.GetKeys("Resources"))
            {
                var resourcePath = $"Resources.{resourceName}";
                var type = _templateWriter.GetToken<string>($"{resourcePath}.Type", string.Empty);
                var creationTool = _templateWriter.GetToken<string>($"{resourcePath}.Metadata.Tool", string.Empty);

                if (!string.Equals(creationTool, CREATION_TOOL, StringComparison.Ordinal))
                {
                    continue;
                }

                // Remove legacy standalone authorizer resources
                if (string.Equals(type, "AWS::ApiGatewayV2::Authorizer", StringComparison.Ordinal) ||
                    string.Equals(type, "AWS::ApiGateway::Authorizer", StringComparison.Ordinal))
                {
                    toRemove.Add(resourceName);
                }

                // Remove legacy authorizer Lambda permissions
                if (string.Equals(type, "AWS::Lambda::Permission", StringComparison.Ordinal) &&
                    resourceName.EndsWith("AuthorizerPermission"))
                {
                    toRemove.Add(resourceName);
                }
            }

            foreach (var resourceName in toRemove)
            {
                _templateWriter.RemoveToken($"Resources.{resourceName}");
            }
        }

        /// <summary>
        /// Writes all properties associated with <see cref="SQSEventAttribute"/> to the serverless template.
        /// </summary>
        private string ProcessSqsAttribute(ILambdaFunctionSerializable lambdaFunction, SQSEventAttribute att, Dictionary<string, List<string>> syncedEventProperties)
        {
            var eventName = att.ResourceName;
            var eventPath = $"Resources.{lambdaFunction.ResourceName}.Properties.Events.{eventName}";

            // Set event type - https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/sam-property-function-eventsource.html#sam-function-eventsource-type
            _templateWriter.SetToken($"{eventPath}.Type", "SQS");

            // Set SQS properties - https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/sam-property-function-sqs.html

            // Queue
            // Remove Queue if set previously
            _templateWriter.RemoveToken($"{eventPath}.Properties.Queue");
            if (!att.Queue.StartsWith("@"))
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "Queue", att.Queue);
            }
            else
            {
                var queue = att.Queue.Substring(1);
                if (_templateWriter.Exists($"{PARAMETERS}.{queue}"))
                    SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, $"Queue.{REF}", queue);
                else
                    SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, $"Queue.{GET_ATTRIBUTE}", new List<string> { queue, "Arn" }, TokenType.List);
            }
            
            // BatchSize
            if (att.IsBatchSizeSet)
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "BatchSize", att.BatchSize);
            }

            // Enabled
            if (att.IsEnabledSet)
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "Enabled", att.Enabled); 
            }

            // FilterCriteria
            if (att.IsFiltersSet)
            {
                const char SEPERATOR = ';';
                var filters = att.Filters.Split(SEPERATOR).Select(x => x.Trim()).ToList();
                var filterList = new List<Dictionary<string, string>>();
                foreach (var filter in filters)
                {
                   filterList.Add(new Dictionary<string, string> { { "Pattern", filter } });
                }
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "FilterCriteria.Filters", filterList, TokenType.List);
            }

            // FunctionResponseTypes
            if (lambdaFunction.ReturnTypeFullName.Contains(TypeFullNames.SQSBatchResponse))
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "FunctionResponseTypes", new List<string> { "ReportBatchItemFailures" }, TokenType.List);
            }

            // MaximumBatchingWindowInSeconds
            if (att.IsMaximumBatchingWindowInSecondsSet)
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "MaximumBatchingWindowInSeconds", att.MaximumBatchingWindowInSeconds);
            }

            // ScalingConfig
            if (att.IsMaximumConcurrencySet)
            {
                SetEventProperty(syncedEventProperties, lambdaFunction.ResourceName, eventName, "ScalingConfig.MaximumConcurrency", att.MaximumConcurrency);
            }

            return att.ResourceName;
        }

        /// <summary>
        /// Writes all properties associated with <see cref="LambdaFunctionRoleAttribute"/> to the serverless template.
        /// </summary>

        /// <summary>
        /// Writes the default values for the Lambda function's metadata and properties.
        /// </summary>
        private void ApplyLambdaFunctionDefaults(string lambdaFunctionPath, string propertiesPath, string runtime)
        {
            _templateWriter.SetToken($"{lambdaFunctionPath}.Type", "AWS::Serverless::Function");
            _templateWriter.SetToken($"{lambdaFunctionPath}.Metadata.Tool", CREATION_TOOL);

            _templateWriter.SetToken($"{propertiesPath}.Runtime", runtime);
            _templateWriter.SetToken($"{propertiesPath}.CodeUri", "");
            _templateWriter.SetToken($"{propertiesPath}.MemorySize", 512);
            _templateWriter.SetToken($"{propertiesPath}.Timeout", 30);
            _templateWriter.SetToken($"{propertiesPath}.Policies", new List<string>{"AWSLambdaBasicExecutionRole"}, TokenType.List);
        }

        /// <summary>
        /// Creates a new serverless template with no resources.
        /// </summary>
        private void CreateNewTemplate()
        {
            _templateWriter.SetToken("AWSTemplateFormatVersion", "2010-09-09");
            _templateWriter.SetToken("Transform", "AWS::Serverless-2016-10-31");
        }

        /// <summary>
        /// Removes all Lambda functions that exist in the serverless template but were not encountered during the current source generation pass.
        /// Any resource that is removed must be of type 'AWS::Serverless::Function' and must have 'Resources.FUNCTION_NAME.Metadata.Tool' == 'Amazon.Lambda.Annotations'.
        /// </summary>
        private void RemoveOrphanedLambdaFunctions(HashSet<string> processedLambdaFunctions)
        {
            if (!_templateWriter.Exists("Resources"))
            {
                return;
            }

            var toRemove = new List<string>();
            foreach (var resourceName in _templateWriter.GetKeys("Resources"))
            {
                var resourcePath = $"Resources.{resourceName}";
                var type = _templateWriter.GetToken<string>($"{resourcePath}.Type", string.Empty);
                var creationTool = _templateWriter.GetToken<string>($"{resourcePath}.Metadata.Tool", string.Empty);

                if (string.Equals(type, "AWS::Serverless::Function", StringComparison.Ordinal)
                    && string.Equals(creationTool, "Amazon.Lambda.Annotations", StringComparison.Ordinal)
                    && !processedLambdaFunctions.Contains(resourceName))
                {
                    toRemove.Add(resourceName);
                }
            }

            foreach (var resourceName in toRemove)
            {
                _templateWriter.RemoveToken($"Resources.{resourceName}");
            }
        }

        /// <summary>
        /// Write the IAM role associated with the Lambda function.
        /// The IAM role is specified under 'Resources.FUNCTION_NAME.Properties.Role' path.
        /// </summary>
        private void ProcessLambdaFunctionRole(ILambdaFunctionSerializable lambdaFunction, string rolePath)
        {
            if (string.IsNullOrEmpty(lambdaFunction.Role))
            {
                return;
            }

            if (!lambdaFunction.Role.StartsWith("@"))
            {
                _templateWriter.SetToken(rolePath, lambdaFunction.Role);
                return;
            }

            var role = lambdaFunction.Role.Substring(1);
            if (_templateWriter.Exists($"{PARAMETERS}.{role}"))
            {
                _templateWriter.SetToken($"{rolePath}.{REF}", role);
            }
            else
            {
                _templateWriter.SetToken($"{rolePath}.{GET_ATTRIBUTE}", new List<string>{role, "Arn"}, TokenType.List);
            }
        }

        /// <summary>
        /// Suffix that is appended to the CloudFormation template with the name
        /// and version of the Lambda Annotations library
        /// </summary>
        public static string CurrentDescriptionSuffix
        {
            get
            {
                var version = Assembly.GetAssembly(MethodBase.GetCurrentMethod().DeclaringType).GetName().Version.ToString();
                return $"{BASE_DESCRIPTION} (v{version}).";
            }
        }

        /// <summary>
        /// This appends a string to the CloudFormation template description field with the version
        /// of Lambda Annotations that was used during compilation.
        /// 
        /// This string allows AWS to report on these templates to measure the usage of this framework.
        /// This aids investigations and outreach if we find a critical bug, 
        /// helps understanding our version adoption, and allows us to prioritize improvements to this
        /// library against other .NET projects.
        /// </summary>
        private void ProcessTemplateDescription(AnnotationReport report)
        {
            if (report.IsTelemetrySuppressed)
            {
                RemoveTemplateDescriptionIfSet();
            }
            else
            {
                SetOrUpdateTemplateDescription();
            }
        }

        /// <summary>
        /// Either appends the new version suffix in the CloudFormation template 
        /// description, or updates it if an older version is found.
        /// </summary>
        private void SetOrUpdateTemplateDescription()
        {
            string updatedDescription;

            if (_templateWriter.Exists("Description"))
            {
                var existingDescription = _templateWriter.GetToken<string>("Description");

                var existingDescriptionSuffix = ExtractCurrentDescriptionSuffix(existingDescription);

                if (!string.IsNullOrEmpty(existingDescriptionSuffix))
                {
                    updatedDescription = existingDescription.Replace(existingDescriptionSuffix, CurrentDescriptionSuffix);
                }
                else if (!string.IsNullOrEmpty(existingDescription)) // The version string isn't in the current description, so we just append it.
                {
                    updatedDescription = existingDescription + " " + CurrentDescriptionSuffix;
                }
                else // "Description" path exists but is null or empty, so just overwrite it
                {
                    updatedDescription = CurrentDescriptionSuffix;
                }
            }
            else // the "Description" path doesn't exist, so set it
            {
                updatedDescription = CurrentDescriptionSuffix;
            }

            // In any case if the updated description is longer than CloudFormation's limit, fall back to the existing one.
            // https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/template-description-structure.html
            if (updatedDescription.Length > 1024)
            {
                return;
            }

            _templateWriter.SetToken("Description", updatedDescription);
        }

        /// <summary>
        /// Removes the version suffix from a CloudFormation template descripton
        /// </summary>
        private void RemoveTemplateDescriptionIfSet()
        {
            if (!_templateWriter.Exists("Description"))
            {
                return;
            }

            var existingDescription = _templateWriter.GetToken<string>("Description");
            var existingDescriptionSuffix = ExtractCurrentDescriptionSuffix(existingDescription);

            if (string.IsNullOrEmpty(existingDescriptionSuffix))
            {
                return;
            }

            var updatedDescription = existingDescription.Replace(existingDescriptionSuffix, "");

            _templateWriter.SetToken("Description", updatedDescription);
        }

        /// <summary>
        /// Extracts the version suffix from a CloudFormation template description  
        /// </summary>
        /// <param name="templateDescription"></param>
        /// <returns></returns>
        private string ExtractCurrentDescriptionSuffix(string templateDescription)
        {
            var startIndex = templateDescription.IndexOf(BASE_DESCRIPTION);
            if (startIndex >= 0)
            {
                // Find the next ")." which will be the end of the old version string
                var endIndex = templateDescription.IndexOf(END_OF_VESRION_IN_DESCRIPTION, startIndex);

                // If we couldn't find the end of our version string, it's only a fragment, so abort.
                if (endIndex == -1)
                {
                    return string.Empty;
                }

                var lengthOfCurrentDescription = endIndex + END_OF_VESRION_IN_DESCRIPTION.Length - startIndex;
                return templateDescription.Substring(startIndex, lengthOfCurrentDescription);
            }

            return string.Empty;
        }

        /// <summary>
        /// This sets the event property for a given event and property path.
        /// It also keeps track of which properties have been set for each event so that we can remove any orphaned properties later.
        /// </summary>
        private void SetEventProperty(Dictionary<string, List<string>> syncedEventProperties, string lambdaResourceName, string eventResourceName, string propertyPath, object value, TokenType tokenType = TokenType.Other)
        {
            _templateWriter.SetToken($"Resources.{lambdaResourceName}.Properties.Events.{eventResourceName}.Properties.{propertyPath}", value, tokenType);
            if (!syncedEventProperties.ContainsKey(eventResourceName))
            {
                syncedEventProperties[eventResourceName] = new List<string>();
            }
            syncedEventProperties[eventResourceName].Add(propertyPath);
        }

        /// <summary>
        /// Synchronizes events and their properties for a given lambda function in its CloudFormation metadata.
        /// </summary>
        /// <param name="syncedEvents">List of events to synchronize.</param>
        /// <param name="syncedEventProperties">Dictionary containing event properties to synchronize.</param>
        /// <param name="lambdaFunction">The lambda function for which to synchronize events and properties.</param>
        private void SynchronizeEventsAndProperties(List<string> syncedEvents, Dictionary<string, List<string>> syncedEventProperties, ILambdaFunctionSerializable lambdaFunction)
        {
            // Construct paths for synced events in the resource template.
            var eventsPath = $"Resources.{lambdaFunction.ResourceName}.Properties.Events";
            var syncedEventsPath = $"Resources.{lambdaFunction.ResourceName}.Metadata.SyncedEvents";

            // Get previously synced events.
            var previousSyncedEvents = _templateWriter.GetToken<List<string>>(syncedEventsPath, new List<string>());

            // Remove orphaned events.
            var orphanedEvents = previousSyncedEvents.Except(syncedEvents).ToList();
            orphanedEvents.ForEach(eventName => _templateWriter.RemoveToken($"{eventsPath}.{eventName}"));

            // Update synced events in the template.
            _templateWriter.RemoveToken(syncedEventsPath);
            if (syncedEvents.Any())
                _templateWriter.SetToken(syncedEventsPath, syncedEvents, TokenType.List);

            // Construct path for synced event properties in the resource template.
            var syncedEventPropertiesPath = $"Resources.{lambdaFunction.ResourceName}.Metadata.SyncedEventProperties";

            // Get previously synced event properties.
            var previousSyncedEventProperties = _templateWriter.GetToken<Dictionary<string, List<string>>>(syncedEventPropertiesPath, new Dictionary<string, List<string>>());

            // Remove orphaned event properties.
            foreach (var eventName in previousSyncedEventProperties.Keys.Intersect(syncedEventProperties.Keys))
            {
                var orphanedEventProperties = previousSyncedEventProperties[eventName].Except(syncedEventProperties[eventName]).ToList();
                orphanedEventProperties.ForEach(propertyPath =>
                {
                    // If previously a property existed as a terminal property but now exists as complex property then do not delete it.
                    // This can happen when a property was previously added as an ARN by is now being added as a Ref.
                    if (syncedEventProperties[eventName].Any(p => p.StartsWith(propertyPath)))
                    {
                        return;
                    }

                    _templateWriter.RemoveToken($"{eventsPath}.{eventName}.Properties.{propertyPath}");

                    // Remove the terminal property and parent properties if they're now empty.
                    // Consider the following example:
                    // {
                    //   "A": {
                    //     "B": {
                    //       "C": "D"
                    //      }
                    //   }
                    // }
                    // If A.B.C is removed, then A.B and A must also be removed since they're now empty because of the cascading effects.
                    var propertyPathList = propertyPath.Split('.').ToList();
                    while (propertyPathList.Any())
                    {
                        _templateWriter.RemoveTokenIfNullOrEmpty($"{eventsPath}.{eventName}.Properties.{string.Join(".", propertyPathList)}");
                        propertyPathList.RemoveAt(propertyPathList.Count - 1);
                    }
                });
            }

            // Update synced event properties in the template.
            _templateWriter.RemoveToken(syncedEventPropertiesPath);
            if (syncedEventProperties.Any())
                _templateWriter.SetToken(syncedEventPropertiesPath, syncedEventProperties, TokenType.KeyVal);
        }
    }
}