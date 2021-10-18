using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    public class CloudFormationTemplateFinder
    {
        private readonly IFileSystem _fileSystem;

        public CloudFormationTemplateFinder(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public string DetermineProjectRootDirectory(string sourceFilePath)
        {
            if (!_fileSystem.File.Exists(sourceFilePath))
                return string.Empty;

            var directoryPath = _fileSystem.Path.GetDirectoryName(sourceFilePath);
            while (!string.IsNullOrEmpty(directoryPath))
            {
                if (_fileSystem.Directory.GetFiles(directoryPath, "*.csproj").Length == 1)
                    return directoryPath;
                directoryPath = _fileSystem.Path.GetDirectoryName(directoryPath);
            }

            return string.Empty;
        }

        public string FindCloudFormationTemplate(string projectRootDirectory)
        {
            if (!_fileSystem.Directory.Exists(projectRootDirectory))
                throw new DirectoryNotFoundException("Failed to find the project root directory");

            var templateAbsolutePath = string.Empty;
            
            var defaultConfigFile = _fileSystem.Directory
                .GetFiles(projectRootDirectory, "aws-lambda-tools-defaults.json", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (_fileSystem.File.Exists(defaultConfigFile))
                // the templateAbsolutePath will be empty if the template property is not found in the default config file
                templateAbsolutePath = GetTemplatePathFromDefaultConfigFile(defaultConfigFile);

            // if the default config file does not exist or if the template property is not found in the default config file
            // set the template path inside the project root directory. 
            if (string.IsNullOrEmpty(templateAbsolutePath))
                templateAbsolutePath = Path.Combine(projectRootDirectory, "serverless.template");
                
            if (!_fileSystem.File.Exists(templateAbsolutePath))
                _fileSystem.File.Create(templateAbsolutePath).Close();
            
            return templateAbsolutePath;
        }

        private string GetTemplatePathFromDefaultConfigFile(string defaultConfigFile)
        {
            JToken rootToken;
            try
            {
                rootToken = JObject.Parse(_fileSystem.File.ReadAllText(defaultConfigFile));
            }
            catch (Exception)
            {
                return string.Empty;    
            }
            
            var templateRelativePath = rootToken["template"]?.ToObject<string>();
            
            if (string.IsNullOrEmpty(templateRelativePath))
                return string.Empty;

            var templateAbsolutePath = _fileSystem.Path.Combine(_fileSystem.Path.GetDirectoryName(defaultConfigFile), templateRelativePath);
            return templateAbsolutePath;
        }
    }
}