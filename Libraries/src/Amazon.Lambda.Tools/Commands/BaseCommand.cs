using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon;
using Amazon.Runtime;
using Amazon.Lambda;

using Amazon.CloudFormation;

using Amazon.IdentityManagement;

using Amazon.S3;

using Amazon.Lambda.Tools.Options;
using System.Reflection;

namespace Amazon.Lambda.Tools.Commands
{

    public abstract class BaseCommand: ICommand
    {
        /// <summary>
        /// The common options used by every command
        /// </summary>
        protected static readonly IList<CommandOption> CommonOptions = new List<CommandOption>
        {
            DefinedCommandOptions.ARGUMENT_AWS_REGION,
            DefinedCommandOptions.ARGUMENT_AWS_PROFILE,
            DefinedCommandOptions.ARGUMENT_AWS_PROFILE_LOCATION,
            DefinedCommandOptions.ARGUMENT_PROJECT_LOCATION,
            DefinedCommandOptions.ARGUMENT_CONFIG_FILE
        };

        public abstract Task<bool> ExecuteAsync();

        /// <summary>
        /// Used to combine the command specific command options with the common options.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        protected static IList<CommandOption> BuildLineOptions(List<CommandOption> options)
        {
            var list = new List<CommandOption>();
            list.AddRange(CommonOptions);
            list.AddRange(options);

            return list;
        }

        public string Region { get; set; }
        public string Profile { get; set; }
        public string ProfileLocation { get; set; }
        public AWSCredentials Credentials { get; set; }
        public string ProjectLocation { get; set; }
        public string ConfigFile { get; set; }



        public IToolLogger Logger { get; protected set; }
        public string WorkingDirectory { get; set; }

        public LambdaToolsException LastToolsException { get; protected set; }


        LambdaToolsDefaults _defaultConfig;
        public LambdaToolsDefaults DefaultConfig
        {
            get
            {
                if (this._defaultConfig == null)
                {
                    this._defaultConfig = LambdaToolsDefaultsReader.LoadDefaults(Utilities.DetermineProjectLocation(this.WorkingDirectory, this.ProjectLocation), this.ConfigFile);
                }
                return this._defaultConfig;
            }
        }

        private static void SetUserAgentString()
        {
            string version = typeof(BaseCommand).GetTypeInfo().Assembly.GetName().Version.ToString();
            Util.Internal.InternalSDKUtils.SetUserAgent("AWSLambdaToolsDotnet",
                                          version);
        }


        /// <summary>
        /// Disable all Console.Read operations to make sure the command is never blocked waiting for input. This is 
        /// used by the AWS Visual Studio Toolkit to make sure it never gets blocked.
        /// </summary>
        public bool EnableInteractive { get; set; } = false;

        IAmazonLambda _lambdaClient;
        public IAmazonLambda LambdaClient
        {
            get
            {
                if (this._lambdaClient == null)
                {
                    this._lambdaClient = CreateLambdaClient();
                }
                return this._lambdaClient;
            }
            set { this._lambdaClient = value; }
        }

        IAmazonCloudFormation _cloudFormationClient;
        public IAmazonCloudFormation CloudFormationClient
        {
            get
            {
                if (this._cloudFormationClient == null)
                {
                    this._cloudFormationClient = CreateCloudFormationClient();
                }
                return this._cloudFormationClient;
            }
            set { this._cloudFormationClient = value; }
        }


        IAmazonS3 _s3Client;
        public IAmazonS3 S3Client
        {
            get
            {
                if (this._s3Client == null)
                {
                    this._s3Client = CreateS3Client();
                }
                return this._s3Client;
            }
            set { this._s3Client = value; }
        }


        IAmazonIdentityManagementService _iamClient;
        public IAmazonIdentityManagementService IAMClient
        {
            get
            {
                if (this._iamClient == null)
                {
                    this._iamClient = CreateIAMClient();
                }
                return this._iamClient;
            }
            set { this._iamClient = value; }
        }


        public BaseCommand(IToolLogger logger, string workingDirectory)
        {
            this.Logger = logger;
            this.WorkingDirectory = workingDirectory;
        }

        public BaseCommand(IToolLogger logger, string workingDirectory, IList<CommandOption> possibleOptions, string[] args)
            : this(logger, workingDirectory)
        {
            var values = CommandLineParser.ParseArguments(possibleOptions, args);
            ParseCommandArguments(values);
        }

