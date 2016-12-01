using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Amazon.CloudFormation.Model;

namespace Amazon.Lambda.Tools.Test
{
    public class OptionParseTests
    {
        [Fact]
        public void ParseNullCloudFormationParameter()
        {
            var parameters = Utilities.ParseKeyValueOption(null);
            Assert.Equal(0, parameters.Count);

            parameters = Utilities.ParseKeyValueOption(string.Empty);
            Assert.Equal(0, parameters.Count);
        }

        [Fact]
        public void ParseSingleCloudFormationParameter()
        {
            var parameters = Utilities.ParseKeyValueOption("Table=Blog");
            Assert.Equal(1, parameters.Count);
            Assert.Equal("Blog", parameters["Table"]);

            parameters = Utilities.ParseKeyValueOption("Table=Blog;");
            Assert.Equal(1, parameters.Count);
            Assert.Equal("Blog", parameters["Table"]);

            parameters = Utilities.ParseKeyValueOption("\"ConnectionString\"=\"User=foo;Password=test\"");
            Assert.Equal(1, parameters.Count);
            Assert.Equal("User=foo;Password=test", parameters["ConnectionString"]);
        }

        [Fact]
        public void ParseTwoCloudFormationParameter()
        {
            var parameters = Utilities.ParseKeyValueOption("Table=Blog;Bucket=MyBucket");
            Assert.Equal(2, parameters.Count);

            Assert.Equal("Blog", parameters["Table"]);
            Assert.Equal("MyBucket", parameters["Bucket"]);

            parameters = Utilities.ParseKeyValueOption("\"ConnectionString1\"=\"User=foo;Password=test\";\"ConnectionString2\"=\"Password=test;User=foo\"");
            Assert.Equal(2, parameters.Count);
            Assert.Equal("User=foo;Password=test", parameters["ConnectionString1"]);
            Assert.Equal("Password=test;User=foo", parameters["ConnectionString2"]);
        }

        [Fact]
        public void ParseEmptyValue()
        {
            var parameters = Utilities.ParseKeyValueOption("ShouldCreateTable=true;BlogTableName=");
            Assert.Equal(2, parameters.Count);
            Assert.Equal("true", parameters["ShouldCreateTable"]);
            Assert.Equal("", parameters["BlogTableName"]);

            parameters = Utilities.ParseKeyValueOption("BlogTableName=;ShouldCreateTable=true");
            Assert.Equal(2, parameters.Count);
            Assert.Equal("true", parameters["ShouldCreateTable"]);
            Assert.Equal("", parameters["BlogTableName"]);
        }

        [Fact]
        public void ParseErrors()
        {
            Assert.Throws(typeof(LambdaToolsException), () => Utilities.ParseKeyValueOption("=aaa"));
        }
    }
}
