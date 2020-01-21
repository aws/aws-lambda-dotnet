using System;
using System.Runtime.Serialization;

namespace Amazon.Lambda.TestTool.Runtime
{
    /// <summary>
    /// The class represents the output of an executed Lambda function.
    /// </summary>
    public class ExecutionResponse
    {
        /// <summary>
        /// The return data from a Lambda function.
        /// </summary>
        public string Response { get; set; }

        /// <summary>
        /// The logs captures from calls to ILambdaContext.Logger and Console.Write
        /// </summary>
        public string Logs { get; set; }

        /// <summary>
        /// If an unhandled exception occured in the Lambda function this will contain the error message and stack trace.
        /// </summary>
        public string Error { get; set; }        

        /// <summary>
        /// True if the Lambda function was executed without any unhandled exceptions.
        /// </summary>
        public bool IsSuccess  => this.Error == null;
        
    }
}