using System;
using System.IO;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    /// <summary>
    /// This class contains utility methods to determine the .NET project's root directory and resolve the AWS serverless template file path.
    /// </summary>
    public class CloudFormationTemplateHandler
    {
        private const string DEFAULT_CONFIG_FILE_NAME = "aws-lambda-tools-defaults.json";
        private const string DEFAULT_SERVERLESS_TEMPLATE_NAME = "serverless.template";

        private readonly IFileManager _fileManager;
        private readonly IDirectoryManager _directoryManager;

        public CloudFormationTemplateHandler(IFileManager fileManager, IDirectoryManager directoryManager)
        {
            _fileManager = fileManager;
            _directoryManager = directoryManager;
        }

        /// <summary>
        /// This method takes any file path in the customer's .NET project and resolves the project root directory.
        /// The root directory is the folder that contains the .csproj file.
        /// </summary>
        /// <returns>The .NET project root directory path</returns>
        public string DetermineProjectRootDirectory(string sourceFilePath)
        {
            if (!_fileManager.Exists(sourceFilePath))
                return string.Empty;

            var directoryPath = _directoryManager.GetDirectoryName(sourceFilePath);
            while (!string.IsNullOrEmpty(directoryPath))
            {
                if (_directoryManager.GetFiles(directoryPath, "*.csproj").Length == 1)
                    return directoryPath;
                directoryPath = _directoryManager.GetDirectoryName(directoryPath);
            }

            return string.Empty;
        }

        /// <summary>
        /// Determines the path to the AWS serverless template file.
        /// If the file does not exist then an empty file is created before returning the path.
        /// </summary>
        public string FindTemplate(string projectRootDirectory)
        {
            var templateAbsolutePath = DetermineTemplatePath(projectRootDirectory);

            if (!_fileManager.Exists(templateAbsolutePath))
                _fileManager.Create(templateAbsolutePath).Close();

            return templateAbsolutePath;
        }

        /// <summary>
        /// Checks if the AWS serverless template file exists.
        /// </summary>
        public bool DoesTemplateExist(string projectRootDirectory)
        {
            var templateAbsolutePath = DetermineTemplatePath(projectRootDirectory);
            return _fileManager.Exists(templateAbsolutePath);
        }

        /// <summary>
        /// Determines the file format of the AWS serverless template.
        /// If the template does not exist or if the template is empty, then by default <see cref="CloudFormationTemplateFormat.Json"/> is returned.
        /// </summary>
        public CloudFormationTemplateFormat DetermineTemplateFormat(string templatePath)
        {
            if (!_fileManager.Exists(templatePath))
            {
                return CloudFormationTemplateFormat.Json;
            }

            var content = _fileManager.ReadAllText(templatePath);
            content = content.Trim();
            if (string.IsNullOrEmpty(content))
            {
                return CloudFormationTemplateFormat.Json;
            }

            return content[0] == '{' ? CloudFormationTemplateFormat.Json : CloudFormationTemplateFormat.Yaml;
        }

        /// <summary>
        /// This is a helper method to determine the path to the AWS serverless template file.
        /// It will first look for <see cref="DEFAULT_CONFIG_FILE_NAME"/> inside the project root directory and will try to resolve the template file path from the `template` property.
        /// If <see cref="DEFAULT_CONFIG_FILE_NAME"/> does not exist or if the 'template' property is not found, then default to projectRootDirectory/<see cref="DEFAULT_SERVERLESS_TEMPLATE_NAME"/>
        /// </summary>
        private string DetermineTemplatePath(string projectRootDirectory)
        {
            if (!_directoryManager.Exists(projectRootDirectory))
                throw new DirectoryNotFoundException("Failed to find the project root directory");

            var templateAbsolutePath = string.Empty;

            var defaultConfigFile = _directoryManager.GetFiles(projectRootDirectory, DEFAULT_CONFIG_FILE_NAME, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (_fileManager.Exists(defaultConfigFile))
                // the templateAbsolutePath will be empty if the template property is not found in the default config file
                templateAbsolutePath = GetTemplatePathFromDefaultConfigFile(defaultConfigFile);

            // if the default config file does not exist or if the template property is not found in the default config file
            // set the template path inside the project root directory.
            if (string.IsNullOrEmpty(templateAbsolutePath))
                templateAbsolutePath = Path.Combine(projectRootDirectory, DEFAULT_SERVERLESS_TEMPLATE_NAME);

            return templateAbsolutePath;
        }
        /// <summary>
        /// This method parses the default config file and tries to resolve the serverless template path from the 'template' property.
        /// </summary>
        private string GetTemplatePathFromDefaultConfigFile(string defaultConfigFile)
        {
            JToken rootToken;
            try
            {
                rootToken = JObject.Parse(_fileManager.ReadAllText(defaultConfigFile));
            }
            catch (Exception)
            {
                return string.Empty;
            }

            var templateRelativePath = rootToken["template"]?
                .ToObject<string>()?
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (string.IsNullOrEmpty(templateRelativePath))
                return string.Empty;

            var templateAbsolutePath = Path.Combine(_directoryManager.GetDirectoryName(defaultConfigFile), templateRelativePath);
            return templateAbsolutePath;
        }
    }
}