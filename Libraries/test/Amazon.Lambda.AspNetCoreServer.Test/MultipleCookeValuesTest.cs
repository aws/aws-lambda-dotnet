using System;
using System.Collections.Generic;
using System.Text;

using Xunit;
using Amazon.Lambda.AspNetCoreServer;
using Microsoft.Extensions.Primitives;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class MultipleCookeValuesTest
    {

        [Fact]
        public void SingleValue()
        {
            var apiGatewayHeaders = new Dictionary<string, string>();
            StringValues aspNetCoreCookies = new StringValues("Foo");

            APIGatewayProxyFunction.ProcessCookies(apiGatewayHeaders, aspNetCoreCookies);
            Assert.Single(apiGatewayHeaders);
        }

        [Fact]
        public void HundredCookies()
        {
            var list = new List<string>();
            for(int i = 0; i < 100; i++)
            {
                list.Add($"Bar{i}");
            }

            var apiGatewayHeaders = new Dictionary<string, string>();
            StringValues aspNetCoreCookies = new StringValues(list.ToArray());

            APIGatewayProxyFunction.ProcessCookies(apiGatewayHeaders, aspNetCoreCookies);
            Assert.Equal(100, apiGatewayHeaders.Count);

            foreach(var key in apiGatewayHeaders.Keys)
            {
                Assert.Equal("set-cookie", key.ToLower());
            }
        }
    }
}
