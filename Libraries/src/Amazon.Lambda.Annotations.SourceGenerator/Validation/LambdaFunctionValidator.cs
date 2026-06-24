// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.ALB;
using Amazon.Lambda.Annotations.S3;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.DynamoDB;
using Amazon.Lambda.Annotations.SNS;
using Amazon.Lambda.Annotations.Schedule;
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
            ValidateDynamoDBEvents(lambdaFunctionModel, methodLocation, diagnostics);
            ValidateSnsEvents(lambdaFunctionModel, methodLocation, diagnostics);
            ValidateScheduleEvents(lambdaFunctionModel, methodLocation, diagnostics);
            ValidateAlbEvents(lambdaFunctionModel, methodLocation, diagnostics);
            ValidateS3Events(lambdaFunctionModel, methodLocation, diagnostics);
            ValidateDurableExecution(context, lambdaMethodSymbol, lambdaFunctionModel, methodLocation, diagnostics);

            return ReportDiagnostics(diagnosticReporter, diagnostics);
        }

        private static void ValidateDurableExecution(GeneratorExecutionContext context, IMethodSymbol lambdaMethodSymbol, LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            if (!lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.DurableExecution))
            {
                return;
            }

            // Durable functions support BOTH programming models on the managed runtime:
            //  - executable: the generated wrapper delegates to DurableFunction.WrapAsync, which reads the
            //    serializer off the ILambdaContext populated by the bootstrap loop the executable hosts.
            //  - class library: the managed runtime hosts its own bootstrap, resolves [assembly: LambdaSerializer],
            //    and populates ILambdaContext.Serializer the same way, so WrapAsync finds the serializer there too.
            // Either way the wrapper is identical; only the deployed Handler string differs (assembly name vs.
            // Assembly::Type::Method), which LambdaFunctionModel.Handler already derives from IsExecutable. So no
            // OutputKind gate is needed here.

            // Validate the attribute's property values (RetentionPeriodInDays / ExecutionTimeout bounds) so a
            // misconfiguration surfaces as a build-time diagnostic instead of a deploy-time service rejection,
            // matching how every other event attribute calls its own Validate() above.
            foreach (var att in lambdaFunctionModel.Attributes)
            {
                if (att.Type.FullName != TypeFullNames.DurableExecutionAttribute)
                    continue;

                var durableExecutionAttribute = ((AttributeModel<DurableExecutionAttribute>)att).Data;
                var validationErrors = durableExecutionAttribute.Validate();
                validationErrors.ForEach(errorMessage => diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidDurableExecutionAttribute, methodLocation, errorMessage)));
            }

            // The generated wrapper hands the user method group to DurableFunction.WrapAsync, whose overloads
            // accept Func<TInput, IDurableContext, Task> or Func<TInput, IDurableContext, Task<TOutput>>. A
            // mismatched signature would produce a C# error in the generated code, so reject it up front with
            // a diagnostic pointing at the user's method instead.
            ValidateDurableExecutionSignature(lambdaMethodSymbol, lambdaFunctionModel, methodLocation, diagnostics);

            // When the user supplies an explicit Role, the generator does not manage the function's Policies,
            // so it cannot inject the checkpoint policy. Inform the user to attach the actions themselves.
            if (!string.IsNullOrEmpty(lambdaFunctionModel.Role))
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.DurableExecutionExplicitRoleNeedsCheckpointPolicy, methodLocation));
            }
        }

        private static void ValidateDurableExecutionSignature(IMethodSymbol lambdaMethodSymbol, LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            void AddSignatureError(string detail) =>
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.DurableExecutionInvalidSignature, methodLocation, detail));

            // Must be exactly (TInput, IDurableContext).
            if (lambdaMethodSymbol.Parameters.Length != 2)
            {
                AddSignatureError($"The method has {lambdaMethodSymbol.Parameters.Length} parameter(s).");
            }
            else if (lambdaMethodSymbol.Parameters[1].Type.ToDisplayString() != TypeFullNames.IDurableContext)
            {
                AddSignatureError($"The second parameter must be '{TypeFullNames.IDurableContext}'.");
            }

            // Must return Task or Task<TOutput>. ValueTask, void, or a bare value are not accepted by WrapAsync.
            // Reuse the model's return-type classification (computed with SymbolEqualityComparer in the builder).
            if (!lambdaFunctionModel.LambdaMethod.ReturnsVoidOrGenericTask)
            {
                AddSignatureError($"The return type must be Task or Task<TOutput> but was '{lambdaMethodSymbol.ReturnType.ToDisplayString()}'.");
            }
        }

        internal static bool ValidateDependencies(GeneratorExecutionContext context, IMethodSymbol lambdaMethodSymbol, Location methodLocation, DiagnosticReporter diagnosticReporter)
        {
            // Check for references to "Amazon.Lambda.APIGatewayEvents" if the Lambda method is annotated with RestApi, HttpApi, or authorizer attributes.
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAttribute) || lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAttribute)
                || lambdaMethodSymbol.HasAttribute(context, TypeFullNames.FunctionUrlAttribute)
                || lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAuthorizerAttribute) || lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAuthorizerAttribute))
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

            // Check for references to "Amazon.Lambda.ApplicationLoadBalancerEvents" if the Lambda method is annotated with ALBApi attribute.
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.ALBApiAttribute))
            {
                if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.ApplicationLoadBalancerEvents") == null)
                {
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MissingDependencies, methodLocation, "Amazon.Lambda.ApplicationLoadBalancerEvents"));
                    return false;
                }
            }

            // Check for references to "Amazon.Lambda.S3Events" if the Lambda method is annotated with S3Event attribute.
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.S3EventAttribute))
            {
                if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.S3Events") == null)
                {
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MissingDependencies, methodLocation, "Amazon.Lambda.S3Events"));
                    return false;
                }
            }

            // Check for references to "Amazon.Lambda.DynamoDBEvents" if the Lambda method is annotated with DynamoDBEvent attribute.
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.DynamoDBEventAttribute))
            {
                if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.DynamoDBEvents") == null)
                {
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MissingDependencies, methodLocation, "Amazon.Lambda.DynamoDBEvents"));
                    return false;
                }
            }

            // Check for references to "Amazon.Lambda.SNSEvents" if the Lambda method is annotated with SNSEvent attribute.
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.SNSEventAttribute))
            {
                if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.SNSEvents") == null)
                {
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MissingDependencies, methodLocation, "Amazon.Lambda.SNSEvents"));
                    return false;
                }
            }

            // Check for references to "Amazon.Lambda.CloudWatchEvents" if the Lambda method is annotated with ScheduleEvent attribute.
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.ScheduleEventAttribute))
            {
                if (context.Compilation.ReferencedAssemblyNames.FirstOrDefault(x => x.Name == "Amazon.Lambda.CloudWatchEvents") == null)
                {
                    diagnosticReporter.Report(Diagnostic.Create(DiagnosticDescriptors.MissingDependencies, methodLocation, "Amazon.Lambda.CloudWatchEvents"));
                    return false;
                }
            }

            return true;
        }

        private static void ValidateApiGatewayEvents(LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            var isApiEvent = lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.API);
            var isAuthorizerEvent = lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.Authorizer);

            // IHttpResult is only valid for API Gateway events (not Authorizer events)
            if (!isApiEvent && lambdaFunctionModel.LambdaMethod.ReturnsIHttpResults)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.HttpResultsOnNonApiFunction, methodLocation));
            }

            // IAuthorizerResult is only valid for Authorizer events
            if (!isAuthorizerEvent && lambdaFunctionModel.LambdaMethod.ReturnsIAuthorizerResult)
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.AuthorizerResultOnNonAuthorizerFunction, methodLocation));
            }

            // If the method does not contain any API, Authorizer, or ALB events, then it cannot have
            // parameters that are annotated with HTTP API attributes.
            // Authorizer functions also support FromHeader, FromQuery, FromRoute attributes.
            // ALB functions also support FromHeader, FromQuery, FromBody attributes.
            var isAlbEvent = lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.ALB);
            if (!isApiEvent && !isAuthorizerEvent && !isAlbEvent)
            {
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

            // Authorizer-specific parameter validation
            if (isAuthorizerEvent)
            {
                foreach (var parameter in lambdaFunctionModel.LambdaMethod.Parameters)
                {
                    // [FromBody] is not supported on authorizer functions
                    if (parameter.Attributes.Any(att => att.Type.FullName == TypeFullNames.FromBodyAttribute))
                    {
                        diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.FromBodyNotSupportedOnAuthorizer, methodLocation));
                    }

                    // Validate [FromQuery] parameter types - only primitive types allowed
                    if (parameter.Attributes.Any(att => att.Type.FullName == TypeFullNames.FromQueryAttribute))
                    {
                        if (!parameter.Type.IsPrimitiveType() && !parameter.Type.IsPrimitiveEnumerableType())
                        {
                            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedMethodParameterType, methodLocation, parameter.Name, parameter.Type.FullName));
                        }
                    }

                    // Validate attribute names for FromQuery, FromRoute, and FromHeader
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

        private static void ValidateAlbEvents(LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            // If the method does not contain any ALB events, then simply return early
            if (!lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.ALB))
            {
                return;
            }

            // Validate ALBApiAttributes
            foreach (var att in lambdaFunctionModel.Attributes)
            {
                if (att.Type.FullName != TypeFullNames.ALBApiAttribute)
                    continue;

                var albApiAttribute = ((AttributeModel<ALBApiAttribute>)att).Data;
                var validationErrors = albApiAttribute.Validate();
                validationErrors.ForEach(errorMessage => diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidAlbApiAttribute, methodLocation, errorMessage)));
            }

            // Validate method parameters
            var parameters = lambdaFunctionModel.LambdaMethod.Parameters;
            foreach (var parameter in parameters)
            {
                // [FromRoute] is not supported on ALB functions
                if (parameter.Attributes.Any(att => att.Type.FullName == TypeFullNames.FromRouteAttribute))
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.FromRouteNotSupportedOnAlb, methodLocation));
                }

                // Validate [FromQuery] parameter types - only primitive types allowed
                if (parameter.Attributes.Any(att => att.Type.FullName == TypeFullNames.ALBFromQueryAttribute))
                {
                    if (!parameter.Type.IsPrimitiveType() && !parameter.Type.IsPrimitiveEnumerableType())
                    {
                        diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedMethodParameterType, methodLocation, parameter.Name, parameter.Type.FullName));
                    }
                }

                // Validate attribute names for FromQuery and FromHeader
                foreach (var att in parameter.Attributes)
                {
                    var parameterAttributeName = string.Empty;
                    switch (att.Type.FullName)
                    {
                        case TypeFullNames.ALBFromQueryAttribute:
                            if (att is AttributeModel<ALB.FromQueryAttribute> albFromQueryAttribute)
                                parameterAttributeName = albFromQueryAttribute.Data.Name;
                            break;

                        case TypeFullNames.ALBFromHeaderAttribute:
                            if (att is AttributeModel<ALB.FromHeaderAttribute> albFromHeaderAttribute)
                                parameterAttributeName = albFromHeaderAttribute.Data.Name;
                            break;

                        default:
                            break;
                    }

                    if (!string.IsNullOrEmpty(parameterAttributeName) && !_parameterAttributeNameRegex.IsMatch(parameterAttributeName))
                    {
                        diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidParameterAttributeName, methodLocation, parameterAttributeName, parameter.Name));
                    }
                }

                // Validate that every parameter has a recognized binding
                // Allowed: ILambdaContext, ApplicationLoadBalancerRequest, [FromServices], [FromQuery], [FromHeader], [FromBody]
                if (parameter.Type.FullName != TypeFullNames.ILambdaContext &&
                    !TypeFullNames.ALBRequests.Contains(parameter.Type.FullName) &&
                    !parameter.Attributes.Any(att =>
                        att.Type.FullName == TypeFullNames.FromServiceAttribute ||
                        att.Type.FullName == TypeFullNames.ALBFromQueryAttribute ||
                        att.Type.FullName == TypeFullNames.ALBFromHeaderAttribute ||
                        att.Type.FullName == TypeFullNames.ALBFromBodyAttribute ||
                        att.Type.FullName == TypeFullNames.FromRouteAttribute)) // FromRoute already has its own error
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.AlbUnmappedParameter, methodLocation, parameter.Name));
                }
            }
        }

        private static void ValidateS3Events(LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            if (!lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.S3))
                return;

            // Validate S3EventAttributes
            var seenResourceNames = new HashSet<string>();
            foreach (var att in lambdaFunctionModel.Attributes)
            {
                if (att.Type.FullName != TypeFullNames.S3EventAttribute)
                    continue;

                var s3EventAttribute = ((AttributeModel<S3EventAttribute>)att).Data;
                var validationErrors = s3EventAttribute.Validate();
                validationErrors.ForEach(errorMessage => diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidS3EventAttribute, methodLocation, errorMessage)));

                // Check for duplicate resource names (only when ResourceName is safe to evaluate)
                var derivedResourceName = s3EventAttribute.ResourceName;
                if (!string.IsNullOrEmpty(derivedResourceName) && !seenResourceNames.Add(derivedResourceName))
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidS3EventAttribute, methodLocation,
                        $"Duplicate S3 event resource name '{derivedResourceName}'. Each [S3Event] attribute on the same method must have a unique ResourceName."));
                }
            }

            // Validate method parameters - first param must be S3Event, optional second param ILambdaContext
            var parameters = lambdaFunctionModel.LambdaMethod.Parameters;
            if (parameters.Count == 0 ||
                parameters.Count > 2 ||
                (parameters.Count == 1 && parameters[0].Type.FullName != TypeFullNames.S3Event) ||
                (parameters.Count == 2 && (parameters[0].Type.FullName != TypeFullNames.S3Event || parameters[1].Type.FullName != TypeFullNames.ILambdaContext)))
            {
                var errorMessage = $"When using the {nameof(S3EventAttribute)}, the Lambda method can accept at most 2 parameters. " +
                    $"The first parameter is required and must be of type {TypeFullNames.S3Event}. " +
                    $"The second parameter is optional and must be of type {TypeFullNames.ILambdaContext}.";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }

            // Validate method return type - must be void or Task
            if (!lambdaFunctionModel.LambdaMethod.ReturnsVoid && !lambdaFunctionModel.LambdaMethod.ReturnsVoidTask)
            {
                var errorMessage = $"When using the {nameof(S3EventAttribute)}, the Lambda method can return either void or {TypeFullNames.Task}";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }
        }

        private static void ValidateDynamoDBEvents(LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            if (!lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.DynamoDB))
            {
                return;
            }

            // Validate DynamoDBEventAttributes
            foreach (var att in lambdaFunctionModel.Attributes)
            {
                if (att.Type.FullName != TypeFullNames.DynamoDBEventAttribute)
                    continue;

                var dynamoDBEventAttribute = ((AttributeModel<DynamoDBEventAttribute>)att).Data;
                var validationErrors = dynamoDBEventAttribute.Validate();
                validationErrors.ForEach(errorMessage => diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidDynamoDBEventAttribute, methodLocation, errorMessage)));
            }

            // Validate method parameters - When using DynamoDBEventAttribute, the method signature must be (DynamoDBEvent evnt) or (DynamoDBEvent evnt, ILambdaContext context)
            var parameters = lambdaFunctionModel.LambdaMethod.Parameters;
            if (parameters.Count == 0 ||
                parameters.Count > 2 ||
                (parameters.Count == 1 && parameters[0].Type.FullName != TypeFullNames.DynamoDBEvent) ||
                (parameters.Count == 2 && (parameters[0].Type.FullName != TypeFullNames.DynamoDBEvent || parameters[1].Type.FullName != TypeFullNames.ILambdaContext)))
            {
                var errorMessage = $"When using the {nameof(DynamoDBEventAttribute)}, the Lambda method can accept at most 2 parameters. " +
                    $"The first parameter is required and must be of type {TypeFullNames.DynamoDBEvent}. " +
                    $"The second parameter is optional and must be of type {TypeFullNames.ILambdaContext}.";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }

            // Validate method return type - When using DynamoDBEventAttribute, the return type must be either void or Task
            if (!lambdaFunctionModel.LambdaMethod.ReturnsVoid && !lambdaFunctionModel.LambdaMethod.ReturnsVoidTask)
            {
                var errorMessage = $"When using the {nameof(DynamoDBEventAttribute)}, the Lambda method can return either void or {TypeFullNames.Task}";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }
        }

        private static void ValidateSnsEvents(LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            if (!lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.SNS))
            {
                return;
            }

            // Validate SNSEventAttributes
            foreach (var att in lambdaFunctionModel.Attributes)
            {
                if (att.Type.FullName != TypeFullNames.SNSEventAttribute)
                    continue;

                var snsEventAttribute = ((AttributeModel<SNSEventAttribute>)att).Data;
                var validationErrors = snsEventAttribute.Validate();
                validationErrors.ForEach(errorMessage => diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidSnsEventAttribute, methodLocation, errorMessage)));
            }

            // Validate method parameters - When using SNSEventAttribute, the method signature must be (SNSEvent snsEvent) or (SNSEvent snsEvent, ILambdaContext context)
            var parameters = lambdaFunctionModel.LambdaMethod.Parameters;
            if (parameters.Count == 0 ||
                parameters.Count > 2 ||
                (parameters.Count == 1 && parameters[0].Type.FullName != TypeFullNames.SNSEvent) ||
                (parameters.Count == 2 && (parameters[0].Type.FullName != TypeFullNames.SNSEvent || parameters[1].Type.FullName != TypeFullNames.ILambdaContext)))
            {
                var errorMessage = $"When using the {nameof(SNSEventAttribute)}, the Lambda method can accept at most 2 parameters. " +
                    $"The first parameter is required and must be of type {TypeFullNames.SNSEvent}. " +
                    $"The second parameter is optional and must be of type {TypeFullNames.ILambdaContext}.";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }

            // Validate method return type - When using SNSEventAttribute, the return type must be either void or Task
            if (!lambdaFunctionModel.LambdaMethod.ReturnsVoid && !lambdaFunctionModel.LambdaMethod.ReturnsVoidTask)
            {
                var errorMessage = $"When using the {nameof(SNSEventAttribute)}, the Lambda method can return either void or {TypeFullNames.Task}";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }
        }

        private static void ValidateScheduleEvents(LambdaFunctionModel lambdaFunctionModel, Location methodLocation, List<Diagnostic> diagnostics)
        {
            if (!lambdaFunctionModel.LambdaMethod.Events.Contains(EventType.Schedule))
            {
                return;
            }

            foreach (var att in lambdaFunctionModel.Attributes)
            {
                if (att.Type.FullName != TypeFullNames.ScheduleEventAttribute)
                    continue;

                var scheduleEventAttribute = ((AttributeModel<ScheduleEventAttribute>)att).Data;
                var validationErrors = scheduleEventAttribute.Validate();
                validationErrors.ForEach(errorMessage => diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidScheduleEventAttribute, methodLocation, errorMessage)));
            }

            // Validate method parameters - When using ScheduleEventAttribute, the method signature must be (ScheduledEvent evnt) or (ScheduledEvent evnt, ILambdaContext context)
            var parameters = lambdaFunctionModel.LambdaMethod.Parameters;
            if (parameters.Count == 0 ||
                parameters.Count > 2 ||
                (parameters.Count == 1 && parameters[0].Type.FullName != TypeFullNames.ScheduledEvent) ||
                (parameters.Count == 2 && (parameters[0].Type.FullName != TypeFullNames.ScheduledEvent || parameters[1].Type.FullName != TypeFullNames.ILambdaContext)))
            {
                var errorMessage = $"When using the {nameof(ScheduleEventAttribute)}, the Lambda method can accept at most 2 parameters. " +
                    $"The first parameter is required and must be of type {TypeFullNames.ScheduledEvent}. " +
                    $"The second parameter is optional and must be of type {TypeFullNames.ILambdaContext}.";
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidLambdaMethodSignature, methodLocation, errorMessage));
            }

            // Validate return type - must be void or Task
            if (!lambdaFunctionModel.LambdaMethod.ReturnsVoid && !lambdaFunctionModel.LambdaMethod.ReturnsVoidTask)
            {
                var errorMessage = $"When using the {nameof(ScheduleEventAttribute)}, the Lambda method can return either void or {TypeFullNames.Task}";
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
