using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;

namespace BLUEPRINT_BASE_NAME.Tests
{
    /// <summary>
    /// This class extends from APIGatewayProxyFunction which contains the method FunctionHandlerAsync which is the 
    /// actual Lambda function entry point. The Lambda handler field should be set to
    /// 
    /// BLUEPRINT_BASE_NAME::BLUEPRINT_BASE_NAME.LambdaEntryPoint::FunctionHandlerAsync
    /// </summary>
    public class TestLambdaEntryPoint : Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            var contentRoot = TestUtils.GetProjectPath(Path.Combine(string.Empty));

            builder
                .UseContentRoot(contentRoot)
                .UseStartup<Startup>()
                .UseApiGateway();
        }
    }
}
