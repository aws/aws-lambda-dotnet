using Amazon.Lambda.TestTool.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Amazon.Lambda.TestTool.Services
{
    public class AwsProfileConfig : IAwsProfileConfig
    {
        private IList<string> availableAWSProfiles;

        private IList<LambdaFunction> availableFunctions;

        private LambdaConfigInfo lamdaConfigInfo;

        private string configFile;

        private readonly LocalLambdaOptions _options;

        public AwsProfileConfig(LocalLambdaOptions options)
        {
            _options = options;
        }

        public string ConfigFile()
        {
            if (string.IsNullOrEmpty(configFile))
            {
                if (_options.LambdaConfigFiles.Count > 0)
                {
                    try
                    {
                        configFile = _options.LambdaConfigFiles[0];
                    }
                    catch (Exception)
                    {

                    }
                }

            }
            return configFile;
        }

        public LambdaConfigInfo LambdaConfigInfo()
        {
            if (lamdaConfigInfo == null)
            {
                try
                {
                    lamdaConfigInfo = LambdaDefaultsConfigFileParser.LoadFromFile(ConfigFile());

                }
                catch (Exception)
                {

                }
            }
            return lamdaConfigInfo;
        }

        public IList<LambdaFunction> AvailableFunctions()
        {
            if (availableFunctions == null)
            {
                try
                {
                    availableFunctions = this._options.LambdaRuntime.LoadLambdaFunctions(LambdaConfigInfo().FunctionInfos);

                }
                catch (Exception)
                {

                }
            }
            return availableFunctions;
        }

        public IList<string> AvailableAWSProfiles()
        {
            if (availableAWSProfiles == null)
            {
                try
                {
                    availableAWSProfiles = this._options.LambdaRuntime.AWSService.ListProfiles();

                }
                catch (Exception)
                {

                }
            }
            return availableAWSProfiles;
        }
    }
}
