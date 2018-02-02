using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Amazon.Lambda.Tools.Commands;
using Amazon.Lambda.Tools.Options;

namespace Amazon.Lambda.Tools
{
    public class Program
    {
        private static string Version
        {
            get
            {
                AssemblyInformationalVersionAttribute attribute = null;
                try
                {
                    attribute = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                }
                catch (AmbiguousMatchException)
                {
                    // Catch exception and continue if multiple attributes are found.
                }
                return attribute?.InformationalVersion;
            }
        }

        public static void Main(string[] args)
        {
            try
            {
                PrintToolTitle();

                if (args.Length == 0)
                {
                    PrintUsage();
                    Environment.Exit(-1);
                }

                ICommand command = null;
                switch(args[0])
                {
                    case DeployFunctionCommand.COMMAND_DEPLOY_NAME:
                        command = new DeployFunctionCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case InvokeFunctionCommand.COMMAND_NAME:
                        command = new InvokeFunctionCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case ListFunctionCommand.COMMAND_NAME:
                        command = new ListFunctionCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case DeleteFunctionCommand.COMMAND_NAME:
                        command = new DeleteFunctionCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case GetFunctionConfigCommand.COMMAND_NAME:
                        command = new GetFunctionConfigCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case UpdateFunctionConfigCommand.COMMAND_NAME:
                        command = new UpdateFunctionConfigCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case DeployServerlessCommand.COMMAND_NAME:
                        command = new DeployServerlessCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case ListServerlessCommand.COMMAND_NAME:
                        command = new ListServerlessCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case DeleteServerlessCommand.COMMAND_NAME:
                        command = new DeleteServerlessCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case PackageCommand.COMMAND_NAME:
                        command = new PackageCommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case PackageCICommand.COMMAND_NAME:
                        command = new PackageCICommand(new ConsoleToolLogger(), Directory.GetCurrentDirectory(), args.Skip(1).ToArray());
                        break;
                    case "--help":
                    case "--h":
                    case "help":
                        if (args.Length > 1)
                            PrintUsage(args[1]);
                        else
                            PrintUsage();
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown command {args[0]}");
                        PrintUsage();
                        Environment.Exit(-1);
                        break;
                }

                if (command != null)
                {
                    var success = command.ExecuteAsync().Result;
                    if (!success)
                    {
                        Environment.Exit(-1);
                    }
                }
            }
            catch(LambdaToolsException e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(-1);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Unknown error: {e.Message}");
                Console.Error.WriteLine(e.StackTrace);

                Environment.Exit(-1);
            }
        }

        private static void PrintToolTitle()
        {
            var sb = new StringBuilder("AWS Lambda Tools for .NET Core functions");
            var version = Version;
            if (!string.IsNullOrEmpty(version))
            {
                sb.Append($" ({version})");
            }
            Console.WriteLine(sb.ToString());
            Console.WriteLine("Project Home: https://github.com/aws/aws-lambda-dotnet");
            Console.WriteLine("\t");
        }

        private static void PrintUsage()
        {
            const int NAME_WIDTH = 23;
            Console.WriteLine("\t");
            Console.WriteLine("Commands to deploy and manage AWS Lambda functions:");
            Console.WriteLine("\t");
            Console.WriteLine($"\t{DeployFunctionCommand.COMMAND_DEPLOY_NAME.PadRight(NAME_WIDTH)} {DeployFunctionCommand.COMMAND_DEPLOY_DESCRIPTION}");
            Console.WriteLine($"\t{InvokeFunctionCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {InvokeFunctionCommand.COMMAND_DESCRIPTION}");
            Console.WriteLine($"\t{ListFunctionCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {ListFunctionCommand.COMMAND_DESCRIPTION}");
            Console.WriteLine($"\t{DeleteFunctionCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {DeleteFunctionCommand.COMMAND_DESCRIPTION}");
            Console.WriteLine($"\t{GetFunctionConfigCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {GetFunctionConfigCommand.COMMAND_DESCRIPTION}");
            Console.WriteLine($"\t{UpdateFunctionConfigCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {UpdateFunctionConfigCommand.COMMAND_DESCRIPTION}");

            Console.WriteLine("\t");
            Console.WriteLine("\t");
            Console.WriteLine("Commands to deploy and manage AWS Serverless applications using AWS CloudFormation:");
            Console.WriteLine("\t");
            Console.WriteLine($"\t{DeployServerlessCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {DeployServerlessCommand.COMMAND_DESCRIPTION}");
            Console.WriteLine($"\t{ListServerlessCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {ListServerlessCommand.COMMAND_DESCRIPTION}");
            Console.WriteLine($"\t{DeleteServerlessCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {DeleteServerlessCommand.COMMAND_DESCRIPTION}");
            Console.WriteLine("\t");

            Console.WriteLine("\t");
            Console.WriteLine("Other Commands:");
            Console.WriteLine("\t");
            Console.WriteLine($"\t{PackageCommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {PackageCommand.COMMAND_DESCRIPTION}");
            Console.WriteLine($"\t{PackageCICommand.COMMAND_NAME.PadRight(NAME_WIDTH)} {PackageCICommand.COMMAND_SYNOPSIS}");
            Console.WriteLine("\t");
            Console.WriteLine("\t");

            Console.WriteLine("To get help on individual commands execute:");
            Console.WriteLine("\tdotnet lambda help <command>");
        }

