using System;
using System.Collections.Generic;
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
    }
}
