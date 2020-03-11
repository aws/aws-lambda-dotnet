using System;
using System.IO;

using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.WebTester;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Amazon.Lambda.TestTool.WebTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", "AWS_DOTNET_LAMDBA_TEST_TOOL_2_1_" + Utils.DetermineToolVersion());
            TestToolStartup.Startup(Constants.PRODUCT_NAME, (options, showUI) => Startup.LaunchWebTester(options, showUI), args);
        }
    }
}