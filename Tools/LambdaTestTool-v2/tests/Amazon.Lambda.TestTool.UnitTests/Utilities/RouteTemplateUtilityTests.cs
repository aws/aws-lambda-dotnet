// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.UnitTests.Utilities;

using Amazon.Lambda.TestTool.Utilities;
using Microsoft.AspNetCore.Routing.Template;
using Xunit;

public class RouteTemplateUtilityTests
{
    [Theory]
    [InlineData("/users/{id}", "/users/123", "id", "123")]
    [InlineData("/users/{id}/orders/{orderId}", "/users/123/orders/456", "id", "123", "orderId", "456")]
    [InlineData("/products/{category}/{id}", "/products/electronics/laptop-123", "category", "electronics", "id", "laptop-123")]
    [InlineData("/api/{version}/users/{userId}", "/api/v1/users/abc-xyz", "version", "v1", "userId", "abc-xyz")]
    public void ExtractPathParameters_ShouldExtractCorrectly(string routeTemplate, string actualPath, params string[] expectedKeyValuePairs)
    {
        // Arrange
        var expected = new Dictionary<string, string>();
        for (int i = 0; i < expectedKeyValuePairs.Length; i += 2)
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

    [Theory]
    [InlineData("/users/{id:int}", "/users/123", "id", "123")]
    [InlineData("/users/{id:guid}", "/users/550e8400-e29b-41d4-a716-446655440000", "id", "550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("/api/{version:regex(^v[0-9]+$)}/users/{userId}", "/api/v1/users/abc-xyz", "version", "v1", "userId", "abc-xyz")]
    public void ExtractPathParameters_ShouldHandleConstraints(string routeTemplate, string actualPath, params string[] expectedKeyValuePairs)
    {
        // Arrange
        var expected = new Dictionary<string, string>();
        for (int i = 0; i < expectedKeyValuePairs.Length; i += 2)
        {
            expected[expectedKeyValuePairs[i]] = expectedKeyValuePairs[i + 1];
        }

        // Act
        var result = RouteTemplateUtility.ExtractPathParameters(routeTemplate, actualPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetDefaults_ShouldReturnCorrectDefaults()
    {
        // Arrange
        var template = TemplateParser.Parse("/api/{version=v1}/users/{id}");

        // Act
        var result = RouteTemplateUtility.GetDefaults(template);

        // Assert
        Assert.Single(result);
        Assert.Equal("v1", result["version"]);
    }

    [Fact]
    public void GetDefaults_ShouldReturnEmptyDictionary_WhenNoDefaults()
    {
        // Arrange
        var template = TemplateParser.Parse("/api/{version}/users/{id}");

        // Act
        var result = RouteTemplateUtility.GetDefaults(template);

        // Assert
        Assert.Empty(result);
    }
}