        private static void PrintUsage(string command)
        {
            switch (command)
            {
                case DeployFunctionCommand.COMMAND_DEPLOY_NAME:
                    PrintUsage(DeployFunctionCommand.COMMAND_DEPLOY_NAME, DeployFunctionCommand.COMMAND_DEPLOY_DESCRIPTION, DeployFunctionCommand.DeployCommandOptions, DeployFunctionCommand.COMMAND_DEPLOY_ARGUMENTS);
                    break;
                case InvokeFunctionCommand.COMMAND_NAME:
                    PrintUsage(InvokeFunctionCommand.COMMAND_NAME, InvokeFunctionCommand.COMMAND_DESCRIPTION, InvokeFunctionCommand.InvokeCommandOptions, InvokeFunctionCommand.COMMAND_ARGUMENTS);
                    break;
                case ListFunctionCommand.COMMAND_NAME:
                    PrintUsage(ListFunctionCommand.COMMAND_NAME, ListFunctionCommand.COMMAND_DESCRIPTION, ListFunctionCommand.ListCommandOptions, null);
                    break;
                case DeleteFunctionCommand.COMMAND_NAME:
                    PrintUsage(DeleteFunctionCommand.COMMAND_NAME, DeleteFunctionCommand.COMMAND_DESCRIPTION, DeleteFunctionCommand.DeleteCommandOptions, DeleteFunctionCommand.COMMAND_ARGUMENTS);
                    break;
                case GetFunctionConfigCommand.COMMAND_NAME:
                    PrintUsage(GetFunctionConfigCommand.COMMAND_NAME, GetFunctionConfigCommand.COMMAND_DESCRIPTION, GetFunctionConfigCommand.GetConfigCommandOptions, GetFunctionConfigCommand.COMMAND_ARGUMENTS);
                    break;
                case UpdateFunctionConfigCommand.COMMAND_NAME:
                    PrintUsage(UpdateFunctionConfigCommand.COMMAND_NAME, UpdateFunctionConfigCommand.COMMAND_DESCRIPTION, UpdateFunctionConfigCommand.UpdateCommandOptions, UpdateFunctionConfigCommand.COMMAND_ARGUMENTS);
                    break;
                case DeployServerlessCommand.COMMAND_NAME:
                    PrintUsage(DeployServerlessCommand.COMMAND_NAME, DeployServerlessCommand.COMMAND_DESCRIPTION, DeployServerlessCommand.DeployServerlessCommandOptions, DeployServerlessCommand.COMMAND_ARGUMENTS);
                    break;
                case ListServerlessCommand.COMMAND_NAME:
                    PrintUsage(ListServerlessCommand.COMMAND_NAME, ListServerlessCommand.COMMAND_DESCRIPTION, ListServerlessCommand.ListCommandOptions, null);
                    break;
                case DeleteServerlessCommand.COMMAND_NAME:
                    PrintUsage(DeleteServerlessCommand.COMMAND_NAME, DeleteServerlessCommand.COMMAND_DESCRIPTION, DeleteServerlessCommand.DeleteCommandOptions, DeleteServerlessCommand.COMMAND_ARGUMENTS);
                    break;
                case PackageCommand.COMMAND_NAME:
                    PrintUsage(PackageCommand.COMMAND_NAME, PackageCommand.COMMAND_DESCRIPTION, PackageCommand.PackageCommandOptions, PackageCommand.COMMAND_ARGUMENTS);
                    break;
                case PackageCICommand.COMMAND_NAME:
                    PrintUsage(PackageCICommand.COMMAND_NAME, PackageCICommand.COMMAND_DESCRIPTION, PackageCICommand.PackageCICommandOptions, null);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown command {command}");
                    PrintUsage();
                    break;
            }

        }

        private static void PrintUsage(string command, string description, IList<CommandOption> options, string arguments)
        {
            const int INDENT = 3;

            Console.WriteLine($"{command}: ");
            Console.WriteLine($"{new string(' ', INDENT)}{description}");
            Console.WriteLine("\t");


            if (!string.IsNullOrEmpty(arguments))
            {
                Console.WriteLine($"{new string(' ', INDENT)}dotnet lambda {command} [arguments] [options]");
                Console.WriteLine($"{new string(' ', INDENT)}Arguments:");
                Console.WriteLine($"{new string(' ', INDENT * 2)}{arguments}");
            }
            else
            {
                Console.WriteLine($"{new string(' ', INDENT)}dotnet lambda {command} [options]");
            }

            var defaults = LambdaToolsDefaultsReader.LoadDefaults(Directory.GetCurrentDirectory(), LambdaToolsDefaultsReader.DEFAULT_FILE_NAME);

            const int SWITCH_COLUMN_WIDTH = 40;

            Console.WriteLine($"{new string(' ', INDENT)}Options:");
            foreach (var option in options)
            {
                StringBuilder sb = new StringBuilder();
                if (option.ShortSwitch != null)
                    sb.Append($"{option.ShortSwitch.PadRight(6)} | ");

                sb.Append($"{option.Switch}");
                if (sb.Length < SWITCH_COLUMN_WIDTH)
                    sb.Append(new string(' ', SWITCH_COLUMN_WIDTH - sb.Length));

                sb.Append(option.Description);
                var optionDefault = defaults.GetValueAsString(option);
                if (optionDefault != null)
                {
                    sb.Append($" (Default Value: {optionDefault})");
                }

                Console.WriteLine($"{new string(' ', INDENT * 2)}{sb.ToString()}");
            }

        }
    }
}
