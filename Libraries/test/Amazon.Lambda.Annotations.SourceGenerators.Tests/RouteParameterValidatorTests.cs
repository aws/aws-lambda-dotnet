using System;
using System.Collections.Generic;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Validation;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class RouteParameterValidatorTests
    {
        [Fact]
        public void Validate_AllRouteParamsFound()
        {
            var routeParameters = new HashSet<string> {"id1", "id2"};
            var lambdaMethodParams = new List<ParameterModel>
            {
                new ParameterModel
                {
                    Attributes = new List<AttributeModel>
                    {
                        new AttributeModel<FromRouteAttribute>
                        {
                            Data = new FromRouteAttribute { Name = "id1"}
                        }
                    },
                    Name = "identifier1"
                },
                new ParameterModel
                {
                    Name = "id2"
                }
            };

            var (isValid, missingRouteParams) = RouteParametersValidator.Validate(routeParameters, lambdaMethodParams);
            Assert.True(isValid);
            Assert.Empty(missingRouteParams);
        }

        [Fact]
        public void Validate_MissingRouteParam()
        {
            var routeParameters = new HashSet<string> {"id1", "id2"};
            var lambdaMethodParams = new List<ParameterModel>
            {
                new ParameterModel
                {
                    Attributes = new List<AttributeModel>
                    {
                        new AttributeModel<FromRouteAttribute>
                        {
                            Data = new FromRouteAttribute { Name = "id1"}
                        }
                    }
                }
            };

            var (isValid, missingRouteParams) = RouteParametersValidator.Validate(routeParameters, lambdaMethodParams);
            Assert.False(isValid);
            Assert.NotEmpty(missingRouteParams);
            Assert.Equal("id2", missingRouteParams[0]);
        }

        [Fact]
        public void Validate_RouteParamConflictFound()
        {
            var routeParameters = new HashSet<string> {"id1", "id2"};
            var lambdaMethodParams = new List<ParameterModel>
            {
                new ParameterModel
                {
                    Attributes = new List<AttributeModel>
                    {
                        new AttributeModel<FromRouteAttribute>
                        {
                            Data = new FromRouteAttribute { Name = "identifier1"},
                            Type = new TypeModel { FullName = TypeFullNames.FromRouteAttribute}
                        }
                    },
                    Type = new TypeModel
                    {
                        FullName = "int"
                    }
                },
                new ParameterModel
                {
                    Attributes = new List<AttributeModel>
                    {
                        new AttributeModel<FromHeaderAttribute>
                        {
                            Data = new FromHeaderAttribute { Name = "identifier2"},
                            Type = new TypeModel { FullName = TypeFullNames.FromHeaderAttribute}
                        },
                    },
                    Name = "id2",
                }
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                RouteParametersValidator.Validate(routeParameters, lambdaMethodParams);
            });

            Assert.Equal($"Conflicting attribute(s) {TypeFullNames.FromHeaderAttribute} found on id2 method parameter.", exception.Message);
        }
    }
}