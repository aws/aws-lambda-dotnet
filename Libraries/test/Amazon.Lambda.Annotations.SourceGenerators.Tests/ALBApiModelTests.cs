using Amazon.Lambda.Annotations.ALB;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class ALBApiModelTests
    {
        [Fact]
        public void TypeFullNames_ContainsALBConstants()
        {
            Assert.Equal("Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest", TypeFullNames.ApplicationLoadBalancerRequest);
            Assert.Equal("Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerResponse", TypeFullNames.ApplicationLoadBalancerResponse);
            Assert.Equal("Amazon.Lambda.Annotations.ALB.ALBApiAttribute", TypeFullNames.ALBApiAttribute);
        }

        [Fact]
        public void TypeFullNames_Events_ContainsALBApiAttribute()
        {
            Assert.Contains(TypeFullNames.ALBApiAttribute, TypeFullNames.Events);
        }

        [Fact]
        public void TypeFullNames_ALBRequests_ContainsLoadBalancerRequest()
        {
            Assert.Contains(TypeFullNames.ApplicationLoadBalancerRequest, TypeFullNames.ALBRequests);
            Assert.Single(TypeFullNames.ALBRequests);
        }

        [Fact]
        public void EventType_HasALBValue()
        {
            // Verify the ALB enum value exists
            var albEvent = EventType.ALB;
            Assert.Equal(EventType.ALB, albEvent);

            // Verify it's distinct from other event types
            Assert.NotEqual(EventType.API, albEvent);
            Assert.NotEqual(EventType.SQS, albEvent);
        }

        [Fact]
        public void ALBApiAttributeBuilder_BuildsFromConstructorArgs()
        {
            // This tests the attribute builder by constructing an ALBApiAttribute directly
            // (since we can't easily mock Roslyn AttributeData in unit tests, we test the attribute itself)
            var attr = new ALBApiAttribute("@MyListener", "/api/*", 5);

            Assert.Equal("@MyListener", attr.ListenerArn);
            Assert.Equal("/api/*", attr.PathPattern);
            Assert.Equal(5, attr.Priority);
        }

        [Fact]
        public void ALBApiAttributeBuilder_BuildsWithAllOptionalProperties()
        {
            var attr = new ALBApiAttribute("arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def", "/api/v1/*", 10)
            {
                MultiValueHeaders = true,
                HostHeader = "api.example.com",
                HttpMethod = "POST",
                ResourceName = "MyCustomALB"
            };

            Assert.Equal("arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def", attr.ListenerArn);
            Assert.Equal("/api/v1/*", attr.PathPattern);
            Assert.Equal(10, attr.Priority);
            Assert.True(attr.MultiValueHeaders);
            Assert.True(attr.IsMultiValueHeadersSet);
            Assert.Equal("api.example.com", attr.HostHeader);
            Assert.Equal("POST", attr.HttpMethod);
            Assert.Equal("MyCustomALB", attr.ResourceName);
            Assert.True(attr.IsResourceNameSet);
        }

        [Fact]
        public void LambdaMethodModel_ReturnsApplicationLoadBalancerResponse_WhenDirectReturn()
        {
            var model = new LambdaMethodModel
            {
                ReturnsVoid = false,
                ReturnsGenericTask = false,
                ReturnType = new TypeModel
                {
                    FullName = TypeFullNames.ApplicationLoadBalancerResponse,
                    TypeArguments = new List<TypeModel>()
                }
            };

            Assert.True(model.ReturnsApplicationLoadBalancerResponse);
        }

        [Fact]
        public void LambdaMethodModel_ReturnsApplicationLoadBalancerResponse_WhenTaskReturn()
        {
            var model = new LambdaMethodModel
            {
                ReturnsVoid = false,
                ReturnsGenericTask = true,
                ReturnType = new TypeModel
                {
                    FullName = "System.Threading.Tasks.Task`1",
                    TypeArguments = new List<TypeModel>
                    {
                        new TypeModel { FullName = TypeFullNames.ApplicationLoadBalancerResponse }
                    }
                }
            };

            Assert.True(model.ReturnsApplicationLoadBalancerResponse);
        }

        [Fact]
        public void LambdaMethodModel_ReturnsApplicationLoadBalancerResponse_FalseWhenVoid()
        {
            var model = new LambdaMethodModel
            {
                ReturnsVoid = true,
                ReturnsGenericTask = false,
                ReturnType = new TypeModel
                {
                    FullName = "void",
                    TypeArguments = new List<TypeModel>()
                }
            };

            Assert.False(model.ReturnsApplicationLoadBalancerResponse);
        }

        [Fact]
        public void LambdaMethodModel_ReturnsApplicationLoadBalancerResponse_FalseWhenDifferentType()
        {
            var model = new LambdaMethodModel
            {
                ReturnsVoid = false,
                ReturnsGenericTask = false,
                ReturnType = new TypeModel
                {
                    FullName = "System.String",
                    TypeArguments = new List<TypeModel>()
                }
            };

            Assert.False(model.ReturnsApplicationLoadBalancerResponse);
        }

        [Fact]
        public void ParameterListExtension_ALBRequest_IsNotConvertible()
        {
            // ApplicationLoadBalancerRequest parameters should be treated as pass-through
            var parameters = new List<ParameterModel>
            {
                new ParameterModel
                {
                    Name = "request",
                    Type = new TypeModel { FullName = TypeFullNames.ApplicationLoadBalancerRequest },
                    Attributes = new List<AttributeModel>()
                }
            };

            Assert.False(parameters.HasConvertibleParameter());
        }

        [Fact]
        public void ParameterListExtension_FromQuery_IsConvertible()
        {
            // A [FromQuery] string parameter should be convertible
            var parameters = new List<ParameterModel>
            {
                new ParameterModel
                {
                    Name = "name",
                    Type = new TypeModel { FullName = "System.String" },
                    Attributes = new List<AttributeModel>
                    {
                        new AttributeModel<FromQueryAttribute>
                        {
                            Data = new FromQueryAttribute(),
                            Type = new TypeModel { FullName = TypeFullNames.FromQueryAttribute }
                        }
                    }
                }
            };

            Assert.True(parameters.HasConvertibleParameter());
        }

        [Fact]
        public void ParameterListExtension_ILambdaContext_IsNotConvertible()
        {
            var parameters = new List<ParameterModel>
            {
                new ParameterModel
                {
                    Name = "context",
                    Type = new TypeModel { FullName = TypeFullNames.ILambdaContext },
                    Attributes = new List<AttributeModel>()
                }
            };

            Assert.False(parameters.HasConvertibleParameter());
        }

        [Fact]
        public void ParameterListExtension_FromBodyString_IsNotConvertible()
        {
            // A [FromBody] string parameter should NOT be convertible (string body is pass-through)
            var parameters = new List<ParameterModel>
            {
                new ParameterModel
                {
                    Name = "body",
                    Type = new TypeModel { FullName = "string" },
                    Attributes = new List<AttributeModel>
                    {
                        new AttributeModel<FromBodyAttribute>
                        {
                            Data = new FromBodyAttribute(),
                            Type = new TypeModel { FullName = TypeFullNames.FromBodyAttribute }
                        }
                    }
                }
            };

            Assert.False(parameters.HasConvertibleParameter());
        }

        [Fact]
        public void DiagnosticDescriptors_FromRouteNotSupportedOnAlb_Exists()
        {
            Assert.Equal("AWSLambda0134", DiagnosticDescriptors.FromRouteNotSupportedOnAlb.Id);
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, DiagnosticDescriptors.FromRouteNotSupportedOnAlb.DefaultSeverity);
        }

        [Fact]
        public void DiagnosticDescriptors_AlbUnmappedParameter_Exists()
        {
            Assert.Equal("AWSLambda0135", DiagnosticDescriptors.AlbUnmappedParameter.Id);
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, DiagnosticDescriptors.AlbUnmappedParameter.DefaultSeverity);
        }

        [Fact]
        public void ALBFromQuery_ParameterName_DefaultsToParameterName()
        {
            // When Name is not set, FromQueryAttribute should default to parameter name
            var attr = new FromQueryAttribute();
            Assert.Null(attr.Name);
        }

        [Fact]
        public void ALBFromQuery_ParameterName_UsesExplicitName()
        {
            var attr = new FromQueryAttribute { Name = "custom_name" };
            Assert.Equal("custom_name", attr.Name);
        }

        [Fact]
        public void ALBFromHeader_ParameterName_UsesExplicitName()
        {
            var attr = new FromHeaderAttribute { Name = "X-Custom-Header" };
            Assert.Equal("X-Custom-Header", attr.Name);
        }
    }
}
