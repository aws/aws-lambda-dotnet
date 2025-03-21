// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    public class TestRouteConfig
    {
        public required string Path { get; set; }
        public required string HttpMethod { get; set; }
        public required string LambdaFunctionArn { get; set; }
        public required string Description { get; set; }
    }

    public static class TestRoutes
    {
        public static class Ids
        {
            public const string ParseAndReturnBody = "ParseAndReturnBody";
            public const string ReturnRawBody = "ReturnRawBody";
            public const string ReturnFullEvent = "ReturnFullEvent";
            public const string DecodeParseBinary = "BinaryMediaType";
        }

        public static class Paths
        {
            public const string ParseAndReturnBody = "/parse-and-return";
            public const string ReturnRawBody = "/return-raw";
            public const string ReturnFullEvent = "/return-full";
            public const string BinaryMediaType = "/binary";
        }

        public static Dictionary<string, TestRouteConfig> GetDefaultRoutes(ApiGatewayIntegrationTestFixture fixture)
        {
            return new Dictionary<string, TestRouteConfig>
            {
                [Ids.ParseAndReturnBody] = new TestRouteConfig
                {
                    Path = Paths.ParseAndReturnBody,
                    HttpMethod = "POST",
                    LambdaFunctionArn = fixture.ParseAndReturnBodyLambdaFunctionArn,
                    Description = "Test parsing and returning body",
                },
                [Ids.ReturnRawBody] = new TestRouteConfig
                {
                    Path = Paths.ReturnRawBody,
                    HttpMethod = "POST",
                    LambdaFunctionArn = fixture.ReturnRawBodyLambdaFunctionArn,
                    Description = "Test returning raw body",
                },
                [Ids.ReturnFullEvent] = new TestRouteConfig
                {
                    Path = Paths.ReturnFullEvent,
                    HttpMethod = "POST",
                    LambdaFunctionArn = fixture.ReturnFullEventLambdaFunctionArn,
                    Description = "Test returning full event",
                },
                [Ids.DecodeParseBinary] = new TestRouteConfig
                {
                    Path = Paths.BinaryMediaType,
                    HttpMethod = "POST",
                    LambdaFunctionArn = fixture.ReturnDecodedParseBinLambdaFunctionArn,
                    Description = "Test binary media type handling",
                }
            };
        }
    }
} 
