using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Amazon;

using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ServerlessTemplateExample
{
    public class Functions
    {

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Functions()
        {
        }

        public void HelloWorld(ILambdaContext context)
        {
            context.Logger.LogLine("Hello World Test");
        }

        public string ToUpper(string input, ILambdaContext context)
        {
            context.Logger.LogLine("Hello World Test");
            return input?.ToUpper();
        }
    }
}
