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
    public class ParameterlessMethods
    {
        [LambdaFunction]
        public void NoParameter()
        {
            return;
        }
    }
}