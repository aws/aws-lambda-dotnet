using System;

namespace Amazon.Lambda.TestTool
{
    public class CommandLineOptions
    {
        public static LambdaTestToolOptions Parse(string[] args)
        {
            var options = new LambdaTestToolOptions();

            for (int i = 0; i < args.Length; i++)
            {
                bool skipAhead;
                switch (args[i])
                {
                    case "--help":
                        options.ShowHelp = GetNextBoolValue(i, out skipAhead);
                        if (skipAhead)
                        {
                            i++;
                        }
                        break;
                    case "--host":
                        options.Host = GetNextStringValue(i);
                        i++;
                        break;
                    case "--port":
                        options.Port = GetNextIntValue(i);
                        i++;
                        break;
                    case "--no-launch-window":
                        options.NoLaunchWindow = GetNextBoolValue(i, out skipAhead);
                        if (skipAhead)
                        {
                            i++;
                        }
                        break;
                    case "--pause-exit":
                        options.PauseExit = GetNextBoolValue(i, out skipAhead);
                        if (skipAhead)
                        {
                            i++;
                        }
                        break;
                    case "--disable-logs":
                        options.DisableLogs = GetNextBoolValue(i, out skipAhead);
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
                if (valueIndex == args.Length)
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
            Console.WriteLine("TODO: Add console help");
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
