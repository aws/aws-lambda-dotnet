using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

using Amazon.Lambda.TestTool.SampleRequests;


namespace Amazon.Lambda.TestTool.Tests
{
    public class SampleRequestTests
    {
        [Theory]
        [InlineData("SavedRequests@foo.json", "foo")]
        [InlineData("S3@foo.json", null)]
        public void DetermineSampleName(string testValue, string expected)
        {
            string determined;
            if(SampleRequestManager.TryDetermineSampleRequestName(testValue, out determined))
            {
                Assert.NotNull(expected);
                Assert.Equal(expected, determined);
            }
            else
            {
                Assert.Null(expected);
            }
        }

        [Fact]
        public void GetRequest_WithTraversalName_StaysWithinSavedRequestDirectory()
        {
            var preferenceDirectory = Path.Combine(Path.GetTempPath(), "LambdaTestToolTests", Guid.NewGuid().ToString());
            var savedRequestDirectory = Path.Combine(preferenceDirectory, SampleRequestManager.SAVED_REQUEST_DIRECTORY);
            Directory.CreateDirectory(savedRequestDirectory);
            try
            {
                File.WriteAllText(Path.Combine(savedRequestDirectory, "target.json"), "INSIDE");
                File.WriteAllText(Path.Combine(preferenceDirectory, "target.json"), "OUTSIDE");

                var manager = new SampleRequestManager(preferenceDirectory);

                var content = manager.GetRequest($"{SampleRequestManager.SAVED_REQUEST_DIRECTORY}@../target.json");

                Assert.Equal("INSIDE", content);
            }
            finally
            {
                Directory.Delete(preferenceDirectory, recursive: true);
            }
        }
    }
}
