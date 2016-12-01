using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Tools.Options
{

    /// <summary>
    /// Container for all the command options
    /// </summary>
    public class CommandOptions
    {
        Dictionary<CommandOption, CommandOptionValue> _values = new Dictionary<CommandOption, CommandOptionValue>();

        public int Count
        {
            get { return this._values.Count; }
        }

        /// <summary>
        /// Gets the list of command line arguments that are not associated with a command option. Currently 
        /// the only valid value for this is function name.
        /// </summary>
        public IList<string> Arguments { get; } = new List<string>();

        /// <summary>
        /// Adds a CommandOption along with its value
        /// </summary>
        /// <param name="option"></param>
        /// <param name="value"></param>
        public void AddOption(CommandOption option, CommandOptionValue value)
        {
            _values[option] = value;
        }

        /// <summary>
        /// Gets the command option along with its value. The argument is searched for using both the short switch and the full switch.
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        public Tuple<CommandOption, CommandOptionValue> FindCommandOption(string argument)
        {
            var option = _values.Keys.FirstOrDefault(x =>
            {
                if (string.Equals(argument, x.ShortSwitch, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, x.Switch, StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            });

            if (option == null)
                return null;

            return new Tuple<CommandOption, CommandOptionValue>(option, _values[option]);
        }
    }
}
