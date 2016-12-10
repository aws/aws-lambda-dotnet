using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Tools.Options
{
    public static class CommandLineParser
    {

        /// <summary>
        /// Parse all the command line arguments.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static CommandOptions ParseArguments(
            IList<CommandOption> options, string[] arguments)
        {
            CommandOptions values = new CommandOptions();

            for (int i = 0; i < arguments.Length; i++)
            {
                // Collect arguments that are not attached to a switch. This is currently always the function name.
                if (!arguments[i].StartsWith("-"))
                {
                    values.Arguments.Add(arguments[i]);
                }
                else
                {
                    var option = FindCommandOption(options, arguments[i]);
                    if (option != null)
                    {
                        var value = new CommandOptionValue();

                        if (option.ValueType != CommandOption.CommandOptionValueType.NoValue)
                        {
                            if (i + 1 >= arguments.Length)
                            {
                                throw new LambdaToolsException($"Argument {arguments[i]} must be followed by a value", LambdaToolsException.ErrorCode.CommandLineParseError);
                            }

                            switch (option.ValueType)
                            {
                                case CommandOption.CommandOptionValueType.StringValue:
                                    value.StringValue = arguments?[i + 1];
                                    break;
                                case CommandOption.CommandOptionValueType.CommaDelimitedList:
                                    value.StringValues = arguments?[i + 1].SplitByComma();
                                    break;
                                case CommandOption.CommandOptionValueType.KeyValuePairs:
                                    value.KeyValuePairs = Utilities.ParseKeyValueOption(arguments[i + 1]);
                                    break;
                                case CommandOption.CommandOptionValueType.IntValue:
                                    int iv;
                                    if (!int.TryParse(arguments?[i + 1], out iv))
                                        throw new Exception($"Argument {arguments?[i]} expects an integer value but received an {arguments?[i + 1]}");
                                    value.IntValue = iv;
                                    break;
                                case CommandOption.CommandOptionValueType.BoolValue:
                                    bool bv;
                                    if (!bool.TryParse(arguments?[i + 1], out bv))
                                        throw new Exception($"Argument {arguments?[i]} expects either {bool.TrueString} or {bool.FalseString} value but received an {arguments?[i + 1]}");
                                    value.BoolValue = bv;
                                    break;
                            }
                            i++;
                        }

                        values.AddOption(option, value);
                    }
                }
            }

            return values;
        }



        private static CommandOption FindCommandOption(IEnumerable<CommandOption> options, string argument)
        {
            var option = options.FirstOrDefault(x => 
            {
                if (string.Equals(argument, x.ShortSwitch, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(argument, x.Switch, StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            });

            return option;
        }
    }
}
