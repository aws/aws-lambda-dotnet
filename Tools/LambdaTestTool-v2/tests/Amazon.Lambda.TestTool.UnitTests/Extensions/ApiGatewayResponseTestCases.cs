using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal(200, response.StatusCode);
                    Assert.Equal("application/json", response.ContentType);
                    Assert.Equal("{\"message\":\"Hello, World!\"}", ReadResponseBody(response));
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(200, (int)response.StatusCode);
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":\"Hello, World!\"}", content);
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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal(201, response.StatusCode);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal("application/json", response.Headers["Content-Type"]);
                    Assert.Equal("CustomValue", response.Headers["X-Custom-Header"]);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal(new[] { "Value1", "Value2" }, response.Headers["X-Multi-Header"]);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal("{\"message\":\"Hello, World!\"}", ReadResponseBody(response));
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":\"Hello, World!\"}", content);
                }
            }
        };

        yield return new object[]
        {
            "V1_SetsBodyBase64",
            new ApiGatewayResponseTestCase
            {
                Response = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"message\":\"Hello, World!\"}")),
                    IsBase64Encoded = true
                },
                Assertions = (response, emulatormode) =>
                {
                    if (emulatormode == ApiGatewayEmulatorMode.Rest)
                    {

                    } else
                    {
                      Assert.Equal("{\"message\":\"Hello, World!\"}", ReadResponseBody(response));
                    }
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (emulatorMode == ApiGatewayEmulatorMode.Rest)
                    {
                        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"message\":\"Hello, World!\"}")), content); // rest doesnt decode
                    } else
                    {
                        Assert.Equal("{\"message\":\"Hello, World!\"}", content);
                    }
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
                Assertions = (response, emulatorMode) =>
                {
                    if (emulatorMode == ApiGatewayEmulatorMode.HttpV1)
                    {
                      Assert.Equal("text/plain; charset=utf-8", response.ContentType);
                    } else
                    {
                      Assert.Equal("application/json", response.ContentType);
                    }
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    if (emulatorMode == ApiGatewayEmulatorMode.HttpV1)
                    {
                      Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType.ToString());
                    } 
                    else
                    {
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal("application/json", response.Headers["Content-Type"]);
                    Assert.Equal("test,other", response.Headers["myheader"]);
                    Assert.Equal("secondvalue", response.Headers["anotherheader"]);
                    Assert.Equal(new[] { "headervalue", "headervalue2" }, response.Headers["headername"]);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal("application/json", response.Headers["Content-Type"]);
                    Assert.Equal("single-value", response.Headers["X-Custom-Header"]);
                    Assert.Equal(new[] { "multi-value1", "multi-value2" }, response.Headers["X-Multi-Header"]);
                    Assert.Equal(new[] { "multi-value1", "multi-value2", "single-value" }, response.Headers["Combined-Header"]);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.Equal("{\"message\":\"Hello, World!\"}".Length, response.ContentLength);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
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
                Assertions = (response, emulatorMode) =>
                {
                    string error = null;
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
                    Assert.Equal(statusCode, response.StatusCode);
                    Assert.Equal("application/json", response.ContentType);
                    Assert.Equal("{\"message\":"+error, ReadResponseBody(response));
                    Assert.Equal(contentLength, response.ContentLength);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    string error = null;
                    int contentLength;
                    if (emulatorMode == ApiGatewayEmulatorMode.Rest)
                    {
                        error = " \"Internal server error\"}";
                        contentLength = 36;
                    }
                    else
                    {
                        error = "\"Internal Server Error\"}";
                        contentLength = 35;
                    }
                    Assert.Equal(500, (int)response.StatusCode);
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Equal("{\"message\":"+error, content);
                    Assert.Equal(contentLength, response.Content.Headers.ContentLength);
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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal("application/json", response.ContentType);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
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
            Assertions = (response, emulatorMode) =>
            {
                Assert.True(response.Headers.ContainsKey("Date"));

                if (emulatorMode == ApiGatewayEmulatorMode.Rest)
                {
                    Assert.True(response.Headers.ContainsKey("x-amzn-RequestId"));
                    Assert.True(response.Headers.ContainsKey("x-amz-apigw-id"));
                    Assert.True(response.Headers.ContainsKey("X-Amzn-Trace-Id"));

                    Assert.Matches(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", response.Headers["x-amzn-RequestId"]);
                    Assert.Matches(@"^[A-Za-z0-9]{12}=$", response.Headers["x-amz-apigw-id"]);
                    Assert.Matches(@"^Root=1-[0-9a-f]{8}-[0-9a-f]{24};Parent=[0-9a-f]{16};Sampled=0;Lineage=1:[0-9a-f]{8}:0$", response.Headers["X-Amzn-Trace-Id"]);
                }
                else // HttpV1 or HttpV2
                {
                    Assert.True(response.Headers.ContainsKey("Apigw-Requestid"));
                    Assert.Matches(@"^[A-Za-z0-9]{14}=$", response.Headers["Apigw-Requestid"]);
                }
            },
            IntegrationAssertions = async (response, emulatorMode) =>
            {
                Assert.True(response.Headers.Contains("Date"));

                if (emulatorMode == ApiGatewayEmulatorMode.Rest)
                {
                    Assert.True(response.Headers.Contains("x-amzn-RequestId"));
                    Assert.True(response.Headers.Contains("x-amz-apigw-id"));
                    Assert.True(response.Headers.Contains("X-Amzn-Trace-Id"));

                    Assert.Matches(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", response.Headers.GetValues("x-amzn-RequestId").First());
                    Assert.Matches(@"^[A-Za-z0-9]{12}=$", response.Headers.GetValues("x-amz-apigw-id").First());
                    Assert.Matches(@"^Root=1-[0-9a-f]{8}-[0-9a-f]{24};Parent=[0-9a-f]{16};Sampled=0;Lineage=1:[0-9a-f]{8}:0$", response.Headers.GetValues("X-Amzn-Trace-Id").First());
                }
                else // HttpV1 or HttpV2
                {
                    Assert.True(response.Headers.Contains("Apigw-Requestid"));
                    Assert.Matches(@"^[A-Za-z0-9]{14}=$", response.Headers.GetValues("Apigw-Requestid").First());
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.Equal(200, response.StatusCode);
                    Assert.Equal("application/json", response.ContentType);
                    Assert.Equal("{\"message\":\"Hello, World!\"}", ReadResponseBody(response));
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(200, (int)response.StatusCode);
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.Equal(201, response.StatusCode);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(201, (int)response.StatusCode);
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Headers["Content-Type"]);
                    Assert.Equal("CustomValue", response.Headers["X-Custom-Header"]);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.Equal("{\"message\":\"Hello, API Gateway v2!\"}", ReadResponseBody(response));
                },
                IntegrationAssertions = async (response, emulatorMode) =>
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
                Assertions = (response, emulatormode) =>
                {
                    Assert.Equal("{\"message\":\"Hello, API Gateway v2!\"}", ReadResponseBody(response));
                },
                IntegrationAssertions = async (response, emulatorMode) =>
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.Equal("text/plain; charset=utf-8", response.ContentType);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Headers["Content-Type"]);
                    Assert.Equal("test,shouldhavesecondvalue", response.Headers["myheader"]);
                    Assert.Equal("secondvalue", response.Headers["anotherheader"]);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.Equal(201, response.StatusCode);
                    Assert.Equal("application/xml", response.ContentType);
                    Assert.Equal("{\"key\":\"value\"}", ReadResponseBody(response));
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.Equal(201, (int)response.StatusCode);
                    Assert.Equal("application/xml", response.Content.Headers.ContentType.ToString());
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
                Assertions = (response, emulatorMode) =>
                {
                    Assert.True(response.Headers.ContainsKey("Date"));
                    Assert.True(response.Headers.ContainsKey("Apigw-Requestid"));

                    Assert.Matches(@"^[A-Za-z0-9]{14}=$", response.Headers["Apigw-Requestid"]);
                },
                IntegrationAssertions = async (response, emulatorMode) =>
                {
                    Assert.True(response.Headers.Contains("Date"));
                    Assert.True(response.Headers.Contains("Apigw-Requestid"));

                    Assert.Matches(@"^[A-Za-z0-9]{14}=$", response.Headers.GetValues("Apigw-Requestid").First());
                    await Task.CompletedTask;
                }
            }
       };

    }

    private static string ReadResponseBody(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body);
        return reader.ReadToEnd();
    }

    public class ApiGatewayResponseTestCase
    {
        public object Response { get; set; }
        public Action<HttpResponse, ApiGatewayEmulatorMode> Assertions { get; set; }
        public Func<HttpResponseMessage, ApiGatewayEmulatorMode, Task> IntegrationAssertions { get; set; }
    }

}
