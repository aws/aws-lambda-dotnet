using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.TestTool.BlazorTester
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var version = Utils.DetermineToolVersion();
            // The leading and trailing whitespaces are intentional
            var userAgent = $" lib/AWS_DOTNET_LAMDBA_TEST_TOOL_BLAZOR#{version} ";

            Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", userAgent);
            TestToolStartup.Startup(Constants.PRODUCT_NAME, (options, showUI) => Startup.LaunchWebTester(options, showUI), args);
        }
    }
}
