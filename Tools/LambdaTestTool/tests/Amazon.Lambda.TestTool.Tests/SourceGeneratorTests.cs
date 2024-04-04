namespace Amazon.Lambda.TestTool.Tests
{
    public class SourceGeneratorTests
    {
        [Fact]
        public void SourceGeneratorInputAndOutput()
        {
            var runConfiguration = new TestToolStartup.RunConfiguration
            {
                Mode = TestToolStartup.RunConfiguration.RunMode.Test,
                OutputWriter = new StringWriter()
            };
            var buildPath = Path.GetFullPath($"../../../../LambdaFunctions/SourceGeneratorExample/bin/Debug/{TestUtils.GetTargetFramework()}");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "{\"Name\" : \"FooBar\"}", "--config-file", "SourceGeneratorInputAndOutput.json" }, runConfiguration);
            Assert.Contains("Response = FooBar", runConfiguration.OutputWriter.ToString());
        }

        [Fact]
        public void SourceGeneratorAsyncInputOnly()
        {
            var runConfiguration = new TestToolStartup.RunConfiguration
            {
                Mode = TestToolStartup.RunConfiguration.RunMode.Test,
                OutputWriter = new StringWriter()
            };
            var buildPath = Path.GetFullPath($"../../../../LambdaFunctions/SourceGeneratorExample/bin/Debug/{TestUtils.GetTargetFramework()}");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "{\"Name\" : \"FooBar\"}", "--config-file", "SourceGeneratorAsyncInputOnly.json" }, runConfiguration);
            Assert.Contains("Calling function with:", runConfiguration.OutputWriter.ToString());
            Assert.DoesNotContain("Error:", runConfiguration.OutputWriter.ToString());
        }
    }
}