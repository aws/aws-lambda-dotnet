using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.ApplicationLoadBalancerEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1
{
    public class Function
    {
        
        /// <summary>
        /// Lambda function handler to respond to events coming from an Application Load Balancer.
        /// 
        /// Note: If "Multi value headers" is disabled on the ELB Target Group then use the Headers and QueryStringParameters properties 
        /// on the ApplicationLoadBalancerRequest and ApplicationLoadBalancerResponse objects. If "Multi value headers" is enabled then
        /// use MultiValueHeaders and MultiValueQueryStringParameters properties.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public ApplicationLoadBalancerResponse FunctionHandler(ApplicationLoadBalancerRequest request, ILambdaContext context)
        {
            var response = new ApplicationLoadBalancerResponse
            {
                StatusCode = 200,
                StatusDescription = "200 OK",
                IsBase64Encoded = false
            };

            // If "Multi value headers" is enabled for the ELB Target Group then use the "response.MultiValueHeaders" property instead of "response.Headers".
            response.Headers = new Dictionary<string, string>
            {
                {"Content-Type", "text/html; charset=utf-8" }
            };

            response.Body =
@"
<html>
    <head>
        <title>Hello World!</title>
        <style>
            html, body {
                margin: 0; padding: 0;
                font-family: arial; font-weight: 700; font-size: 3em;
                text-align: center;
            }
        </style>
    </head>
    <body>
        <p>Hello World from Lambda</p>
    </body>
</html>
";

            return response;
        }
    }
}
