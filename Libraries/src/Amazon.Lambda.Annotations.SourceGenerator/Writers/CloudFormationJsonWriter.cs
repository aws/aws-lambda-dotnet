using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    public class CloudFormationJsonWriter : IAnnotationReportWriter
    {
        private readonly IFileManager _fileManager;
        private readonly IDirectoryManager _directoryManager;
        private readonly IJsonWriter _jsonWriter;
        private readonly IDiagnosticReporter _diagnosticReporter;

        public CloudFormationJsonWriter(IFileManager fileManager, IDirectoryManager directoryManager, IJsonWriter jsonWriter, IDiagnosticReporter diagnosticReporter)
        {
            _fileManager = fileManager;
            _directoryManager = directoryManager;
            _jsonWriter = jsonWriter;
            _diagnosticReporter = diagnosticReporter;
        }

        public void ApplyReport(AnnotationReport report)
        {
            var originalContent = _fileManager.ReadAllText(report.CloudFormationTemplatePath);
            var templateDirectory = _directoryManager.GetDirectoryName(report.CloudFormationTemplatePath);
            var relativeProjectUri = _directoryManager.GetRelativePath(templateDirectory, report.ProjectRootDirectory);

            if (string.IsNullOrEmpty(originalContent))
                CreateNewTemplate();
            else
                _jsonWriter.Parse(originalContent);

            var processedLambdaFunctions = new HashSet<string>();

            foreach (var lambdaFunction in report.LambdaFunctions)
            {
                if (!ShouldProcessLambdaFunction(lambdaFunction))
                    continue;
                ProcessLambdaFunction(lambdaFunction, relativeProjectUri);
                processedLambdaFunctions.Add(lambdaFunction.Name);
            }

            RemoveOrphanedLambdaFunctions(processedLambdaFunctions);
            RemoveOrphanedResources();

            var json = _jsonWriter.GetPrettyJson();
            _fileManager.WriteAllText(report.CloudFormationTemplatePath, json);

            _diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.CodeGeneration, Location.None, $"{report.CloudFormationTemplatePath}", json));
        }

        private bool ShouldProcessLambdaFunction(ILambdaFunctionSerializable lambdaFunction)
        {
            var lambdaFunctionPath = $"Resources.{lambdaFunction.Name}";

            if (!_jsonWriter.Exists(lambdaFunctionPath))
                return true;

            var creationTool = _jsonWriter.GetToken($"{lambdaFunctionPath}.Metadata.Tool", string.Empty);
            return string.Equals(creationTool.ToObject<string>(), "Amazon.Lambda.Annotations", StringComparison.Ordinal);
        }

        private void ProcessLambdaFunction(ILambdaFunctionSerializable lambdaFunction, string relativeProjectUri)
        {
            var lambdaFunctionPath = $"Resources.{lambdaFunction.Name}";
            var propertiesPath = $"{lambdaFunctionPath}.Properties";

            if (!_jsonWriter.Exists(lambdaFunctionPath))
                ApplyLambdaFunctionDefaults(lambdaFunctionPath, propertiesPath);

            ProcessLambdaFunctionAttributes(lambdaFunction, propertiesPath, relativeProjectUri);
            ProcessLambdaFunctionEventAttributes(lambdaFunction);
        }

        private void ProcessLambdaFunctionAttributes(ILambdaFunctionSerializable lambdaFunction, string propertiesPath, string relativeProjectUri)
        {
            if (lambdaFunction.Timeout > 0)
                _jsonWriter.SetToken($"{propertiesPath}.Timeout", lambdaFunction.Timeout);

            if (lambdaFunction.MemorySize > 0)
                _jsonWriter.SetToken($"{propertiesPath}.MemorySize", lambdaFunction.MemorySize);

            if (!string.IsNullOrEmpty(lambdaFunction.Role))
            {
                _jsonWriter.SetToken($"{propertiesPath}.Role", GetValueOrRef(lambdaFunction.Role));
                _jsonWriter.RemoveToken($"{propertiesPath}.Policies");
            }

            if (!string.IsNullOrEmpty(lambdaFunction.Policies))
            {
                var policyArray = lambdaFunction.Policies.Split(',').Select(x => GetValueOrRef(x.Trim()));
                _jsonWriter.SetToken($"{propertiesPath}.Policies", new JArray(policyArray));
                _jsonWriter.RemoveToken($"{propertiesPath}.Role");
            }

            ProcessPackageTypeProperty(lambdaFunction, propertiesPath, relativeProjectUri);
        }

        private void ProcessPackageTypeProperty(ILambdaFunctionSerializable lambdaFunction, string propertiesPath, string relativeProjectUri)
        {
            _jsonWriter.SetToken($"{propertiesPath}.PackageType", lambdaFunction.PackageType.ToString());

            switch (lambdaFunction.PackageType)
            {
                case LambdaPackageType.Zip:
                    _jsonWriter.SetToken($"{propertiesPath}.CodeUri", relativeProjectUri);
                    _jsonWriter.SetToken($"{propertiesPath}.Handler", lambdaFunction.Handler);
                    _jsonWriter.RemoveToken($"{propertiesPath}.ImageUri");
                    _jsonWriter.RemoveToken($"{propertiesPath}.ImageConfig");
                    break;

                case LambdaPackageType.Image:
                    _jsonWriter.SetToken($"{propertiesPath}.ImageUri", relativeProjectUri);
                    _jsonWriter.SetToken($"{propertiesPath}.ImageConfig.Command", new JArray(lambdaFunction.Handler));
                    _jsonWriter.RemoveToken($"{propertiesPath}.Handler");
                    _jsonWriter.RemoveToken($"{propertiesPath}.CodeUri");
                    _jsonWriter.RemoveToken($"{propertiesPath}.Runtime");
                    break;

                default:
                    throw new InvalidEnumArgumentException($"The {nameof(lambdaFunction.PackageType)} does not match any supported enums of type {nameof(LambdaPackageType)}");
            }
        }

        private void ProcessLambdaFunctionEventAttributes(ILambdaFunctionSerializable lambdaFunction)
        {
            var currentSyncedEvents = new List<string>();
            var currentSyncedResources = new List<string>();

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
                    case AttributeModel<SqsMessageAttribute> sqsAttributeModel:
                        eventName = ProcessSqsMessageAttribute(lambdaFunction, sqsAttributeModel.Data);
                        currentSyncedEvents.Add(eventName);
                        if (ShouldProcessQueue(lambdaFunction, sqsAttributeModel.Data))
                        {
                            string queueLogicalId = ProcessQueue(sqsAttributeModel.Data);
                            currentSyncedResources.Add(queueLogicalId);
                            _processedResources.Add(queueLogicalId);
                        }
                        break;
                }
            }

            var eventsPath = $"Resources.{lambdaFunction.Name}.Properties.Events";
            var metadataPath = $"Resources.{lambdaFunction.Name}.Metadata";
            var syncedEventsMetadataPath = $"{metadataPath}.SyncedEvents";
            var syncedResourcesMetadataPath = $"{metadataPath}.SyncedResources";

            if (_jsonWriter.GetToken(syncedEventsMetadataPath, new JArray()) is JArray previousSyncedEvents)
            {
                foreach (var previousEventName in previousSyncedEvents.Select(x => x.ToObject<string>()))
                {
                    if (!currentSyncedEvents.Contains(previousEventName))
                        _jsonWriter.RemoveToken($"{eventsPath}.{previousEventName}");
                }
            }

            if (_jsonWriter.GetToken(syncedResourcesMetadataPath, new JArray()) is JArray previousSyncedResources)
            {
                foreach (var previousSyncedResource in previousSyncedResources.Select(x => x.ToObject<string>()))
                {
                    if (!currentSyncedResources.Contains(previousSyncedResource))
                        _jsonWriter.RemoveToken($"{eventsPath}.{previousSyncedResources}");
                }
            }

            if (currentSyncedEvents.Any())
                _jsonWriter.SetToken(syncedEventsMetadataPath, new JArray(currentSyncedEvents));
            else
                _jsonWriter.RemoveToken(syncedEventsMetadataPath);

            if (currentSyncedResources.Any())
                _jsonWriter.SetToken(syncedResourcesMetadataPath, new JArray(currentSyncedResources));
            else
                _jsonWriter.RemoveToken(syncedResourcesMetadataPath);
        }
        private string ProcessRestApiAttribute(ILambdaFunctionSerializable lambdaFunction, RestApiAttribute restApiAttribute)
        {
            var eventPath = $"Resources.{lambdaFunction.Name}.Properties.Events";
            var methodName = restApiAttribute.Method.ToString();
            var methodPath = $"{eventPath}.Root{methodName}";

            _jsonWriter.SetToken($"{methodPath}.Type", "Api");
            _jsonWriter.SetToken($"{methodPath}.Properties.Path", restApiAttribute.Template);
            _jsonWriter.SetToken($"{methodPath}.Properties.Method", methodName.ToUpper());

            return $"Root{methodName}";
        }

        private string ProcessHttpApiAttribute(ILambdaFunctionSerializable lambdaFunction, HttpApiAttribute httpApiAttribute)
        {
            var eventPath = $"Resources.{lambdaFunction.Name}.Properties.Events";
            var methodName = httpApiAttribute.Method.ToString();
            var methodPath = $"{eventPath}.Root{methodName}";
            var version = httpApiAttribute.Version == HttpApiVersion.V1 ? "1.0" : "2.0";

            _jsonWriter.SetToken($"{methodPath}.Type", "HttpApi");
            _jsonWriter.SetToken($"{methodPath}.Properties.Path", httpApiAttribute.Template);
            _jsonWriter.SetToken($"{methodPath}.Properties.Method", methodName.ToUpper());
            _jsonWriter.SetToken($"{methodPath}.Properties.PayloadFormatVersion", version);

            return $"Root{methodName}";
        }
        private void ApplyLambdaFunctionDefaults(string lambdaFunctionPath, string propertiesPath)
        {
            _jsonWriter.SetToken($"{lambdaFunctionPath}.Type", "AWS::Serverless::Function");
            _jsonWriter.SetToken($"{lambdaFunctionPath}.Metadata.Tool", "Amazon.Lambda.Annotations");

            _jsonWriter.SetToken($"{propertiesPath}.Runtime", "dotnet6");
            _jsonWriter.SetToken($"{propertiesPath}.CodeUri", "");
            _jsonWriter.SetToken($"{propertiesPath}.MemorySize", 256);
            _jsonWriter.SetToken($"{propertiesPath}.Timeout", 30);
            _jsonWriter.SetToken($"{propertiesPath}.Policies", new JArray("AWSLambdaBasicExecutionRole"));
        }
        private void CreateNewTemplate()
        {
            var content = @"{'AWSTemplateFormatVersion' : '2010-09-09', 'Transform' : 'AWS::Serverless-2016-10-31'}";
            _jsonWriter.Parse(content);
        }
        private void RemoveOrphanedLambdaFunctions(HashSet<string> processedLambdaFunctions)
        {
            var resourceToken = _jsonWriter.GetToken("Resources") as JObject;
            if (resourceToken == null)
                return;

            var toRemove = new List<string>();
            foreach (var resource in resourceToken.Properties())
            {
                var resourcePath = $"Resources.{resource.Name}";
                var type = _jsonWriter.GetToken($"{resourcePath}.Type", string.Empty);
                var creationTool = _jsonWriter.GetToken($"{resourcePath}.Metadata.Tool", string.Empty);

                if (string.Equals(type.ToObject<string>(), "AWS::Serverless::Function", StringComparison.Ordinal)
                    && string.Equals(creationTool.ToObject<string>(), "Amazon.Lambda.Annotations", StringComparison.Ordinal)
                    && !processedLambdaFunctions.Contains(resource.Name))
                {
                    toRemove.Add(resource.Name);
                }
            }

            foreach (var resourceName in toRemove)
            {
                _jsonWriter.RemoveToken($"Resources.{resourceName}");
            }
        }

        private JToken GetValueOrRef(string value)
        {
            if (!value.StartsWith("@"))
                return value;

            var refNode = new JObject();
            refNode["Ref"] = value.Substring(1);
            return refNode;
        }

        // I don't like this being a field member, but given
        // the previous patterns, I can't find a better method
        private readonly HashSet<string> _processedResources = new HashSet<string>();
        private string ProcessQueue(SqsMessageAttribute data)
        {
            var sqsQueueTemplateInfo = GetSqsQueueLogicalIdAndPath(data);
            var propertiesPath = $"{sqsQueueTemplateInfo.Item2}.Properties";

            if (!_jsonWriter.Exists(sqsQueueTemplateInfo.Item2))
                ApplyQueueDefaults(sqsQueueTemplateInfo.Item2, propertiesPath);

            ProcessQueueAttributes(data, propertiesPath);

            return sqsQueueTemplateInfo.Item1;
        }
        private void ProcessQueueAttributes(SqsMessageAttribute sqsMessageAttribute, string propertiesPath)
        {
            try
            {
                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.ContentBasedDeduplication)}", sqsMessageAttribute.ContentBasedDeduplication, SqsMessageAttribute.ContentBasedDeduplicationDefault);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.ContentBasedDeduplication)}", e);
                }

                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.DeduplicationScope)}", sqsMessageAttribute.DeduplicationScope, string.Empty);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.DeduplicationScope)}", e);
                }

                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.DelaySeconds)}", sqsMessageAttribute.DelaySeconds, SqsMessageAttribute.DelaySecondsDefault);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.DelaySeconds)}", e);
                }

                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.FifoQueue)}", sqsMessageAttribute.FifoQueue, SqsMessageAttribute.FifoQueueDefault);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.FifoQueue)}", e);
                }

                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.FifoThroughputLimit)}", sqsMessageAttribute.FifoThroughputLimit, string.Empty);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.FifoThroughputLimit)}", e);
                }

                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.KmsDataKeyReusePeriodSeconds)}", sqsMessageAttribute.KmsDataKeyReusePeriodSeconds, SqsMessageAttribute.KmsDataKeyReusePeriodSecondsDefault);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.KmsDataKeyReusePeriodSeconds)}", e);
                }

                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.KmsMasterKeyId)}", sqsMessageAttribute.KmsMasterKeyId, string.Empty);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.KmsDataKeyReusePeriodSeconds)}", e);
                }

                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.MaximumMessageSize)}", sqsMessageAttribute.MaximumMessageSize, SqsMessageAttribute.MaximumMessageSizeDefault);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.MaximumMessageSize)}", e);
                }

                // MessageRetentionPeriod
                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.MessageRetentionPeriod)}", sqsMessageAttribute.MessageRetentionPeriod, SqsMessageAttribute.MessageRetentionPeriodDefault);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.MessageRetentionPeriod)}", e);
                }

                // QueueName
                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.QueueName)}", sqsMessageAttribute.QueueName, string.Empty);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.QueueName)}", e);
                }

                //ReceiveMessageWaitTimeSeconds
                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.ReceiveMessageWaitTimeSeconds)}", sqsMessageAttribute.ReceiveMessageWaitTimeSeconds, SqsMessageAttribute.ReceiveMessageWaitTimeSecondsDefault);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.ReceiveMessageWaitTimeSeconds)}", e);
                }

                //RedriveAllowPolicy
                try
                {
                    WriteOrRemoveAsJson($"{propertiesPath}.{nameof(ISqsMessage.RedriveAllowPolicy)}", sqsMessageAttribute.RedriveAllowPolicy);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.RedriveAllowPolicy)}", e);
                }

                //RedrivePolicy
                try
                {
                    WriteOrRemoveAsJson($"{propertiesPath}.{nameof(ISqsMessage.RedrivePolicy)}", sqsMessageAttribute.RedrivePolicy);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.RedrivePolicy)}", e);
                }

                // Tags
                try
                {
                    var tagArray = new JArray();
                    if (sqsMessageAttribute.Tags != default)
                    {
                        foreach (var tag in sqsMessageAttribute.Tags)
                        {
                            var tagParts = tag.Split('=');
                            var key = tagParts.FirstOrDefault();
                            var value = tagParts.LastOrDefault();
                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                            {
                                var tagObject = new JObject();
                                tagObject.Add(new JProperty("Key", key));
                                tagObject.Add(new JProperty("Value", value));
                                tagArray.Add(tagObject);
                            }
                        }
                    }

                    if (tagArray.Any())
                    {
                        _jsonWriter.SetToken($"{propertiesPath}.{nameof(ISqsMessage.Tags)}", tagArray);
                    }
                    else
                    {
                        _jsonWriter.RemoveToken($"{propertiesPath}.{nameof(ISqsMessage.Tags)}");
                    }

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.Tags)}", e);
                }

                try
                {
                    WriteOrRemove($"{propertiesPath}.{nameof(ISqsMessage.VisibilityTimeout)}", sqsMessageAttribute.VisibilityTimeout, SqsMessageAttribute.VisibilityTimeoutDefault);

                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to write {nameof(ISqsMessageSerializable.VisibilityTimeout)}", e);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to write AWS::SQS::Queue: {e.Message} {e.InnerException?.Message}", e);
            }
        }
        private void RemoveOrphanedResources()
        {
            var resourceToken = _jsonWriter.GetToken("Resources") as JObject;
            if (resourceToken == null)
                return;

            var toRemove = new List<string>();
            foreach (var resource in resourceToken.Properties())
            {
                var resourcePath = $"Resources.{resource.Name}";
                var type = _jsonWriter.GetToken($"{resourcePath}.Type", string.Empty);
                var creationTool = _jsonWriter.GetToken($"{resourcePath}.Metadata.Tool", string.Empty);

                if (string.Equals(type.ToObject<string>(), "AWS::SQS::Queue", StringComparison.Ordinal)
                    && string.Equals(creationTool.ToObject<string>(), "Amazon.Lambda.Annotations", StringComparison.Ordinal)
                    && !_processedResources.Contains(resource.Name))
                {
                    toRemove.Add(resource.Name);
                }
            }

            foreach (var resourceName in toRemove)
            {
                _jsonWriter.RemoveToken($"Resources.{resourceName}");
            }
        }
        private void WriteOrRemove(string path, bool value, bool defaultValue)
        {
            if (value != defaultValue)
            {
                _jsonWriter.SetToken(path, value);
            }
            else
            {
                _jsonWriter.RemoveToken(path);
            }
        }
        private void WriteOrRemoveAsJson(string path, string value)
        {

            if (!string.IsNullOrEmpty(value))
            {
                _jsonWriter.SetToken(path, JObject.Parse(value));
            }
            else
            {
                _jsonWriter.RemoveToken(path);
            }
        }
        private void WriteOrRemove(string path, string value, string defaultValue)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            if (defaultValue == null)
            {
                defaultValue = string.Empty;
            }
            if (value != defaultValue)
            {
                _jsonWriter.SetToken(path, value);
            }
            else
            {
                _jsonWriter.RemoveToken(path);
            }
        }
        private void WriteOrRemove(string path, uint value, uint defaultValue)
        {
            if (value != defaultValue)
            {
                _jsonWriter.SetToken(path, value);
            }
            else
            {
                _jsonWriter.RemoveToken(path);
            }
        }
        private void ApplyQueueDefaults(string sqsQueuePath, string propertiesPath)
        {
            _jsonWriter.SetToken($"{sqsQueuePath}.Type", "AWS::SQS::Queue");
            _jsonWriter.SetToken($"{sqsQueuePath}.Metadata.Tool", "Amazon.Lambda.Annotations");

        }
        private bool ShouldProcessQueue(ILambdaFunctionSerializable lambdaFunctionSerializable, SqsMessageAttribute data)
        {
            if (string.IsNullOrEmpty(data.QueueLogicalId)) return false;
            var sqsInfo = GetSqsQueueLogicalIdAndPath(data);
            var sqsQueuePath = sqsInfo.Item2;


            if (!_jsonWriter.Exists(sqsQueuePath))
                return true;

            var creationTool = _jsonWriter.GetToken($"{sqsQueuePath}.Metadata.Tool", string.Empty);
            return string.Equals(creationTool.ToObject<string>(), "Amazon.Lambda.Annotations", StringComparison.Ordinal);
        }
        private static (string, string) GetSqsQueueLogicalIdAndPath(SqsMessageAttribute data)
        {
            return (data.QueueLogicalId, $"Resources.{data.QueueLogicalId}");
        }
        private string ProcessSqsMessageAttribute(ILambdaFunctionSerializable lambdaFunction, SqsMessageAttribute sqsMessageAttribute)
        {
            string queueHandle;
            if (!string.IsNullOrEmpty(sqsMessageAttribute.EventQueueARN))
            {
                queueHandle = sqsMessageAttribute.EventQueueARN.Split(':').LastOrDefault().Replace("-", string.Empty);
            }
            else if (!string.IsNullOrEmpty(sqsMessageAttribute.QueueLogicalId))
            {
                queueHandle = sqsMessageAttribute.QueueLogicalId;
            }
            else
            {
                throw new InvalidOperationException($"You must specify either {nameof(ISqsMessage.EventQueueARN)} or {nameof(ISqsMessage.QueueLogicalId)}");
            }

            var eventName = $"{lambdaFunction.Name}{queueHandle}";
            var eventPath = $"Resources.{lambdaFunction.Name}.Properties.Events";
            var methodName = lambdaFunction.Name + "Sqs";
            var methodPath = $"{eventPath}.{eventName}";

            _jsonWriter.SetToken($"{methodPath}.Type", "SQS");

            var batchSizePropertyPath = $"{methodPath}.Properties.BatchSize";

            if (sqsMessageAttribute.EventBatchSize != SqsMessageAttribute.EventBatchSizeDefault)
            {
                _jsonWriter.SetToken(batchSizePropertyPath, sqsMessageAttribute.EventBatchSize);
            }
            else
            {
                _jsonWriter.RemoveToken(batchSizePropertyPath);
            }

            var queueNamePath = $"{methodPath}.Properties.Queue";
            if (!string.IsNullOrEmpty(sqsMessageAttribute.EventQueueARN))
            {
                _jsonWriter.SetToken(queueNamePath, sqsMessageAttribute.EventQueueARN);
            }
            else if (!string.IsNullOrEmpty(sqsMessageAttribute.QueueLogicalId))
            {
                _jsonWriter.SetToken(queueNamePath, new JObject(new JProperty("Ref", sqsMessageAttribute.QueueLogicalId)));
            }

            return eventName;
        }
    }
}