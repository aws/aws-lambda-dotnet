using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    public class CloudFormationTemplateFinder
    {

        public CloudFormationTemplateFinder()
        {
        }

        public string DetermineProjectRootDirectory(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                return string.Empty;

            var directoryPath = Path.GetDirectoryName(sourceFilePath);
            while (!string.IsNullOrEmpty(directoryPath))
            {
                if (Directory.GetFiles(directoryPath, "*.csproj").Length == 1)
                    return directoryPath;
                directoryPath = Path.GetDirectoryName(directoryPath);
            }

            return string.Empty;
        }

        public string FindCloudFormationTemplate(string projectRootDirectory)
        {
            if (!Directory.Exists(projectRootDirectory))
                throw new DirectoryNotFoundException("Failed to find the project root directory");

            var templateAbsolutePath = string.Empty;
            
            var defaultConfigFile = Directory
                .GetFiles(projectRootDirectory, "aws-lambda-tools-defaults.json", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (File.Exists(defaultConfigFile))
                // the templateAbsolutePath will be empty if the template property is not found in the default config file
                templateAbsolutePath = GetTemplatePathFromDefaultConfigFile(defaultConfigFile);

            // if the default config file does not exist or if the template property is not found in the default config file
            // set the template path inside the project root directory. 
            if (string.IsNullOrEmpty(templateAbsolutePath))
                templateAbsolutePath = Path.Combine(projectRootDirectory, "serverless.template");
                
            if (!File.Exists(templateAbsolutePath))
                File.Create(templateAbsolutePath).Close();
            
            return templateAbsolutePath;
        }

        private string GetTemplatePathFromDefaultConfigFile(string defaultConfigFile)
        {
            JToken rootToken;
            try
            {
                rootToken = JObject.Parse(File.ReadAllText(defaultConfigFile));
            }
            catch (Exception)
            {
                return string.Empty;    
            }
            
            var templateRelativePath = rootToken["template"]?.ToObject<string>();
            
            if (string.IsNullOrEmpty(templateRelativePath))
                return string.Empty;

            var templateAbsolutePath = Path.Combine(Path.GetDirectoryName(defaultConfigFile), templateRelativePath);
            return templateAbsolutePath;
        }
    }
}