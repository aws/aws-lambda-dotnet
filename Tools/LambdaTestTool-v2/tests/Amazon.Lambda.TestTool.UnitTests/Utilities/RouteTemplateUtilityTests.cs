// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.UnitTests.Utilities;

using Amazon.Lambda.TestTool.Utilities;
using Xunit;

public class RouteTemplateUtilityTests
{
    [Theory]
    [InlineData("/users/{id}", "/users/123", "id", "123")]
    [InlineData("/users/{id}/orders/{orderId}", "/users/123/orders/456", "id", "123", "orderId", "456")]
    [InlineData("/products/{category}/{id}", "/products/electronics/laptop-123", "category", "electronics", "id", "laptop-123")]
    [InlineData("/api/{version}/users/{userId}", "/api/v1/users/abc-xyz", "version", "v1", "userId", "abc-xyz")]
    [InlineData("/api/{proxy+}", "/api/v1/users/abc-xyz", "proxy", "v1/users/abc-xyz")]
    [InlineData("/{param}", "/value", "param", "value")]
    [InlineData("/{param}/", "/value/", "param", "value")]
    [InlineData("/static/{param}/static", "/static/value/static", "param", "value")]
    [InlineData("/{param1}/{param2}", "/123/456", "param1", "123", "param2", "456")]
    [InlineData("/{param1}/{param2+}", "/123/456/789/000", "param1", "123", "param2", "456/789/000")]
    [InlineData("/api/{version}/{proxy+}", "/api/v2/users/123/orders", "version", "v2", "proxy", "users/123/orders")]
    [InlineData("/{param}", "/value with spaces", "param", "value with spaces")]
    [InlineData("/{param+}", "/a/very/long/path/with/many/segments", "param", "a/very/long/path/with/many/segments")]
    [InlineData("/api/{proxy+}", "/api/", "proxy", "")]
    [InlineData("/api/{proxy+}", "/api", "proxy", "")]
    [InlineData("/{param1}/static/{param2+}", "/value1/static/rest/of/the/path", "param1", "value1", "param2", "rest/of/the/path")]
    [InlineData("/users/{id}/posts/{postId?}", "/users/123/posts", "id", "123")]
    [InlineData("/{param:int}", "/123", "param:int", "123")]
    [InlineData("/users/{id}", "/users/", new string[] { })]
    [InlineData("/", "/", new string[] { })]
    public void ExtractPathParameters_ShouldExtractCorrectly(string routeTemplate, string actualPath, params string[] expectedKeyValuePairs)
    {
        // Arrange
        var expected = new Dictionary<string, string>();
        for (var i = 0; i < expectedKeyValuePairs.Length; i += 2)
        {
            expected[expectedKeyValuePairs[i]] = expectedKeyValuePairs[i + 1];
        }

        // Act
        var result = RouteTemplateUtility.ExtractPathParameters(routeTemplate, actualPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/users/{id}", "/products/123")]
    [InlineData("/api/{version}/users", "/api/v1/products")]
    [InlineData("/products/{category}/{id}", "/products/electronics")]
    public void ExtractPathParameters_ShouldReturnEmptyDictionary_WhenNoMatch(string routeTemplate, string actualPath)
    {
        // Act
        var result = RouteTemplateUtility.ExtractPathParameters(routeTemplate, actualPath);

        // Assert
        Assert.Empty(result);
    }
}
