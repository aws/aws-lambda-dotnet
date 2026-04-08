// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.ALB;
using System.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class ALBApiAttributeTests
    {
        [Fact]
        public void Constructor_SetsRequiredProperties()
        {
            // Arrange & Act
            var attr = new ALBApiAttribute(
                "arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/50dc6c495c0c9188/f2f7dc8efc522ab2",
                "/api/orders/*",
                10);

            // Assert
            Assert.Equal("arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/50dc6c495c0c9188/f2f7dc8efc522ab2", attr.ListenerArn);
            Assert.Equal("/api/orders/*", attr.PathPattern);
            Assert.Equal(10, attr.Priority);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var attr = new ALBApiAttribute("arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def", "/hello", 1);

            // Assert
            Assert.False(attr.MultiValueHeaders);
            Assert.False(attr.IsMultiValueHeadersSet);
            Assert.Null(attr.HostHeader);
            Assert.Null(attr.HttpMethod);
            Assert.Null(attr.ResourceName);
            Assert.False(attr.IsResourceNameSet);
        }

        [Fact]
        public void MultiValueHeaders_WhenExplicitlySet_IsTracked()
        {
            var attr = new ALBApiAttribute("arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def", "/hello", 1);

            // Before setting
            Assert.False(attr.IsMultiValueHeadersSet);

            // After setting to false explicitly
            attr.MultiValueHeaders = false;
            Assert.True(attr.IsMultiValueHeadersSet);
            Assert.False(attr.MultiValueHeaders);

            // After setting to true
            attr.MultiValueHeaders = true;
            Assert.True(attr.IsMultiValueHeadersSet);
            Assert.True(attr.MultiValueHeaders);
        }

        [Fact]
        public void ResourceName_WhenExplicitlySet_IsTracked()
        {
            var attr = new ALBApiAttribute("arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def", "/hello", 1);

            Assert.False(attr.IsResourceNameSet);

            attr.ResourceName = "MyCustomName";
            Assert.True(attr.IsResourceNameSet);
            Assert.Equal("MyCustomName", attr.ResourceName);
        }

        [Fact]
        public void TemplateReference_IsAccepted()
        {
            var attr = new ALBApiAttribute("@MyALBListener", "/api/*", 5);

            Assert.Equal("@MyALBListener", attr.ListenerArn);
            Assert.StartsWith("@", attr.ListenerArn);
        }

        [Fact]
        public void OptionalProperties_CanBeSet()
        {
            var attr = new ALBApiAttribute("@MyALBListener", "/api/*", 5)
            {
                HostHeader = "api.example.com",
                HttpMethod = "GET",
                MultiValueHeaders = true,
                ResourceName = "MyALBTarget"
            };

            Assert.Equal("api.example.com", attr.HostHeader);
            Assert.Equal("GET", attr.HttpMethod);
            Assert.True(attr.MultiValueHeaders);
            Assert.Equal("MyALBTarget", attr.ResourceName);
        }

        // ===== Validation Tests =====

        [Fact]
        public void Validate_ValidArn_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute(
                "arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def",
                "/api/*",
                1);

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_ValidTemplateReference_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyALBListener", "/api/*", 1);

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_EmptyListenerArn_ReturnsError()
        {
            var attr = new ALBApiAttribute("", "/api/*", 1);

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ListenerArn", errors[0]);
            Assert.Contains("required", errors[0]);
        }

        [Fact]
        public void Validate_NullListenerArn_ReturnsError()
        {
            var attr = new ALBApiAttribute(null, "/api/*", 1);

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ListenerArn", errors[0]);
        }

        [Fact]
        public void Validate_InvalidListenerArn_NotArnOrReference_ReturnsError()
        {
            var attr = new ALBApiAttribute("some-random-string", "/api/*", 1);

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ListenerArn", errors[0]);
            Assert.Contains("arn:", errors[0]);
        }

        [Fact]
        public void Validate_EmptyPathPattern_ReturnsError()
        {
            var attr = new ALBApiAttribute("@MyListener", "", 1);

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("PathPattern", errors[0]);
            Assert.Contains("required", errors[0]);
        }

        [Fact]
        public void Validate_NullPathPattern_ReturnsError()
        {
            var attr = new ALBApiAttribute("@MyListener", null, 1);

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("PathPattern", errors[0]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(50001)]
        [InlineData(100000)]
        public void Validate_InvalidPriority_ReturnsError(int priority)
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", priority);

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Priority", errors[0]);
            Assert.Contains("1 and 50000", errors[0]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50000)]
        [InlineData(100)]
        [InlineData(25000)]
        public void Validate_ValidPriority_ReturnsNoErrors(int priority)
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", priority);

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidResourceName_ReturnsError()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                ResourceName = "invalid-name!"
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ResourceName", errors[0]);
            Assert.Contains("alphanumeric", errors[0]);
        }

        [Fact]
        public void Validate_ValidResourceName_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                ResourceName = "MyValidResource123"
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_UnsetResourceName_ReturnsNoErrors()
        {
            // ResourceName not set should not produce validation errors
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1);

            var errors = attr.Validate();
            Assert.Empty(errors);
            Assert.False(attr.IsResourceNameSet);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        [InlineData("get")]
        [InlineData("post")]
        public void Validate_ValidHttpMethod_ReturnsNoErrors(string method)
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HttpMethod = method
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidHttpMethod_ReturnsError()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HttpMethod = "INVALID"
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("HttpMethod", errors[0]);
        }

        [Fact]
        public void Validate_NullHttpMethod_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HttpMethod = null
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MultipleErrors_ReturnsAll()
        {
            var attr = new ALBApiAttribute("", "", 0)
            {
                ResourceName = "invalid-name!",
                HttpMethod = "INVALID"
            };

            var errors = attr.Validate();
            // Should have errors for: ListenerArn, PathPattern, Priority, ResourceName, HttpMethod
            Assert.Equal(5, errors.Count);
            Assert.Contains(errors, e => e.Contains("ListenerArn"));
            Assert.Contains(errors, e => e.Contains("PathPattern"));
            Assert.Contains(errors, e => e.Contains("Priority"));
            Assert.Contains(errors, e => e.Contains("ResourceName"));
            Assert.Contains(errors, e => e.Contains("HttpMethod"));
        }

        [Fact]
        public void Validate_AllValidWithOptionals_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute(
                "arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def",
                "/api/v1/products/*",
                42)
            {
                MultiValueHeaders = true,
                HostHeader = "api.example.com",
                HttpMethod = "POST",
                ResourceName = "ProductsALB"
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        // ===== HTTP Header Condition Tests =====

        [Fact]
        public void HttpHeaderCondition_DefaultValues_AreNull()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1);

            Assert.Null(attr.HttpHeaderConditionName);
            Assert.Null(attr.HttpHeaderConditionValues);
        }

        [Fact]
        public void HttpHeaderCondition_BothSet_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HttpHeaderConditionName = "X-Environment",
                HttpHeaderConditionValues = new[] { "dev", "staging" }
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
            Assert.Equal("X-Environment", attr.HttpHeaderConditionName);
            Assert.Equal(2, attr.HttpHeaderConditionValues.Length);
            Assert.Equal("dev", attr.HttpHeaderConditionValues[0]);
            Assert.Equal("staging", attr.HttpHeaderConditionValues[1]);
        }

        [Fact]
        public void HttpHeaderCondition_NameSetWithoutValues_ReturnsError()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HttpHeaderConditionName = "X-Environment"
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("HttpHeaderConditionName", errors[0]);
            Assert.Contains("HttpHeaderConditionValues", errors[0]);
        }

        [Fact]
        public void HttpHeaderCondition_ValuesSetWithoutName_ReturnsError()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HttpHeaderConditionValues = new[] { "dev" }
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("HttpHeaderConditionValues", errors[0]);
            Assert.Contains("HttpHeaderConditionName", errors[0]);
        }

        [Fact]
        public void HttpHeaderCondition_NameSetWithEmptyValues_ReturnsError()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HttpHeaderConditionName = "User-Agent",
                HttpHeaderConditionValues = new string[0]
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("HttpHeaderConditionName", errors[0]);
        }

        [Fact]
        public void HttpHeaderCondition_WithWildcards_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HttpHeaderConditionName = "User-Agent",
                HttpHeaderConditionValues = new[] { "*Chrome*", "*Safari*" }
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        // ===== Query String Condition Tests =====

        [Fact]
        public void QueryStringConditions_DefaultValue_IsNull()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1);
            Assert.Null(attr.QueryStringConditions);
        }

        [Fact]
        public void QueryStringConditions_WithKeyValuePairs_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                QueryStringConditions = new[] { "version=v1", "=*example*" }
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
            Assert.Equal(2, attr.QueryStringConditions.Length);
            Assert.Equal("version=v1", attr.QueryStringConditions[0]);
            Assert.Equal("=*example*", attr.QueryStringConditions[1]);
        }

        [Fact]
        public void QueryStringConditions_WithSingleEntry_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                QueryStringConditions = new[] { "env=prod" }
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        // ===== Source IP Condition Tests =====

        [Fact]
        public void SourceIpConditions_DefaultValue_IsNull()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1);
            Assert.Null(attr.SourceIpConditions);
        }

        [Fact]
        public void SourceIpConditions_WithCidrBlocks_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                SourceIpConditions = new[] { "192.0.2.0/24", "198.51.100.10/32" }
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
            Assert.Equal(2, attr.SourceIpConditions.Length);
        }

        [Fact]
        public void SourceIpConditions_WithIPv6_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                SourceIpConditions = new[] { "2001:db8::/32" }
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        // ===== Combined Condition Tests =====

        [Fact]
        public void AllConditions_CanBeSetTogether_ReturnsNoErrors()
        {
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                HostHeader = "api.example.com",
                HttpMethod = "POST",
                HttpHeaderConditionName = "X-Environment",
                HttpHeaderConditionValues = new[] { "dev" },
                QueryStringConditions = new[] { "version=v1" },
                SourceIpConditions = new[] { "10.0.0.0/8" }
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }
    }
}
