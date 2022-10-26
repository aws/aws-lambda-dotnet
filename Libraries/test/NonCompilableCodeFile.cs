using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

// This file is used to test the Lambda Annotations source generator does not run when there is a compile error.

namespace TestServerlessApp
{
    public class NonCompilableCodeFile
    {
		[LambdaFunction]
        public void SyntaxErrorFunction()
        {
			{
        }
    }
}