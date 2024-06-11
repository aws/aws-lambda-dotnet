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

            var processedLambdaFunctions = new HashSet<string>();

            foreach (var lambdaFunction in report.LambdaFunctions)
            {
                if (!ShouldProcessLambdaFunction(lambdaFunction))
                    continue;
                ProcessLambdaFunction(lambdaFunction, relativeProjectUri);
                processedLambdaFunctions.Add(lambdaFunction.ResourceName);
            }

            RemoveOrphanedLambdaFunctions(processedLambdaFunctions);
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
        private void ProcessLambdaFunction(ILambdaFunctionSerializable lambdaFunction, string relativeProjectUri)
        {
            var lambdaFunctionPath = $"Resources.{lambdaFunction.ResourceName}";
            var propertiesPath = $"{lambdaFunctionPath}.Properties";

            if (!_templateWriter.Exists(lambdaFunctionPath))
                ApplyLambdaFunctionDefaults(lambdaFunctionPath, propertiesPath, lambdaFunction.Runtime);

            ProcessLambdaFunctionProperties(lambdaFunction, propertiesPath, relativeProjectUri);
            ProcessLambdaFunctionEventAttributes(lambdaFunction);
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
        private void ProcessLambdaFunctionEventAttributes(ILambdaFunctionSerializable lambdaFunction)
        {
            var currentSyncedEvents = new List<string>();

            foreach (var attributeModel in lambdaFunction.Attributes)
            {
                string eventName;
                switch (attributeModel)
                {
                    case AttributeModel<HttpApiAttribute> httpApiAttributeModel:
                        eventName = ProcessHttpApiAttribute(lambdaFunction, httpApiAttributeModel.Data);
                        currentSyncedEvents.Add(eventName);
                        break;
                    case AttributeModel<RestApiAttribute> restApiAttributeModel:
                        eventName = ProcessRestApiAttribute(lambdaFunction, restApiAttributeModel.Data);
                        currentSyncedEvents.Add(eventName);
                        break;
                    case AttributeModel<SQSEventAttribute> sqsAttributeModel:
                        eventName = ProcessSqsAttribute(lambdaFunction, sqsAttributeModel.Data);
                        currentSyncedEvents.Add(eventName);
                        break;
                }
            }

            var eventsPath = $"Resources.{lambdaFunction.ResourceName}.Properties.Events";
            var syncedEventsMetadataPath = $"Resources.{lambdaFunction.ResourceName}.Metadata.SyncedEvents";
            var previousSyncedEvents = _templateWriter.GetToken<List<string>>(syncedEventsMetadataPath, new List<string>());

            // Remove all events that exist in the serverless template but were not encountered during the current source generation pass.
            foreach (var previousEventName in previousSyncedEvents)
            {
                if (!currentSyncedEvents.Contains(previousEventName))
                    _templateWriter.RemoveToken($"{eventsPath}.{previousEventName}");
            }

            if (currentSyncedEvents.Any())
                _templateWriter.SetToken(syncedEventsMetadataPath, currentSyncedEvents, TokenType.List);
            else
                _templateWriter.RemoveToken(syncedEventsMetadataPath);
        }

        /// <summary>
        /// Writes all properties associated with <see cref="RestApiAttribute"/> to the serverless template.
        /// </summary>
        private string ProcessRestApiAttribute(ILambdaFunctionSerializable lambdaFunction, RestApiAttribute restApiAttribute)
        {
            var eventPath = $"Resources.{lambdaFunction.ResourceName}.Properties.Events";
            var methodName = restApiAttribute.Method.ToString();
            var methodPath = $"{eventPath}.Root{methodName}";

            _templateWriter.SetToken($"{methodPath}.Type", "Api");
            _templateWriter.SetToken($"{methodPath}.Properties.Path", restApiAttribute.Template);
            _templateWriter.SetToken($"{methodPath}.Properties.Method", methodName.ToUpper());

            return $"Root{methodName}";
        }

        /// <summary>
        /// Writes all properties associated with <see cref="HttpApiAttribute"/> to the serverless template.
        /// </summary>
        private string ProcessHttpApiAttribute(ILambdaFunctionSerializable lambdaFunction, HttpApiAttribute httpApiAttribute)
        {
            var eventPath = $"Resources.{lambdaFunction.ResourceName}.Properties.Events";
            var methodName = httpApiAttribute.Method.ToString();
            var methodPath = $"{eventPath}.Root{methodName}";

            _templateWriter.SetToken($"{methodPath}.Type", "HttpApi");
            _templateWriter.SetToken($"{methodPath}.Properties.Path", httpApiAttribute.Template);
            _templateWriter.SetToken($"{methodPath}.Properties.Method", methodName.ToUpper());

            // Only set the PayloadFormatVersion for 1.0.
            // If no PayloadFormatVersion is specified then by default 2.0 is used.
            if (httpApiAttribute.Version == HttpApiVersion.V1)
                _templateWriter.SetToken($"{methodPath}.Properties.PayloadFormatVersion", "1.0");

            return $"Root{methodName}";
        }

        /// <summary>
        /// Writes all properties associated with <see cref="SQSEventAttribute"/> to the serverless template.
        /// </summary>
        private string ProcessSqsAttribute(ILambdaFunctionSerializable lambdaFunction, SQSEventAttribute att)
        {
            var eventPath = $"Resources.{lambdaFunction.ResourceName}.Properties.Events.{att.ResourceName}";

            // Set event type - https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/sam-property-function-eventsource.html#sam-function-eventsource-type
            _templateWriter.SetToken($"{eventPath}.Type", "SQS");

            // Get properties path - https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/sam-property-function-eventsource.html#sam-function-eventsource-properties
            var eventPropertiesPath = $"{eventPath}.Properties";

            // Since the entire event source configuration can be specified by the SQSEventAttribute, remove everything that was set previously.
            _templateWriter.RemoveToken(eventPropertiesPath);

            // Set SQS properties - https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/sam-property-function-sqs.html

            // Queue
            if (att.Queue.StartsWith("@"))
            {
                _templateWriter.SetToken($"{eventPropertiesPath}.Queue.{GET_ATTRIBUTE}", new List<string> { att.Queue.Substring(1), "Arn" }, TokenType.List);
            }
            else
            {
                _templateWriter.SetToken($"{eventPropertiesPath}.Queue", att.Queue);
            }
            
            // BatchSize
            if (att.IsBatchSizeSet)
            {
                _templateWriter.SetToken($"{eventPropertiesPath}.BatchSize", att.BatchSize);
            }

            // Enabled
            if (att.IsEnabledSet)
            {
                _templateWriter.SetToken($"{eventPropertiesPath}.Enabled", att.Enabled);
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
                _templateWriter.SetToken($"{eventPropertiesPath}.FilterCriteria.Filters", filterList, TokenType.List);
            }

            // FunctionResponseTypes
            if (lambdaFunction.ReturnTypeFullName.Contains(TypeFullNames.SQSBatchResponse))
            {
                _templateWriter.SetToken($"{eventPropertiesPath}.FunctionResponseTypes", new List<string> { "ReportBatchItemFailures" }, TokenType.List);
            }

            // MaximumBatchingWindowInSeconds
            if (att.IsMaximumBatchingWindowInSecondsSet)
            {
                _templateWriter.SetToken($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds", att.MaximumBatchingWindowInSeconds);
            }

            // ScalingConfig
            if (att.IsMaximumConcurrencySet)
            {
                _templateWriter.SetToken($"{eventPropertiesPath}.ScalingConfig.MaximumConcurrency", att.MaximumConcurrency);
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
    }
}