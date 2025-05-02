// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using System.Linq;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.Tests
{
    public class TestUtilitiesLoggingTest
    {
        [Fact]
        public void LogWithLevelAndMessage()
        {
            ILambdaLogger logger = new TestLambdaLogger();
            logger.Log(LogLevel.Warning, "This is a warning");

            Assert.Contains("Warning: This is a warning", ((TestLambdaLogger)logger).Buffer.ToString());
        }

        [Fact]
        public void LogWithLevelAndMessageAndParameters()
        {
            ILambdaLogger logger = new TestLambdaLogger();
            logger.Log(LogLevel.Warning, "This is {name}", "garp");

            Assert.Contains("Warning: This is {name}", ((TestLambdaLogger)logger).Buffer.ToString());
            Assert.Contains("\tgarp", ((TestLambdaLogger)logger).Buffer.ToString());
        }

        [Fact]
        public void LogWithLevelAndMessageAndParametersAndExecption()
        {
            ILambdaLogger logger = new TestLambdaLogger();

            var exception = new ArgumentException("Bad Name");
            logger.Log(LogLevel.Warning, exception, "This is {name}", "garp");


            Assert.Contains("Warning: This is {name}", ((TestLambdaLogger)logger).Buffer.ToString());
            Assert.Contains("\tgarp", ((TestLambdaLogger)logger).Buffer.ToString());
            Assert.Contains("ArgumentException", ((TestLambdaLogger)logger).Buffer.ToString());
        }
    }
}
