// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Models;
using Xunit;

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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(200, (int)response.StatusCode);
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":\"Hello, World!\"}", content);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(201, (int)response.StatusCode);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    Assert.True(response.Headers.Contains("X-Custom-Header"));
                    Assert.Equal("CustomValue", response.Headers.GetValues("X-Custom-Header").First());
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.True(response.Headers.Contains("X-Multi-Header"));
                    var multiHeaderValues = response.Headers.GetValues("X-Multi-Header").ToList();
                    Assert.Contains("Value1", multiHeaderValues);
                    Assert.Contains("Value2", multiHeaderValues);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":\"Hello, World!\"}", content);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    if (emulatorMode == ApiGatewayEmulatorMode.HttpV1)
                    {
                        Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());
                    }
                    else
                    {
                        Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    }
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    Assert.Equal("test,other", response.Headers.GetValues("myheader").First());
                    Assert.Equal("secondvalue", response.Headers.GetValues("anotherheader").First());
                    var headernameValues = response.Headers.GetValues("headername").ToList();
                    Assert.Contains("headervalue", headernameValues);
                    Assert.Contains("headervalue2", headernameValues);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    Assert.Equal("single-value", response.Headers.GetValues("X-Custom-Header").First());
                    var multiHeaderValues = response.Headers.GetValues("X-Multi-Header").ToList();
                    Assert.Contains("multi-value1", multiHeaderValues);
                    Assert.Contains("multi-value2", multiHeaderValues);
                    var combinedHeaderValues = response.Headers.GetValues("Combined-Header").ToList();
                    Assert.Contains("multi-value1", combinedHeaderValues);
                    Assert.Contains("multi-value2", combinedHeaderValues);
                    Assert.Contains("single-value", combinedHeaderValues);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("{\"message\":\"Hello, World!\"}".Length, response.Content.Headers.ContentLength);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    string error;
                    int contentLength;
                    int statusCode;

                    if (emulatorMode == ApiGatewayEmulatorMode.Rest)
                    {
                        error = " \"Internal server error\"}";
                        contentLength = 36;
                        statusCode = 502;
                    }
                    else
                    {
                        error = "\"Internal Server Error\"}";
                        contentLength = 35;
                        statusCode = 500;
                    }
                    Assert.Equal(statusCode, (int)response.StatusCode);
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":"+error, content);
                    Assert.Equal(contentLength, response.Content.Headers.ContentLength);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.True(response.Headers.Contains("Date"));

                    if (emulatorMode == ApiGatewayEmulatorMode.Rest)
                    {
                        Assert.True(response.Headers.Contains("x-amzn-RequestId"));
                        Assert.True(response.Headers.Contains("x-amz-apigw-id"));
                        Assert.True(response.Headers.Contains("X-Amzn-Trace-Id"));

                        Assert.Matches(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", response.Headers.GetValues("x-amzn-RequestId").First());
                        Assert.Matches(@"^[A-Za-z0-9_\-]{15}=$", response.Headers.GetValues("x-amz-apigw-id").First());
                        Assert.Matches(@"^Root=1-[0-9a-f]{8}-[0-9a-f]{24};Parent=[0-9a-f]{16};Sampled=0;Lineage=1:[0-9a-f]{8}:0$", response.Headers.GetValues("X-Amzn-Trace-Id").First());
                    }
                    else // HttpV1 or HttpV2
                    {
                        Assert.True(response.Headers.Contains("Apigw-Requestid"));
                        Assert.Matches(@"^[A-Za-z0-9_\-]{15}=$", response.Headers.GetValues("Apigw-Requestid").First());
                    }

                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(200, (int)response.StatusCode);
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":\"Hello, World!\"}", content);
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(201, (int)response.StatusCode);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    string error;
                    int contentLength;
                    int statusCode;

                    error = "\"Internal Server Error\"}";
                    contentLength = 35;
                    statusCode = 500;
                    Assert.Equal(statusCode, (int)response.StatusCode);
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":"+error, content);
                    Assert.Equal(contentLength, response.Content.Headers.ContentLength);
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    Assert.True(response.Headers.Contains("X-Custom-Header"));
                    Assert.Equal("CustomValue", response.Headers.GetValues("X-Custom-Header").First());
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":\"Hello, API Gateway v2!\"}", content);
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":\"Hello, API Gateway v2!\"}", content);
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
                    Assert.Equal("test,shouldhavesecondvalue", response.Headers.GetValues("myheader").First());
                    Assert.Equal("secondvalue", response.Headers.GetValues("anotherheader").First());
                    await Task.CompletedTask;
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(201, (int)response.StatusCode);
                    Assert.Equal("application/xml", response.Content.Headers.ContentType?.ToString());
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"key\":\"value\"}", content);
                }
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
                Assertions = async (response, emulatorMode) =>
                {
                    Assert.True(response.Headers.Contains("Date"));
                    Assert.True(response.Headers.Contains("Apigw-Requestid"));

                    Assert.Matches(@"^[A-Za-z0-9_\-]{15}=$", response.Headers.GetValues("Apigw-Requestid").First());
                    await Task.CompletedTask;
                }
            }
        };

    }

    public class ApiGatewayResponseTestCase
    {
        public required object Response { get; set; }
        public required Func<HttpResponseMessage, ApiGatewayEmulatorMode, Task> Assertions { get; set; }
    }

}
