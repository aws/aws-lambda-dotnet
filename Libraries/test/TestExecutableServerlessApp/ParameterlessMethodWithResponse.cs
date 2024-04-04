using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class ParameterlessMethodWithResponse
    {
        [LambdaFunction]
        public string NoParameterWithResponse()
        {
            return "OK";
        }
    }
}