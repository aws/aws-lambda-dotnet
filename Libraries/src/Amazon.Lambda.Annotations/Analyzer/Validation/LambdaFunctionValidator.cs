using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SQS;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Amazon.Lambda.Annotations.SourceGenerator.Validation
{
    internal static class LambdaFunctionValidator
    {
        // Only allow alphanumeric characters
        private static readonly Regex _resourceNameRegex = new Regex("^[a-zA-Z0-9]+$");

        // Regex for the 'Name' property for API Gateway attributes - https://docs.aws.amazon.com/apigateway/latest/developerguide/request-response-data-mappings.html
        private static readonly Regex _parameterAttributeNameRegex = new Regex("^[a-zA-Z0-9._$-]+$");

        internal static bool ValidateFunction(GeneratorExecutionContext context, IMethodSymbol lambdaMethodSymbol, Location methodLocation, LambdaFunctionModel lambdaFunctionModel, DiagnosticReporter diagnosticReporter)
        {
            var diagnostics = new List<Diagnostic>();

            // Validate the resource name
            if (!_resourceNameRegex.IsMatch(lambdaFunctionModel.ResourceName))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidResourceName, methodLocation));
            }

            // Check the handler length does not exceed 127 characters when the package type is set to zip
            // The official AWS docs state a 128 character limit on the Lambda handler. However, there is an open issue where the last character is stripped off
            // when the handler is exactly 128 characters long. Hence, we are enforcing a 127 character limit.
            // https://github.com/aws/aws-lambda-dotnet/issues/1642
            if (lambdaFunctionModel.PackageType == LambdaPackageType.Zip && lambdaFunctionModel.Handler.Length > 127)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.MaximumHandlerLengthExceeded, methodLocation, lambdaFunctionModel.Handler));
            }

            // Check for Serializer attribute
            if (!lambdaMethodSymbol.ContainingAssembly.HasAttribute(context, TypeFullNames.LambdaSerializerAttribute))
            {
                if (!lambdaMethodSymbol.HasAttribute(context, TypeFullNames.LambdaSerializerAttribute))
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.MissingLambdaSerializer, methodLocation));
                }
            }

            // Check for multiple event types
            if (lambdaFunctionModel.LambdaMethod.Events.Count > 1)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.MultipleEventsNotSupported, methodLocation));

                // If multiple event types are encountered then return early without validating each individual event
                // since at this point we do not know which event type does the user want to preserve
                return ReportDiagnostics(diagnosticReporter, diagnostics);
            }

            // Validate Events
            ValidateApiGatewayEvents(lambdaFunctionModel, methodLocation, diagnostics);
            ValidateSqsEvents(lambdaFunctionModel, methodLocation, diagnostics);

            return ReportDiagnostics(diagnosticReporter, diagnostics);
        }

        internal static bool ValidateDependencies(GeneratorExecutionContext context, IMethodSymbol lambdaMethodSymbol, Location methodLocation, DiagnosticReporter diagnosticReporter)
        {
            // Check for references to "Amazon.Lambda.APIGatewayEvents" if the Lambda method is annotated with RestApi or HttpApi attributes.
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAttribute) || lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAttribute))
            {
                if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.APIGatewayEvents") == null)
                {
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MissingDependencies, methodLocation, "Amazon.Lambda.APIGatewayEvents"));
                    return false;
                }
            }

            // Check for references to "Amazon.Lambda.SQSEvents" if the Lambda method is annotated with SQSEvent attribute.
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.SQSEventAttribute))
            {
                if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.SQSEvents") == null)
                {
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MissingDependencies, methodLocation, "Amazon.Lambda.SQSEvents"));
                    return false;
                }
            }

            return true;
        }

        private static void ValidateApiGatewayEvents(LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            // If the method does not contain any APIGatewayEvents, then it cannot return IHttpResults and can also not have parameters that are annotated with HTTP API attributes
            if (!lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.API))
            {
                if (lambdaFunctionModel.LambdaMethod.ReturnsIHttpResults)
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.HttpResultsOnNonApiFunction, methodLocation));
                }

                foreach (var parameter in lambdaFunctionModel.LambdaMethod.Parameters)
                {
                    if (parameter.Attributes.Any(att =>
                        att.Type.FullName == TypeFullNames.FromBodyAttribute ||
                        att.Type.FullName == TypeFullNames.FromHeaderAttribute ||
                        att.Type.FullName == TypeFullNames.FromRouteAttribute ||
                        att.Type.FullName == TypeFullNames.FromQueryAttribute))
                    {
                        diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.ApiParametersOnNonApiFunction, methodLocation));
                    }
                }

                return;
            }

            // Validate FromRoute, FromQuery and FromHeader parameters
            foreach (var parameter in lambdaFunctionModel.LambdaMethod.Parameters)
            {
                if (parameter.Attributes.Any(att => att.Type.FullName == TypeFullNames.FromQueryAttribute))
                {
                    var fromQueryAttribute = parameter.Attributes.First(att => att.Type.FullName == TypeFullNames.FromQueryAttribute) as AttributeModel<APIGateway.FromQueryAttribute>;
                    // Use parameter name as key, if Name has not specified explicitly in the attribute definition.
                    var parameterKey = fromQueryAttribute?.Data?.Name ?? parameter.Name;

                    if (!parameter.Type.IsPrimitiveType() && !parameter.Type.IsPrimitiveEnumerableType())
                    {
                        diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedMethodParameterType, methodLocation, parameter.Name, parameter.Type.FullName));
                    }
                }

                foreach (var att in parameter.Attributes)
                {
                    var parameterAttributeName = string.Empty;
                    switch (att.Type.FullName)
                    {
                        case TypeFullNames.FromQueryAttribute:
                            var fromQueryAttribute = (AttributeModel<APIGateway.FromQueryAttribute>)att;
                            parameterAttributeName = fromQueryAttribute.Data.Name;
                            break;

                        case TypeFullNames.FromRouteAttribute:
                            var fromRouteAttribute = (AttributeModel<APIGateway.FromRouteAttribute>)att;
                            parameterAttributeName = fromRouteAttribute.Data.Name;
                            break;

                        case TypeFullNames.FromHeaderAttribute:
                            var fromHeaderAttribute = (AttributeModel<APIGateway.FromHeaderAttribute>)att;
                            parameterAttributeName = fromHeaderAttribute.Data.Name;
                            break;

                        default:
                            break;
                    }

                    if (!string.IsNullOrEmpty(parameterAttributeName) && !_parameterAttributeNameRegex.IsMatch(parameterAttributeName))
                    {
                        diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidParameterAttributeName, methodLocation, parameterAttributeName, parameter.Name));
                    }
                }
            }
        }

        private static void ValidateSqsEvents(LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            // If the method does not contain any SQS events, then simply return early
            if (!lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.SQS))
            {
                return;
            }

            // Validate SQSEventAttributes
            foreach (var att in lambdaFunctionModel.Attributes)
            {
                if (att.Type.FullName != TypeFullNames.SQSEventAttribute)
                    continue;

                var sqsEventAttribute = ((AttributeModel<SQSEventAttribute>)att).Data;
                var validationErrors = sqsEventAttribute.Validate();
                validationErrors.ForEach(errorMessage => diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidSqsEventAttribute, methodLocation, errorMessage)));
            }

            // Validate method parameters - When using SQSEventAttribute, the method signature must be (SQSEvent sqsEvent) or (SQSEvent sqsEvent, ILambdaContext context)
            var parameters = lambdaFunctionModel.LambdaMethod.Parameters;
            if (parameters.Count == 0 ||
                parameters.Count > 2 ||
                (parameters.Count == 1 && parameters[0].Type.FullName != TypeFullNames.SQSEvent) ||
                (parameters.Count == 2 && (parameters[0].Type.FullName != TypeFullNames.SQSEvent || parameters[1].Type.FullName != TypeFullNames.ILambdaContext)))
            {
                var errorMessage = $"When using the {nameof(SQSEventAttribute)}, the Lambda method can accept at most 2 parameters. " +
                    $"The first parameter is required and must be of type {TypeFullNames.SQSEvent}. " +
                    $"The second parameter is optional and must be of type {TypeFullNames.ILambdaContext}.";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }

            // Validate method return type - When using SQSEventAttribute, the return type must be either void, Task, SQSBatchResponse or Task<SQSBatchResponse>
            if (!lambdaFunctionModel.LambdaMethod.ReturnsVoidTaskOrSqsBatchResponse)
            {
                var errorMessage = $"When using the {nameof(SQSEventAttribute)}, the Lambda method can return either void, {TypeFullNames.Task}, {TypeFullNames.SQSBatchResponse} or Task<{TypeFullNames.SQSBatchResponse}>";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }
        }

        private static bool ReportDiagnostics(DiagnosticReporter diagnosticReporter, List<Diagnostic> diagnostics)
        {
            var isValid = true;
            foreach (var diagnostic in diagnostics)
            {
                diagnosticReporter.Report(diagnostic);
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    isValid = false;
                }
            }
            return isValid;
        }
    }
}
