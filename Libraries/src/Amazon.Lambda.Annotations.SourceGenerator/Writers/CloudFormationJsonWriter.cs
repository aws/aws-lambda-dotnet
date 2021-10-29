using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    public class CloudFormationJsonWriter : IAnnotationReportWriter
    {
        private readonly IFileManager _fileManager;
        private readonly IJsonWriter _jsonWriter;

        public CloudFormationJsonWriter(IFileManager fileManager, IJsonWriter jsonWriter)
        {
            _fileManager = fileManager;
            _jsonWriter = jsonWriter;
        }

        public void ApplyReport(AnnotationReport report)
        {
            var originalContent = _fileManager.ReadAllText(report.CloudFormationTemplatePath);
            
            if (string.IsNullOrEmpty(originalContent))
                CreateNewTemplate();
            else
                _jsonWriter.Parse(originalContent);

            var processedLambdaFunctions = new HashSet<string>();
            
            foreach (var lambdaFunction in report.LambdaFunctions)
            {
                if (!ShouldProcessLambdaFunction(lambdaFunction)) 
                    continue;
                ProcessLambdaFunction(lambdaFunction);
                processedLambdaFunctions.Add(lambdaFunction.Name);
            }

            RemoveOrphanedLambdaFunctions(processedLambdaFunctions);
            _fileManager.WriteAllText(report.CloudFormationTemplatePath, _jsonWriter.GetPrettyJson());
        }

        private bool ShouldProcessLambdaFunction(ILambdaFunctionSerializable lambdaFunction)
        {
            var lambdaFunctionPath = $"Resources.{lambdaFunction.Name}";
            
            if (!_jsonWriter.Exists(lambdaFunctionPath))
                return true;
            
            var creationTool = _jsonWriter.GetToken($"{lambdaFunctionPath}.Metadata.Tool", string.Empty);
            return string.Equals(creationTool.ToObject<string>(), "Amazon.Lambda.Annotations", StringComparison.Ordinal);
        }
        
        private void ProcessLambdaFunction(ILambdaFunctionSerializable lambdaFunction)
        {
            var lambdaFunctionPath = $"Resources.{lambdaFunction.Name}";
            var propertiesPath = $"{lambdaFunctionPath}.Properties";

            if (!_jsonWriter.Exists(lambdaFunctionPath))
                ApplyLambdaFunctionDefaults(lambdaFunctionPath, propertiesPath);

            _jsonWriter.SetToken($"{propertiesPath}.Handler", lambdaFunction.Handler);

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
                var policyArray = lambdaFunction.Policies.Split(',')
                    .Select(x => GetValueOrRef(x.Trim()));
                _jsonWriter.SetToken($"{propertiesPath}.Policies", new JArray(policyArray));
                _jsonWriter.RemoveToken($"{propertiesPath}.Role");
            }
            
            // Processing of RestApiRoutes will be added after RestApiRouteAttribute is modeled.
        }
        
        private void ApplyLambdaFunctionDefaults(string lambdaFunctionPath, string propertiesPath)
        {
            _jsonWriter.SetToken($"{lambdaFunctionPath}.Type", "AWS::Serverless::Function");
            _jsonWriter.SetToken($"{lambdaFunctionPath}.Metadata.Tool", "Amazon.Lambda.Annotations");
            
            _jsonWriter.SetToken($"{propertiesPath}.Runtime", "dotnetcore3.1");
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