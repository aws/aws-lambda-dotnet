using System;
using System.Net;
using System.Collections.Generic;
using System.Text.Json;
using Amazon.Lambda.Annotations.APIGateway;
using Xunit;
using System.IO;
using System.Text.Json.Nodes;
using System.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class HttpResultsTest
    {
        [Fact]
        public void OkNoBody()
        {
            var result = HttpResults.Ok();
            ValidateResult(result, HttpStatusCode.OK);
        }

        [Fact]
        public void OkStringBody()
        {
            var body = "Hello World";
            var result = HttpResults.Ok(body);
            ValidateResult(result, HttpStatusCode.OK, body, headers: new Dictionary<string, IList<string>>
                            {
                                { "content-type", new List<string> { "text/plain" } }
                            }
                );
        }

        [Fact]
        public void OverrideContentType()
        {
            var body = "Hello World";
            var result = HttpResults.Ok(body).AddHeader("content-type", "custom/foo");
            ValidateResult(result, HttpStatusCode.OK, body, headers: new Dictionary<string, IList<string>>
                            {
                                { "content-type", new List<string> { "custom/foo" } }
                            }
                );
        }

        [Fact]
        public void OkByteArrayBody()
        {
            var body = new byte[] { 0x01, 0x02 };

            var result = HttpResults.Ok(body);
            ValidateResult(result, HttpStatusCode.OK, Convert.ToBase64String(body), isBase64Encoded: true, headers: new Dictionary<string, IList<string>>
                            {
                                { "content-type", new List<string> { "application/octet-stream" } }
                            }
                );
        }

        [Fact]
        public void OkStreamBody()
        {
            var body = new byte[] { 0x01, 0x02 };
            var result = HttpResults.Ok(new MemoryStream(body));
            ValidateResult(result, HttpStatusCode.OK, Convert.ToBase64String(body), isBase64Encoded: true, headers: new Dictionary<string, IList<string>>
                            {
                                { "content-type", new List<string> { "application/octet-stream" } }
                            }
                );
        }

        [Fact]
        public void OkListOfBytesBody()
        {
            var body = new byte[] { 0x01, 0x02 };
            var result = HttpResults.Ok(new List<byte>(body));
            ValidateResult(result, HttpStatusCode.OK, Convert.ToBase64String(body), isBase64Encoded: true, headers: new Dictionary<string, IList<string>>
                            {
                                { "content-type", new List<string> { "application/octet-stream" } }
                            }
                );
        }

        [Fact]
        public void OkWithTypeBody()
        {
            var body = new FakeBody();
            var result = HttpResults.Ok(body);
            ValidateResult(result, HttpStatusCode.OK, "{\"Id\":1}", isBase64Encoded: false, headers: new Dictionary<string, IList<string>>
                            {
                                { "content-type", new List<string> { "application/json" } }
                            }
                );
        }

        [Fact]
        public void OkWithSingleValueHeader()
        {
            var result = HttpResults.Ok()
                                    .AddHeader("header1", "value1")
                                    .AddHeader("header2", "value2");

            ValidateResult(result, HttpStatusCode.OK, 
                headers: new Dictionary<string, IList<string>> 
                            { 
                                { "header1", new List<string> { "value1" } }, 
                                { "header2", new List<string> { "value2" } } 
                            }
                );
        }

        [Fact]
        public void OkWithMultiValueHeader()
        {
            var result = HttpResults.Ok()
                                    .AddHeader("header1", "foo1")
                                    .AddHeader("header1", "foo2")
                                    .AddHeader("header2", "bar1")
                                    .AddHeader("header2", "bar2");

            ValidateResult(result, HttpStatusCode.OK,
                headers: new Dictionary<string, IList<string>>
                            {
                                { "header1", new List<string> { "foo1", "foo2" } },
                                { "header2", new List<string> { "bar1", "bar2" } }
                            }
                );
        }

        [Fact]
        public void Accepted()
        {
            var result = HttpResults.Accepted();
            ValidateResult(result, HttpStatusCode.Accepted);
        }

        [Fact]
        public void BadRequest()
        {
            var result = HttpResults.BadRequest();
            ValidateResult(result, HttpStatusCode.BadRequest);
        }

        [Fact]
        public void Conflict()
        {
            var result = HttpResults.Conflict();
            ValidateResult(result, HttpStatusCode.Conflict);
        }

        [Fact]
        public void Created()
        {
            var result = HttpResults.Created();
            ValidateResult(result, HttpStatusCode.Created);
        }

        [Fact]
        public void CreatedWithUriAndBody()
        {
            var result = HttpResults.Created("http://localhost/foo", "Resource Created");
            ValidateResult(result, HttpStatusCode.Created, "Resource Created",
                headers: new Dictionary<string, IList<string>>
                            {
                                { "content-type", new List<string> { "text/plain" } },
                                { "location", new List<string> { "http://localhost/foo" } }
                            }
                );
        }

        [Fact]
        public void Forbid()
        {
            var result = HttpResults.Forbid();
            ValidateResult(result, HttpStatusCode.Forbidden);
        }

        [Fact]
        public void Redirect_PermanentRedirect()
        {
            var result = HttpResults.Redirect("http://localhost/foo", permanent: true, preserveMethod: true);
            ValidateResult(result, HttpStatusCode.PermanentRedirect,
                headers: new Dictionary<string, IList<string>>
                            {
                                { "location", new List<string> { "http://localhost/foo" } }
                            }
                );
        }

        [Fact]
        public void Redirect_MovedPermanently()
        {
            var result = HttpResults.Redirect("http://localhost/foo", permanent: true, preserveMethod: false);
            ValidateResult(result, HttpStatusCode.MovedPermanently,
                headers: new Dictionary<string, IList<string>>
                            {
                                { "location", new List<string> { "http://localhost/foo" } }
                            }
                );
        }

        [Fact]
        public void Redirect_TemporaryRedirect()
        {
            var result = HttpResults.Redirect("http://localhost/foo", permanent: false, preserveMethod: true);
            ValidateResult(result, HttpStatusCode.TemporaryRedirect,
                headers: new Dictionary<string, IList<string>>
                            {
                                { "location", new List<string> { "http://localhost/foo" } }
                            }
                );
        }

        [Fact]
        public void Redirect_Redirect()
        {
            var result = HttpResults.Redirect("http://localhost/foo", permanent: false, preserveMethod: false);
            ValidateResult(result, HttpStatusCode.Redirect,
                headers: new Dictionary<string, IList<string>>
                            {
                                { "location", new List<string> { "http://localhost/foo" } }
                            }
                );
        }

        [Fact]
        public void NotFound()
        {
            var result = HttpResults.NotFound();
            ValidateResult(result, HttpStatusCode.NotFound);
        }

        [Fact]
        public void Unauthorized()
        {
            var result = HttpResults.Unauthorized();
            ValidateResult(result, HttpStatusCode.Unauthorized);
        }

        [Fact]
        public void MixCaseHeaders()
        {
            var result = HttpResults.Ok()
                                    .AddHeader("key", "value1")
                                    .AddHeader("key", "value2")
                                    .AddHeader("KEY", "VALUE3");

            ValidateResult(result, HttpStatusCode.OK, headers: new Dictionary<string, IList<string>>
            {
                {"key", new List<string> {"value1", "value2", "VALUE3"} }
            });
        }
        
        [Fact]
        public void InternalServerError()
        {
            var result = HttpResults.InternalServerError();
            ValidateResult(result, HttpStatusCode.InternalServerError);
        }
        
        [Fact]
        public void BadGateway()
        {
            var result = HttpResults.BadGateway();
            ValidateResult(result, HttpStatusCode.BadGateway);
        }
        
        [Fact]
        public void ServiceUnavailable_WithoutRetryAfter()
        {
            var result = HttpResults.ServiceUnavailable();
            ValidateResult(result, HttpStatusCode.ServiceUnavailable);
        }
        
        [Fact]
        public void ServiceUnavailable_WithRetryAfter()
        {
            var result = HttpResults.ServiceUnavailable(100);
            ValidateResult(result, HttpStatusCode.ServiceUnavailable, headers: new Dictionary<string, IList<string>>
            {
                {"retry-after", new List<string> {"100"} }
            });
        }


        private void ValidateResult(IHttpResult result, HttpStatusCode statusCode, string body = null, bool isBase64Encoded = false, IDictionary<string, IList<string>> headers = null)
        {
            var testScenarios = new List<Tuple<HttpResultSerializationOptions.ProtocolFormat, HttpResultSerializationOptions.ProtocolVersion>>
            {
                new (HttpResultSerializationOptions.ProtocolFormat.RestApi, HttpResultSerializationOptions.ProtocolVersion.V1),
                new (HttpResultSerializationOptions.ProtocolFormat.HttpApi, HttpResultSerializationOptions.ProtocolVersion.V1),
                new (HttpResultSerializationOptions.ProtocolFormat.HttpApi, HttpResultSerializationOptions.ProtocolVersion.V2)
            };

            foreach(var (format, version) in testScenarios)
            {
                var stream = result.Serialize(new HttpResultSerializationOptions { Format = format, Version = version });
                var jsonDoc = JsonDocument.Parse(stream);
                if (format == HttpResultSerializationOptions.ProtocolFormat.RestApi || (format == HttpResultSerializationOptions.ProtocolFormat.HttpApi && version == HttpResultSerializationOptions.ProtocolVersion.V1))
                {
                    Assert.Equal((int)statusCode, jsonDoc.RootElement.GetProperty("statusCode").GetInt32());

                    if(body != null)
                    {
                        Assert.Equal(body, jsonDoc.RootElement.GetProperty("body").GetString());
                        Assert.Equal(isBase64Encoded, jsonDoc.RootElement.GetProperty("isBase64Encoded").GetBoolean());
                    }
                    else
                    {
                        var bodyProperties = jsonDoc.RootElement.GetProperty("body");
                        Assert.Equal(JsonValueKind.Null, bodyProperties.ValueKind);
                    }

                    if (headers != null)
                    {
                        var headerProperties = jsonDoc.RootElement.GetProperty("multiValueHeaders");
                        Assert.Equal(headers.Count, headerProperties.EnumerateObject().Count());

                        foreach(var kvp in headers)
                        {
                            if(!headerProperties.TryGetProperty(kvp.Key, out var values))
                            {
                                Assert.Fail($"Fail to find header {kvp.Key}");
                            }

                            Assert.Equal(JsonValueKind.Array, values.ValueKind);
                            Assert.Equal(kvp.Value.Count, values.GetArrayLength());
                            for(var i = 0; i < kvp.Value.Count; i++)
                            {
                                Assert.Equal(kvp.Value[i], values[i].GetString());
                            }
                        }
                    }
                    else
                    {
                        var headerProperties = jsonDoc.RootElement.GetProperty("multiValueHeaders");
                        Assert.Equal(JsonValueKind.Null, headerProperties.ValueKind);
                    }
                }
                else
                {
                    Assert.Equal((int)statusCode, jsonDoc.RootElement.GetProperty("statusCode").GetInt32());
                    if (body != null)
                    {
                        Assert.Equal(body, jsonDoc.RootElement.GetProperty("body").GetString());
                        Assert.Equal(isBase64Encoded, jsonDoc.RootElement.GetProperty("isBase64Encoded").GetBoolean());
                    }
                    else
                    {
                        var bodyProperties = jsonDoc.RootElement.GetProperty("body");
                        Assert.Equal(JsonValueKind.Null, bodyProperties.ValueKind);
                    }

                    if (headers != null)
                    {
                        var headerProperties = jsonDoc.RootElement.GetProperty("headers");
                        Assert.Equal(headers.Count, headerProperties.EnumerateObject().Count());

                        foreach (var kvp in headers)
                        {
                            var commaDelimtedValues = string.Join(",", kvp.Value);
                            if (!headerProperties.TryGetProperty(kvp.Key, out var values))
                            {
                                Assert.Fail($"Fail to find header {kvp.Key}");
                            }
                            Assert.Equal(commaDelimtedValues, values.GetString());                            
                        }
                    }
                    else
                    {
                        var headerProperties = jsonDoc.RootElement.GetProperty("headers");
                        Assert.Equal(JsonValueKind.Null, headerProperties.ValueKind);
                    }
                }
            }
        }

        public class FakeBody
        {
            public int Id { get; set; } = 1;
        }
    }
}
