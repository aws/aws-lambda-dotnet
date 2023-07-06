namespace Amazon.Lambda.TestTool.Tests.NET6
{
    public class LambdaAssemblyPathTests
    {

        [Fact]
        public void SingleDepsJsonFile()
        {
            var lambdaAssemblyPath =  Utils.FindLambdaAssemblyPath(Path.Combine("TestFiles", "SingleDepsJsonFile"));
            Assert.Equal(Path.Combine("TestFiles", "SingleDepsJsonFile", "LambdaDemo.dll"), lambdaAssemblyPath);
        }

        [Fact]
        public void MultipleDepsJsonFile()
        {
            var lambdaAssemblyPath = Utils.FindLambdaAssemblyPath(Path.Combine("TestFiles", "MultipleDepsJsonFile"));
            Assert.Equal(Path.Combine("TestFiles", "MultipleDepsJsonFile", "LambdaDemo.dll"), lambdaAssemblyPath);
        }
    }
}
