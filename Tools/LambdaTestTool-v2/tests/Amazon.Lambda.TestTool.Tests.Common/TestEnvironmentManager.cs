using Amazon.Lambda.TestTool.Services.IO;

namespace Amazon.Lambda.TestTool.Tests.Common;

public class TestEnvironmentManager(System.Collections.IDictionary dictionary) : IEnvironmentManager
{
    public System.Collections.IDictionary GetEnvironmentVariables() => dictionary;
}
