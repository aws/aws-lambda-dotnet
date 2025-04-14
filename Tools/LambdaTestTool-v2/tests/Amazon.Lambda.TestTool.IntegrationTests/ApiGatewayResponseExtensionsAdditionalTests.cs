// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Microsoft.AspNetCore.Http;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using System.Text;
using Xunit;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    public class ApiGatewayResponseExtensionsAdditionalTests
    {
        [Fact]
        public async Task ToHttpResponse_RestAPIGatewayV1DecodesBase64()
        {
            var testResponse = new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
                IsBase64Encoded = true
            };

            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            await testResponse.ToHttpResponseAsync(httpContext, ApiGatewayEmulatorMode.Rest);

            httpContext.Response.Body.Position = 0;

            Assert.Equal(200, (int)httpContext.Response.StatusCode);
            var content = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
            Assert.Equal("test", content);
        }

        [Fact]
        public async Task ToHttpResponse_HttpV1APIGatewayV1DecodesBase64()
        {
            var testResponse = new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
                IsBase64Encoded = true
            };

            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            await testResponse.ToHttpResponseAsync(httpContext, ApiGatewayEmulatorMode.HttpV1);

            httpContext.Response.Body.Position = 0;

            Assert.Equal(200, (int)httpContext.Response.StatusCode);
            var content = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
            Assert.Equal("test", content);

        }
    }
}
