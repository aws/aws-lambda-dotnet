using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;

using YamlDotNet.RepresentationModel;
using LitJson;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool.Runtime
{
    /// <summary>
    /// This class handles getting the configuration information from aws-lambda-tools-defaults.json file 
    /// and possibly a CloudFormation template. YAML CloudFormation templates aren't supported yet.
    /// </summary>
    public static class LambdaDefaultsConfigFileParser
    {
        public static async Task<LambdaConfigInfo> LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Lambda config file {filePath} not found");
            }
            
            var rootData = JsonMapper.ToObject(await File.ReadAllTextAsync(filePath)) as JsonData;

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
                await ProcessServerlessTemplate(configInfo, templateFullPath);
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

        private static async Task ProcessServerlessTemplate(LambdaConfigInfo configInfo, string templateFilePath)
        {
            var content = (await File.ReadAllTextAsync(templateFilePath)).Trim();

            if(content[0] != '{')
            {
                ProcessYamlServerlessTemplate(configInfo, content);
            }
            else
            {                
                ProcessJsonServerlessTemplate(configInfo, content);                
            }
        }

        private static void ProcessYamlServerlessTemplate(LambdaConfigInfo configInfo, string content)
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(content));
            
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            if (root == null)
                return;

            YamlMappingNode resources = null;
            
            if (root.Children.ContainsKey("Resources"))
            {
                resources = root.Children["Resources"] as YamlMappingNode;
                ProcessYamlServerlessTemplateResourcesBased(configInfo, resources);
            } else if (root.Children.ContainsKey("functions"))
            {
                resources = (YamlMappingNode) root.Children["functions"];
                ProcessYamlServerlessTemplateFunctionBased(configInfo, resources);
            }


           ;
        }

        private static void ProcessYamlServerlessTemplateResourcesBased(LambdaConfigInfo configInfo, YamlMappingNode resources)
        {
            if (resources == null)
                return;

            foreach (var resource in resources.Children)
            {
                var resourceBody = (YamlMappingNode) resource.Value;
                var type = resourceBody.Children.ContainsKey("Type")
                    ? ((YamlScalarNode) resourceBody.Children["Type"])?.Value
                    : null;


                if (!string.Equals("AWS::Serverless::Function", type, StringComparison.Ordinal) &&
                    !string.Equals("AWS::Lambda::Function", type, StringComparison.Ordinal))
                {
                    continue;
                }

                var properties = resourceBody.Children.ContainsKey("Properties")
                    ? resourceBody.Children["Properties"] as YamlMappingNode
                    : null;
                if (properties == null)
                {
                    continue;
                }

                var handler = properties.Children.ContainsKey("Handler")
                    ? ((YamlScalarNode) properties.Children["Handler"])?.Value
                    : null;

                if (!string.IsNullOrEmpty(handler))
                {
                    var functionInfo = new LambdaFunctionInfo
                    {
                        Name = resource.Key.ToString(),
                        Handler = handler
                    };

                    configInfo.FunctionInfos.Add(functionInfo);
                }
            }
        }
        
        private static void ProcessYamlServerlessTemplateFunctionBased(LambdaConfigInfo configInfo, YamlMappingNode resources)
        {
            if (resources == null)
                return;

            foreach (var resource in resources.Children)
            {
                var resourceBody = (YamlMappingNode) resource.Value;

                var handler = resourceBody.Children.ContainsKey("handler")
                    ? ((YamlScalarNode) resourceBody.Children["handler"])?.Value
                    : null;

                
                if (handler == null) continue;
                if (string.IsNullOrEmpty(handler)) continue;
                
                
                var functionInfo = new LambdaFunctionInfo
                {
                    Name = resource.Key.ToString(),
                    Handler = handler
                };

                configInfo.FunctionInfos.Add(functionInfo);
            }
        }

        private static void ProcessJsonServerlessTemplate(LambdaConfigInfo configInfo, string content)
        {
            var rootData = JsonMapper.ToObject(content);

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

                if (!string.IsNullOrEmpty(handler))
                {
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
}