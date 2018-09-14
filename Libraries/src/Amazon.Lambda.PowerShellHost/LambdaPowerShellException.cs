using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.PowerShellHost
{
    /// <summary>
    /// Exceptions thrown from errors running the PowerShell script
    /// </summary>
    public class LambdaPowerShellException : Exception
    {
        /// <summary>
        /// Exceptions thrown from errors running the PowerShell script
        /// </summary>
        /// <param name="message"></param>
        public LambdaPowerShellException(string message)
            : base(message) { }

        /// <summary>
        /// Exceptions thrown from errors running the PowerShell script
        /// </summary>
        /// <param name="wrappedException"></param>
        public LambdaPowerShellException(Exception wrappedException)
            : base(wrappedException.Message, wrappedException) { }

        /// <summary>
        /// Exceptions thrown from errors running the PowerShell script
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public LambdaPowerShellException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
