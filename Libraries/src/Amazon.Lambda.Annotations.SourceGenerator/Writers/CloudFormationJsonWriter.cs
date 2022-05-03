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
                }
            }

            var eventsPath = $"Resources.{lambdaFunction.Name}.Properties.Events";
            var syncedEventsMetadataPath = $"Resources.{lambdaFunction.Name}.Metadata.SyncedEvents";

            if (_jsonWriter.GetToken(syncedEventsMetadataPath, new JArray()) is JArray previousSyncedEvents)
            {
                foreach (var previousEventName in previousSyncedEvents.Select(x => x.ToObject<string>()))
                {
                    if (!currentSyncedEvents.Contains(previousEventName))
                        _jsonWriter.RemoveToken($"{eventsPath}.{previousEventName}");
                }
            }

            if (currentSyncedEvents.Any())
                _jsonWriter.SetToken(syncedEventsMetadataPath, new JArray(currentSyncedEvents));
            else
                _jsonWriter.RemoveToken(syncedEventsMetadataPath);
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
    }
}