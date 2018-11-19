using System;
using System.IO;
using System.Collections.Generic;

using LitJson;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Rewrite.Internal.ApacheModRewrite;

namespace Amazon.Lambda.TestTool.Runtime
{
    /// <summary>
    /// This class handles getting the configuration information from aws-lambda-tools-defaults.json file 
    /// and possibly a CloudFormation template. YAML CloudFormation templates aren't supported yet.
    /// </summary>
    public static class LambdaDefaultsConfigFileParser
    {
        public static LambdaConfigInfo LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Lambda config file {filePath} not found");
            }
            
            var rootData = JsonMapper.ToObject(File.ReadAllText(filePath)) as JsonData;

            var configInfo = new LambdaConfigInfo
            {
                AWSProfile = rootData.ContainsKey("profile") ? rootData["profile"]?.ToString() : "default",
                AWSRegion = rootData.ContainsKey("region") ? rootData["region"]?.ToString() : null,
                FunctionInfos = new List<LambdaFunctionInfo>()
            };

            if (string.IsNullOrEmpty(configInfo.AWSProfile))
                configInfo.AWSProfile = "default";


            var templateFileName = rootData.ContainsKey("template") ? rootData["template"]?.ToString() : null;
            var functionHandler = rootData.ContainsKey("function-handler") ? rootData["function-handler"]?.ToString() : null;

            if (!string.IsNullOrEmpty(templateFileName))
            {
                var templateFullPath = Path.Combine(Path.GetDirectoryName(filePath), templateFileName);
                if (!File.Exists(templateFullPath))
                {
                    throw new FileNotFoundException($"Serverless template file {templateFullPath} not found");
                }
                ProcessServerlessTemplate(configInfo, templateFullPath);
            }
            else if(!string.IsNullOrEmpty(functionHandler))
            {
                var info = new LambdaFunctionInfo
                {
                    Handler = functionHandler
                };

                info.Name = rootData.ContainsKey("function-name") ? rootData["function-name"]?.ToString() : null;
                if (string.IsNullOrEmpty(info.Name))
                {
                    info.Name = functionHandler;
                }
            
                configInfo.FunctionInfos.Add(info);
            }
            
            return configInfo;
        }

        private static void ProcessServerlessTemplate(LambdaConfigInfo configInfo, string templateFilePath)
        {
            var content = File.ReadAllText(templateFilePath).Trim();

            if(content[0] != '{')
            {
                // TODO: Implement YAML support.
                var message = ".NET Lambda Test Tool does not currently YAML CloudFormation templates.";
                Console.Error.WriteLine(message);
                throw new NotImplementedException(message);
            }

            var rootData = JsonMapper.ToObject(content);
            
            ProcessJsonServerlessTemplate(configInfo, rootData);
        }

        private static void ProcessJsonServerlessTemplate(LambdaConfigInfo configInfo, JsonData rootData)
        {
            var resourcesNode = rootData.ContainsKey("Resources") ? rootData["Resources"] : null as JsonData;
            if (resourcesNode == null)
                return;

            foreach (var key in resourcesNode.Keys)
            {
                var resource = resourcesNode[key];
                var type = resource.ContainsKey("Type") ? resource["Type"]?.ToString() : null;
                var properties = resource.ContainsKey("Properties") ? resource["Properties"] : null;

                if (properties == null)
                    continue;

                if (!string.Equals("AWS::Serverless::Function", type, StringComparison.Ordinal) &&
                    !string.Equals("AWS::Lambda::Function", type, StringComparison.Ordinal))
                {
                    continue;
                }

                var handler = properties.ContainsKey("Handler") ? properties["Handler"]?.ToString() : null;

                var functionInfo = new LambdaFunctionInfo
                {
                    Name = key,
                    Handler = handler
                };
                    
                configInfo.FunctionInfos.Add(functionInfo);

            }
        }
    }
}