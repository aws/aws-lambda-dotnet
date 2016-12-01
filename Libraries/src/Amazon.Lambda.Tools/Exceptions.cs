using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Tools
{
    /// <summary>
    /// The deploy tool exception. This is used to throw back an error to the user but is considerd a known error
    /// so the stack trace will not be displayed.
    /// </summary>
    public class LambdaToolsException : Exception
    {
        public LambdaToolsException(string message) : base(message) { }
    }

    public class ValidateHandlerException : LambdaToolsException
    {
        public string ProjectLocation { get; }
        public string Handler { get; }
        public ValidateHandlerException(string projectLocation, string handler, string message) : base(message)
        {
            this.ProjectLocation = projectLocation;
            this.Handler = handler;
        }
    }
}
