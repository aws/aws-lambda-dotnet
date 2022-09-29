namespace Amazon.Lambda.TestTool.Tests.NET6
{
    public class SourceGeneratorTests
    {
        [Fact]
        public void DirectFunctionCallFromConfig()
        {
            var runConfiguration = new TestToolStartup.RunConfiguration
            {
                Mode = TestToolStartup.RunConfiguration.RunMode.Test,
                OutputWriter = new StringWriter()
            };
            var buildPath = Path.GetFullPath($"../../../../LambdaFunctions/net6/SourceGeneratorExample/bin/debug/net6.0");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "{\"Name\" : \"FooBar\"}" }, runConfiguration);
            Assert.Contains("Response = FooBar", runConfiguration.OutputWriter.ToString());
        }
    }
}