        /// <summary>
        /// Parse the CommandOptions into the Properties on the command.
        /// </summary>
        /// <param name="values"></param>
        protected virtual void ParseCommandArguments(CommandOptions values)
        {
            Tuple<CommandOption, CommandOptionValue> tuple;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_AWS_PROFILE.Switch)) != null)
                this.Profile = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_AWS_PROFILE_LOCATION.Switch)) != null)
                this.ProfileLocation = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_AWS_REGION.Switch)) != null)
                this.Region = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_PROJECT_LOCATION.Switch)) != null)
                this.ProjectLocation = tuple.Item2.StringValue;
            if ((tuple = values.FindCommandOption(DefinedCommandOptions.ARGUMENT_CONFIG_FILE.Switch)) != null)
                this.ConfigFile = tuple.Item2.StringValue;

            if (string.IsNullOrEmpty(this.ConfigFile))
                this.ConfigFile = LambdaToolsDefaultsReader.DEFAULT_FILE_NAME;
        }

        private IAmazonLambda CreateLambdaClient()
        {
            // If the client is being created then the LambdaTools
            // is not being invoked from the VS toolkit. The toolkit will pass in
            // its configured client.
            SetUserAgentString();


            AmazonLambdaConfig config = new AmazonLambdaConfig();
            config.RegionEndpoint = DetermineAWSRegion();

            IAmazonLambda client = new AmazonLambdaClient(DetermineAWSCredentials(), config);
            return client;
        }

        private IAmazonCloudFormation CreateCloudFormationClient()
        {
            // If the client is being created then the LambdaTools
            // is not being invoked from the VS toolkit. The toolkit will pass in
            // its configured client.
            SetUserAgentString();

            AmazonCloudFormationConfig config = new AmazonCloudFormationConfig();

            config.RegionEndpoint = DetermineAWSRegion();

            var client = new AmazonCloudFormationClient(DetermineAWSCredentials(), config);
            return client;
        }

        private IAmazonIdentityManagementService CreateIAMClient()
        {
            // If the client is being created then the LambdaTools
            // is not being invoked from the VS toolkit. The toolkit will pass in
            // its configured Lambda client.
            SetUserAgentString();

            AmazonIdentityManagementServiceConfig config = new AmazonIdentityManagementServiceConfig();

            config.RegionEndpoint = DetermineAWSRegion();

            IAmazonIdentityManagementService client = new AmazonIdentityManagementServiceClient(DetermineAWSCredentials(), config);
            return client;

        }

        private IAmazonS3 CreateS3Client()
        {
            // If the client is being created then the LambdaTools
            // is not being invoked from the VS toolkit. The toolkit will pass in
            // its configured client.
            SetUserAgentString();

            AmazonS3Config config = new AmazonS3Config();

            config.RegionEndpoint = DetermineAWSRegion();

            IAmazonS3 client = new AmazonS3Client(DetermineAWSCredentials(), config);
            return client;
        }

        private AWSCredentials DetermineAWSCredentials()
        {
            AWSCredentials credentials;
            if (this.Credentials != null)
            {
                credentials = this.Credentials;
            }
            else
            {
                var profile = this.Profile;
                if (string.IsNullOrEmpty(profile))
                {
                    profile = DefaultConfig[DefinedCommandOptions.ARGUMENT_AWS_PROFILE.Switch] as string;
                }

                if (!string.IsNullOrEmpty(profile))
                {
                    if (!StoredProfileAWSCredentials.IsProfileKnown(profile, this.ProfileLocation))
                    {
                        throw new LambdaToolsException($"Profile {profile} cannot be found", LambdaToolsException.ErrorCode.ProfileNotFound);
                    }
                    if (!StoredProfileAWSCredentials.CanCreateFrom(profile, this.ProfileLocation))
                    {
                        throw new LambdaToolsException($"Cannot create AWS credentials for profile {profile}", LambdaToolsException.ErrorCode.ProfileNotCreateable);
                    }
                    credentials = new StoredProfileAWSCredentials(profile, this.ProfileLocation);
                }
                else
                {
                    credentials = FallbackCredentialsFactory.GetCredentials();
                }
            }

            return credentials;
        }

        private RegionEndpoint DetermineAWSRegion()
        {
            // See if a region has been set but don't prompt if not set.
            var regionName = this.GetStringValueOrDefault(this.Region, DefinedCommandOptions.ARGUMENT_AWS_REGION, false);
            if(!string.IsNullOrWhiteSpace(regionName))
            {
                return RegionEndpoint.GetBySystemName(regionName);
            }

            // See if we can find a region using the region fallback logic.
            if(string.IsNullOrWhiteSpace(regionName))
            {
                var region = FallbackRegionFactory.GetRegionEndpoint(true);
                if (region != null)
                {
                    return region;
                }
            }

            // If we still don't have a region prompt the user for a region.
            regionName = this.GetStringValueOrDefault(this.Region, DefinedCommandOptions.ARGUMENT_AWS_REGION, true);
            if (!string.IsNullOrWhiteSpace(regionName))
            {
                return RegionEndpoint.GetBySystemName(regionName);
            }

            throw new LambdaToolsException("Can not determine AWS region. Either configure a default region or use the --region option.", LambdaToolsException.ErrorCode.RegionNotConfigured);
        }

        /// <summary>
        /// Gets the value for the CommandOption either through the property value which means the 
        /// user explicity set the value or through defaults for the project. 
        /// 
        /// If no value is found in either the property value or the defaults and the value
        /// is required the user will be prompted for the value if we are running in interactive
        /// mode.
        /// </summary>
        /// <param name="propertyValue"></param>
        /// <param name="option"></param>
        /// <param name="required"></param>
        /// <returns></returns>
        public string GetStringValueOrDefault(string propertyValue, CommandOption option, bool required)
        {
            if (!string.IsNullOrEmpty(propertyValue))
            {
                // If the user gave the short name of the role and not the ARN then look up the role and get its ARN.
                if ((option == DefinedCommandOptions.ARGUMENT_FUNCTION_ROLE || option == DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_ROLE)
                    && !propertyValue.StartsWith(Constants.IAM_ARN_PREFIX))
                {
                    return RoleHelper.ExpandRoleName(this.IAMClient, propertyValue);
                }
                return propertyValue;
            }
            else if (!string.IsNullOrEmpty(DefaultConfig[option.Switch] as string))
            {
                var configDefault = DefaultConfig[option.Switch] as string;
                // If the user gave the short name of the role and not the ARN then look up the role and get its ARN.
                if (configDefault != null && 
                    (option == DefinedCommandOptions.ARGUMENT_FUNCTION_ROLE || option == DefinedCommandOptions.ARGUMENT_CLOUDFORMATION_ROLE) && 
                    !configDefault.StartsWith(Constants.IAM_ARN_PREFIX))
                {
                    return RoleHelper.ExpandRoleName(this.IAMClient, configDefault);
                }
                return configDefault;
            }
            else if (required && this.EnableInteractive)
            {
                return PromptForValue(option);
            }
            else if (_cachedRequestedValues.ContainsKey(option))
            {
                var cachedValue = _cachedRequestedValues[option];
                return cachedValue;
            }

            return null;
        }

        /// <summary>
        /// String[] version of GetStringValueOrDefault
        /// </summary>
        /// <param name="propertyValue"></param>
        /// <param name="option"></param>
        /// <param name="required"></param>
        /// <returns></returns>
        public string[] GetStringValuesOrDefault(string[] propertyValue, CommandOption option, bool required)
        {
            if (propertyValue != null)
            {
                return propertyValue;
            }
            else if (!string.IsNullOrEmpty(DefaultConfig[option.Switch] as string))
            {
                var configDefault = DefaultConfig[option.Switch] as string;
                if (string.IsNullOrEmpty(configDefault))
                    return null;

                return configDefault.SplitByComma();
            }
            else if (required && this.EnableInteractive)
            {
                var response = PromptForValue(option);
                if (string.IsNullOrEmpty(response))
                    return null;

                return response.SplitByComma();
            }
            else if (_cachedRequestedValues.ContainsKey(option))
            {
                var cachedValue = _cachedRequestedValues[option];
                return cachedValue?.SplitByComma();
            }


            return null;
        }

        public Dictionary<string, string> GetKeyValuePairOrDefault(Dictionary<string, string> propertyValue, CommandOption option, bool required)
        {
            if (propertyValue != null)
            {
                return propertyValue;
            }
            else if (!string.IsNullOrEmpty(DefaultConfig[option.Switch] as string))
            {
                var configDefault = DefaultConfig[option.Switch] as string;
                if (string.IsNullOrEmpty(configDefault))
                    return null;

                return Utilities.ParseKeyValueOption(configDefault);
            }
            else if (required && this.EnableInteractive)
            {
                var response = PromptForValue(option);
                if (string.IsNullOrEmpty(response))
                    return null;

                return Utilities.ParseKeyValueOption(response);
            }
            else if (_cachedRequestedValues.ContainsKey(option))
            {
                var cachedValue = _cachedRequestedValues[option];
                return cachedValue == null ? null : Utilities.ParseKeyValueOption(cachedValue);
            }

            return null;
        }

        /// <summary>
        /// Int version of GetStringValueOrDefault
        /// </summary>
        /// <param name="propertyValue"></param>
        /// <param name="option"></param>
        /// <param name="required"></param>
        /// <returns></returns>
        public int? GetIntValueOrDefault(int? propertyValue, CommandOption option, bool required)
        {
            if (propertyValue.HasValue)
            {
                return propertyValue.Value;
            }
            else if (DefaultConfig[option.Switch] is int)
            {
                var configDefault = (int)DefaultConfig[option.Switch];
                return configDefault;
            }
            else if (required && this.EnableInteractive)
            {
                var userValue = PromptForValue(option);
                if (string.IsNullOrWhiteSpace(userValue))
                    return null;

                int i;
                if (!int.TryParse(userValue, out i))
                {
                    throw new LambdaToolsException($"{userValue} cannot be parsed into an integer for {option.Name}", LambdaToolsException.ErrorCode.CommandLineParseError);
                }
                return i;
            }
            else if (_cachedRequestedValues.ContainsKey(option))
            {
                var cachedValue = _cachedRequestedValues[option];
                int i;
                if(int.TryParse(cachedValue, out i))
                {
                    return i;
                }
            }

            return null;
        }

        /// <summary>
        /// bool version of GetStringValueOrDefault
        /// </summary>
        /// <param name="propertyValue"></param>
        /// <param name="option"></param>
        /// <param name="required"></param>
        /// <returns></returns>
        public bool? GetBoolValueOrDefault(bool? propertyValue, CommandOption option, bool required)
        {
            if (propertyValue.HasValue)
            {
                return propertyValue.Value;
            }
            else if (DefaultConfig[option.Switch] is bool)
            {
                var configDefault = (bool)DefaultConfig[option.Switch];
                return configDefault;
            }
            else if (required && this.EnableInteractive)
            {
                var userValue = PromptForValue(option);
                if (string.IsNullOrWhiteSpace(userValue))
                    return null;

                bool i;
                if (bool.TryParse(userValue, out i))
                {
                    throw new LambdaToolsException($"{userValue} cannot be parsed into a boolean for {option.Name}", LambdaToolsException.ErrorCode.CommandLineParseError);
                }
                return i;
            }
            else if (_cachedRequestedValues.ContainsKey(option))
            {
                var cachedValue = _cachedRequestedValues[option];
                bool i;
                if (bool.TryParse(cachedValue, out i))
                {
                    return i;
                }
            }

            return null;
        }

        // Cache all prompted values so the user is never prompted for the same CommandOption later.
        Dictionary<CommandOption, string> _cachedRequestedValues = new Dictionary<CommandOption, string>();
        protected string PromptForValue(CommandOption option)
        {
            if (_cachedRequestedValues.ContainsKey(option))
            {
                var cachedValue = _cachedRequestedValues[option];
                return cachedValue;
            }

            string input = null;

            // If the value is missing for the role then assist the user in selecting an existing role or creating a new one.
            if (option == DefinedCommandOptions.ARGUMENT_FUNCTION_ROLE)
            {
                input = new RoleHelper(this.IAMClient).PromptForRole();
                if (string.IsNullOrWhiteSpace(input))
                    return null;
            }
            else
            {
                Console.Out.WriteLine($"Enter {option.Name}: ({option.Description})");
                Console.Out.Flush();
                input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    return null;
                input = input.Trim();
            }

            _cachedRequestedValues[option] = input;
            return input;
        }        
    }
}
