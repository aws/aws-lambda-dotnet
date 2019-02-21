using System;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.IdentityModel.Tokens;

namespace Amazon.Lambda.TestTool
{
    public class CommandLineOptions
    {
        public int? Port { get; set; }
        
        public bool NoLaunchWindow { get; set; }

        public string Path { get; set; }

        public static CommandLineOptions Parse(string[] args)
        {
            var options = new CommandLineOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--port":
                        options.Port = GetNextIntValue(i);
                        i++;
                        break;
                    case "--no-launch-window":
                        options.NoLaunchWindow = GetNextBoolValue(i);
                        i++;
                        break;
                    case "--path":
                      options.Path = GetNextValue(i);
                      i++;
                      break;
                }
            }
            
            return options;

            string GetNextValue(int currentIndex)
            {
                var valueIndex = currentIndex + 1;
                if(valueIndex == args.Length)
                    throw new CommandLineParseException($"Missing value for {args[currentIndex]}");
                
                return args[valueIndex];
            }

            int GetNextIntValue(int currentIndex)
            {
                if (int.TryParse(GetNextValue(currentIndex), out var value))
                {
                    return value;
                }
                throw new CommandLineParseException($"Value for {args[currentIndex]} is not a valid integer");
            }
            
            bool GetNextBoolValue(int currentIndex)
            {
                if (currentIndex + 1 < args.Length && bool.TryParse(GetNextValue(currentIndex), out var value))
                {
                    return value;
                }

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