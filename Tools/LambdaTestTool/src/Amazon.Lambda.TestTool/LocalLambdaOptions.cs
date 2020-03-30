using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.TestTool.Runtime;

namespace Amazon.Lambda.TestTool
{
    public class LocalLambdaOptions
    {
        public int? Port { get; set; }

        public IList<string> LambdaConfigFiles { get; set; }

        public ILocalLambdaRuntime LambdaRuntime { get; set; }

        public LambdaFunction LoadLambdaFuntion(string configFile, string functionHandler)
        {
            var fullConfigFilePath = this.LambdaConfigFiles.FirstOrDefault(x =>
                string.Equals(configFile, x, StringComparison.OrdinalIgnoreCase) || string.Equals(configFile, Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
            if (fullConfigFilePath == null)
            {
                throw new Exception($"{configFile} is not a config file for this project");
            }
            
            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(fullConfigFilePath);
            return LoadLambdaFuntion(configInfo, functionHandler);
        }

        public bool TryLoadLambdaFuntion(LambdaConfigInfo configInfo, string functionHandler, out LambdaFunction lambdaFunction)
        {
            lambdaFunction = null;
            try
            {
                lambdaFunction = LoadLambdaFuntion(configInfo, functionHandler);
                return true;
            }
            catch { }

            return false;
        }

        public LambdaFunction LoadLambdaFuntion(LambdaConfigInfo configInfo, string functionHandler)
        {
            var functionInfo = configInfo.FunctionInfos.FirstOrDefault(x =>
                string.Equals(functionHandler, x.Handler, StringComparison.OrdinalIgnoreCase));
            if (functionInfo == null)
            {
                throw new Exception($"Failed to find function {functionHandler}");
            }

            var function = this.LambdaRuntime.LoadLambdaFunction(functionInfo);
            return function;
        }        

        /// <summary>
        /// The directory to store in local settings for a Lambda project for example saved Lambda requests.
        /// </summary>
        public string GetPreferenceDirectory(bool createIfNotExist)
        {
            var currentDirectory = this.LambdaRuntime.LambdaAssemblyDirectory;
            while (currentDirectory != null && !Utils.IsProjectDirectory(currentDirectory))
            {
                currentDirectory = Directory.GetParent(currentDirectory).FullName;
            }

            if (currentDirectory == null)
                currentDirectory = this.LambdaRuntime.LambdaAssemblyDirectory;

            var preferenceDirectory = Path.Combine(currentDirectory, ".lambda-test-tool");
            if(createIfNotExist && !Directory.Exists(preferenceDirectory))
            {
                Directory.CreateDirectory(preferenceDirectory);
            }

            return preferenceDirectory;
        }
    }
}
