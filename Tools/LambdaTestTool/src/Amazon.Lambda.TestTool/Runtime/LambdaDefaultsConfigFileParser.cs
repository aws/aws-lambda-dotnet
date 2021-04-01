using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

using YamlDotNet.RepresentationModel;


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

            var configFile = JsonSerializer.Deserialize<LambdaConfigFile>(File.ReadAllText(filePath).Trim(), new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            configFile.ConfigFileLocation = filePath;

            return LoadFromFile(configFile);
        }

        public static LambdaConfigInfo LoadFromFile(LambdaConfigFile configFile)
        {
            var configInfo = new LambdaConfigInfo
            {
                AWSProfile = !string.IsNullOrEmpty(configFile.Profile) ? configFile.Profile : "default",
                AWSRegion = !string.IsNullOrEmpty(configFile.Region) ? configFile.Region : null,
                FunctionInfos = new List<LambdaFunctionInfo>()
            };

            if (string.IsNullOrEmpty(configInfo.AWSProfile))
                configInfo.AWSProfile = "default";


            var templateFileName = !string.IsNullOrEmpty(configFile.Template) ? configFile.Template : null;
            var functionHandler = !string.IsNullOrEmpty(configFile.DetermineHandler()) ? configFile.DetermineHandler() : null;

            if (!string.IsNullOrEmpty(templateFileName))
            {
                var directory = Directory.Exists(configFile.ConfigFileLocation) ? configFile.ConfigFileLocation : Path.GetDirectoryName(configFile.ConfigFileLocation);
                var templateFullPath = Path.Combine(directory, templateFileName);
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

                info.Name = !string.IsNullOrEmpty(configFile.FunctionName) ? configFile.FunctionName : null;
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

                string handler = null;
                if(properties.Children.ContainsKey("Handler"))
                {
                    handler = ((YamlScalarNode)properties.Children["Handler"])?.Value;
                }

                if (string.IsNullOrEmpty(handler) && properties.Children.ContainsKey("ImageConfig"))
                {
                    var imageConfigNode = properties.Children["ImageConfig"] as YamlMappingNode;
                    if (imageConfigNode.Children.ContainsKey("Command"))
                    {
                        var imageCommandNode = imageConfigNode.Children["Command"] as YamlSequenceNode;
                        // Grab the first element assuming that is the function handler.
                        var en = imageCommandNode.GetEnumerator();
                        en.MoveNext();
                        handler = ((YamlScalarNode)en.Current)?.Value;
                    }
                }

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
            var rootData = JsonDocument.Parse(content);

            JsonElement resourcesNode;
            if (!rootData.RootElement.TryGetProperty("Resources", out resourcesNode))
                return;

            foreach (var resourceProperty in resourcesNode.EnumerateObject())
            {
                var resource = resourceProperty.Value;

                JsonElement typeProperty;
                if (!resource.TryGetProperty("Type", out typeProperty))
                    continue;

                var type = typeProperty.GetString();

                JsonElement propertiesProperty;
                if (!resource.TryGetProperty("Properties", out propertiesProperty))
                    continue;


                if (!string.Equals("AWS::Serverless::Function", type, StringComparison.Ordinal) &&
                    !string.Equals("AWS::Lambda::Function", type, StringComparison.Ordinal))
                {
                    continue;
                }

                string handler = null;
                if (propertiesProperty.TryGetProperty("Handler", out var handlerProperty))
                {
                    handler = handlerProperty.GetString();
                }
                else if(propertiesProperty.TryGetProperty("ImageConfig", out var imageConfigProperty) &&
                        imageConfigProperty.TryGetProperty("Command", out var imageCommandProperty))
                {
                    if(imageCommandProperty.GetArrayLength() > 0)
                    {
                        // Grab the first element assuming that is the function handler.
                        var en = imageCommandProperty.EnumerateArray();
                        en.MoveNext();
                        handler = en.Current.GetString();
                    }
                }

                if (!string.IsNullOrEmpty(handler))
                {
                    var functionInfo = new LambdaFunctionInfo
                    {
                        Name = resourceProperty.Name,
                        Handler = handler
                    };

                    configInfo.FunctionInfos.Add(functionInfo);
                }
            }
        }
    }
}