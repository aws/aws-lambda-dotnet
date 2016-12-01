using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Tools.Options
{
    /// <summary>
    /// Container class for the actual value of the CommandOption
    /// </summary>
    public class CommandOptionValue
    {
        /// <summary>
        /// This will be set if the CommandOption is of type string
        /// </summary>
        public string StringValue { get; set; }

        /// <summary>
        /// This will be set if the CommandOption is of type CommaDelimitedList
        /// </summary>
        public string[] StringValues { get; set; }

        /// <summary>
        /// This will be set if the CommandOption is of type KeyValuePairs
        /// </summary>
        public Dictionary<string, string> KeyValuePairs { get;set; }

        /// <summary>
        /// This will be set if the CommandOption is of type bool
        /// </summary>
        public bool BoolValue { get; set; }
        
        /// <summary>
        /// This will be set if the CommandOption is of type int
        /// </summary>
        public int IntValue { get; set; }
    }
}