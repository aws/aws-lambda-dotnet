using System;
using System.IO;

namespace Amazon.Lambda.TestTool
{
    public class CommandLineOptions
    {
        public int? Port { get; set; }
        
        public bool NoLaunchWindow { get; set; }

        public string Path { get; set; }

        public bool NoUI { get; set; }

        public string ConfigFile { get; set; }

        public string FunctionHandler { get; set; }

        public string Payload { get; set; }

        public string AWSProfile { get; set; }
        
        public string AWSRegion { get; set; }
        
        public bool ShowHelp { get; set; }

        public bool PauseExit { get; set; } = true;

        public static CommandLineOptions Parse(string[] args)
        {
            var options = new CommandLineOptions();

            for (int i = 0; i < args.Length; i++)
            {
                bool skipAhead;
                switch (args[i])
                {
                    case "--help":
                        options.ShowHelp = GetNextBoolValue(i, out skipAhead);
                        if(skipAhead)
                        {
                            i++;
                        }
                        break;
                    case "--port":
                        options.Port = GetNextIntValue(i);
                        i++;
                        break;
                    case "--no-launch-window":
                        options.NoLaunchWindow = GetNextBoolValue(i, out skipAhead);
                        if(skipAhead)
                        {
                            i++;
                        }
                        break;
                    case "--path":
                      options.Path = GetNextStringValue(i);
                      i++;
                      break;
                    case "--profile":
                        options.AWSProfile = GetNextStringValue(i);
                        i++;
                        break;
                    case "--region":
                        options.AWSRegion = GetNextStringValue(i);
                        i++;
                        break;
                    case "--no-ui":
                        options.NoUI = GetNextBoolValue(i, out skipAhead);
                        if (skipAhead)
                        {
                            i++;
                        }
                        break;
                    case "--config-file":
                        options.ConfigFile = GetNextStringValue(i);
                        i++;
                        break;
                    case "--function-handler":
                        options.FunctionHandler = GetNextStringValue(i);
                        i++;
                        break;
                    case "--payload":
                        options.Payload = GetNextStringValue(i);
                        i++;
                        break;
                    case "--pause-exit":
                        options.PauseExit = GetNextBoolValue(i, out skipAhead);
                        if (skipAhead)
                        {
                            i++;
                        }
                        break;
                }
            }
            
            return options;

            string GetNextStringValue(int currentIndex)
            {
                var valueIndex = currentIndex + 1;
                if(valueIndex == args.Length)
                    throw new CommandLineParseException($"Missing value for {args[currentIndex]}");
                
                return args[valueIndex];
            }

            int GetNextIntValue(int currentIndex)
            {
                if (int.TryParse(GetNextStringValue(currentIndex), out var value))
                {
                    return value;
                }
                throw new CommandLineParseException($"Value for {args[currentIndex]} is not a valid integer");
            }
            
            bool GetNextBoolValue(int currentIndex, out bool skipAhead)
            {
                if (currentIndex + 1 < args.Length && !args[currentIndex + 1].StartsWith("--") && bool.TryParse(GetNextStringValue(currentIndex), out var value))
                {
                    skipAhead = true;
                    return value;
                }

                skipAhead = false;
                return true;
            }

        }

        public static void PrintUsage()
        {
            Console.WriteLine();
            Console.WriteLine("The .NET Lambda Test Tool can be launched in 2 modes. The default mode is to launch a web interface to select the Lambda code");
            Console.WriteLine("to execute with in the Lambda test tool. The second mode skips using the web interface and the Lambda code is identified");
            Console.WriteLine("using the commandline switches as described below. To switch to the no web interface mode use the --no-ui command line switch.");
            Console.WriteLine();
            
            Console.WriteLine("These options are valid for either mode the Lambda test tool is running in.");
            Console.WriteLine();
            Console.WriteLine("\t--path <directory>                    The path to the lambda project to execute. If not set then the current directory will be used.");
            Console.WriteLine();

            Console.WriteLine("These options are valid when using the web interface to select and execute the Lambda code.");
            Console.WriteLine();
            Console.WriteLine("\t--port <port-number>                  The port number used for the test tool's web interface.");
            Console.WriteLine("\t--no-launch-window                    Disable auto launching the test tool's web interface in a browser.");
            Console.WriteLine();
            
            Console.WriteLine("These options are valid in the no web interface mode.");
            Console.WriteLine();
            Console.WriteLine("\t--no-ui                               Disable launching the web interface and immediately execute the Lambda code.");
            Console.WriteLine("\t--profile <profile-name>              Set the AWS credentials profile to provide credentials to the Lambda code.");
            Console.WriteLine("\t                                      If not set the profile from the config file will be used.");
            Console.WriteLine("\t--region <region-name>                Set the AWS region to as the default region for the Lambda code being executed.");
            Console.WriteLine("\t                                      If not set the region from the config file will be used.");
            Console.WriteLine("\t--config-file <file-name>             The config file to read for Lambda settings. If not set then aws-lambda-tools-defaults.json");
            Console.WriteLine("\t                                      will be used.");
            Console.WriteLine("\t--function-handler <handler-string>   The Lambda function handler to identify the code to run. If not set then the function handler");
            Console.WriteLine("\t                                      from the config file will be used. This is the format of <assembly::type-name::method-name>.");
            Console.WriteLine("\t--payload <file-name>                 The JSON payload to send to the Lambda function. This can be either an inline string or a");
            Console.WriteLine("\t                                      file path to a JSON file.");
            Console.WriteLine("\t--pause-exit <true or false>          If set to true the test tool will pause waiting for a key input before exiting. The is useful");
            Console.WriteLine("\t                                      when executing from an IDE so you can avoid having the output window immediately disappear after");
            Console.WriteLine("\t                                      executing the Lambda code. The default value is true.");
        }
    }


    public class CommandLineParseException : Exception
    {
        public CommandLineParseException(string message)
            : base(message)
        {
        }
    }
}