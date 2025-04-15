// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions;

public static class ApiGatewayResponseTestCases
{
    public static IEnumerable<object[]> V1TestCases()
    {
        // V1 (APIGatewayProxyResponse) test cases
        yield return new object[]
        {
            "V1_SimpleJsonResponse",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(new { message = "Hello, World!" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                },
            }
        };

        yield return new object[]
        {
            "V1_SetsCorrectStatusCode",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 201,
                    Body = "{\"message\":\"Created\"}"
                },
            }
        };

        yield return new object[]
        {
            "V1_SetsHeaders",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "X-Custom-Header", "CustomValue" }
                    },
                    Body = "{\"message\":\"With Headers\"}"
                },
            }
        };

        yield return new object[]
        {
            "V1_SetsMultiValueHeaders",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    MultiValueHeaders = new Dictionary<string, IList<string>>
                    {
                        { "X-Multi-Header", new List<string> { "Value1", "Value2" } }
                    },
                    Body = "{\"message\":\"With MultiValueHeaders\"}"
                },
            }
        };

        yield return new object[]
        {
            "V1_SetsBodyNonBase64",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "{\"message\":\"Hello, World!\"}",
                    IsBase64Encoded = false
                },
            }
        };

        yield return new object[]
        {
            "V1_DefaultsToCorrectContentTYpe",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "Hello, World!"
                },
            }
        };

        yield return new object[]
        {
            "V1_HandlesHeadersCorrectly",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "myheader", "test,other" },
                        { "anotherheader", "secondvalue" }
                    },
                    MultiValueHeaders = new Dictionary<string, IList<string>>
                    {
                        { "headername", new List<string> { "headervalue", "headervalue2" } }
                    },
                    Body = "{\"message\":\"With Multiple Headers\"}",
                    StatusCode = 200

                },
            }
        };

        yield return new object[]
        {
            "V1_CombinesSingleAndMultiValueHeaders",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "X-Custom-Header", "single-value" },
                        { "Combined-Header", "single-value" }
                    },
                    MultiValueHeaders = new Dictionary<string, IList<string>>
                    {
                        { "X-Multi-Header", new List<string> { "multi-value1", "multi-value2" } },
                        { "Combined-Header", new List<string> { "multi-value1", "multi-value2" } }
                    },
                    Body = "{\"message\":\"With Combined Headers\"}",
                    StatusCode = 200
                },
            }
        };

        yield return new object[]
        {
            "V1_SetsContentLength",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    Body = "{\"message\":\"Hello, World!\"}",
                    IsBase64Encoded = false,
                    StatusCode = 200
                },
            }
        };

        yield return new object[]
        {
            "V1_HandlesZeroStatusCode",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 0,
                    Body = "{\"key\":\"This body should be replaced\"}"
                },
            }
        };

        yield return new object[]
        {
            "V1_UsesProvidedContentType",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "Hello, World!",
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" }
                    }
                },
            }
        };
        yield return new object[]
        {
            "V1_APIHeaders",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "Test body"
                },
            }
        };

    }

    public static IEnumerable<object[]> V2TestCases()
    {
        // V2 (APIGatewayHttpApiV2ProxyResponse) test cases
        yield return new object[]
        {
            "V2_SimpleJsonResponse",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(new { message = "Hello, World!" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                },
            }
        };

        yield return new object[]
        {
            "V2_SetsCorrectStatusCode",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 201,
                    Body = "{\"message\":\"Created\"}"
                },
            }
        };

        yield return new object[]
        {
            "V2_HandlesZeroStatusCode",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 0,
                    Body = "{\"key\":\"This body should be replaced\"}"
                },
            }
        };

        yield return new object[]
        {
            "V2_SetsHeaders",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "X-Custom-Header", "CustomValue" }
                    },
                    Body = "{\"message\":\"With Headers\"}"
                },
            }
        };

        yield return new object[]
        {
            "V2_SetsBodyNonBase64",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = "{\"message\":\"Hello, API Gateway v2!\"}",
                    IsBase64Encoded = false
                },
            }
        };

        yield return new object[]
        {
            "V2_SetsBodyBase64",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"message\":\"Hello, API Gateway v2!\"}")),
                    IsBase64Encoded = true
                },
            }
        };

        yield return new object[]
        {
            "V2_DefaultsToTextPlainContentType",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = "Hello, World!"
                },
            }
        };

        yield return new object[]
        {
            "V2_HandlesHeadersCorrectly",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "myheader", "test,shouldhavesecondvalue" },
                        { "anotherheader", "secondvalue" }
                    },
                    Body = "{\"message\":\"With Headers\"}"
                },
            }
        };

        yield return new object[]
        {
            "V2_DoesNotOverrideExplicitValues",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 201,
                    Body = "{\"key\":\"value\"}",
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/xml" }
                    }
                },
            }
        };

        yield return new object[]
        {
            "V2_HttpAPIHeaders",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = "Test body"
                },
            }
        };

    }

    public class ApiGatewayResponseTestCase
    {
        public required object Response { get; set; }
    }
}
