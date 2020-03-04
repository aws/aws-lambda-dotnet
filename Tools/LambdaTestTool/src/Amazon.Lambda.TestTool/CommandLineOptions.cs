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

        public static CommandLineOptions Parse(string[] args)
        {
            var options = new CommandLineOptions();

            for (int i = 0; i < args.Length; i++)
            {
                bool skipAhead;
                switch (args[i])
                {
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
                        if(File.Exists(options.Payload))
                        {
                            options.Payload = File.ReadAllText(options.Payload);
                        }
                        i++;
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
                if (currentIndex + 1 < args.Length && !args[currentIndex + 1].StartsWith("--") && bool.TryParse(GetNextStringValue(currentIndex + 1), out var value))
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
            Console.WriteLine("");
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