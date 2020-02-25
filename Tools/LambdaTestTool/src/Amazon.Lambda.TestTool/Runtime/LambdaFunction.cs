using System;
using System.Reflection;
using Amazon.Lambda.TestTool.Runtime;

using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestTool.Runtime
{
    /// <summary>
    /// The abstraction above the code that will be called when during a Lambda invocation.
    /// </summary>
    public class LambdaFunction
    {
        public LambdaFunctionInfo FunctionInfo { get; private set; }

        /// <summary>
        /// False if the test tool was unable to find the reflection objects for the function handler.
        /// </summary>
        public bool IsSuccess  => string.IsNullOrEmpty(this.ErrorMessage);
        public string ErrorMessage { get; set; }
        
        public Assembly LambdaAssembly { get; set; }
        public Type LambdaType { get; set; }
        public MethodInfo LambdaMethod { get; set; }
        
        public ILambdaSerializer Serializer { get; set; } 

        public LambdaFunction(LambdaFunctionInfo functionInfo)
        {
            this.FunctionInfo = functionInfo;
        }
    }
}