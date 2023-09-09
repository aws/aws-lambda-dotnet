using System;
using Microsoft.CodeAnalysis;
using Amazon.Lambda.Annotations.APIGateway;

namespace Amazon.Lambda.Annotations.SourceGenerator.Diagnostics
{
    public static class DiagnosticDescriptors
    {
        /// Generic errors
        public static readonly DiagnosticDescriptor UnhandledException = new DiagnosticDescriptor(id: "AWSLambda0001",
            title: "Unhandled exception",
            messageFormat: "This is a bug. Please run the build with detailed verbosity (dotnet build --verbosity detailed) and file a bug at https://github.com/aws/aws-lambda-dotnet with the build output and stack trace {0}.",
            category: "AWSLambda",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "{0}.",
            helpLinkUri: "https://github.com/aws/aws-lambda-dotnet");

        /// AWSLambdaCSharpGenerator starts from 0101
        public static readonly DiagnosticDescriptor MultipleStartupNotAllowed = new DiagnosticDescriptor(id: "AWSLambda0101",
            title: "Multiple LambdaStartup classes not allowed",
            messageFormat: "Multiple LambdaStartup classes are not allowed in Lambda AWSProjectType",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultipleEventsNotSupported = new DiagnosticDescriptor(id: "AWSLambda0102",
            title: "Multiple events on Lambda function not supported",
            messageFormat: "Multiple event attributes on LambdaFunction are not supported",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CodeGeneration = new DiagnosticDescriptor(id: "AWSLambda0103",
            title: "Generated Code",
            messageFormat: $"{{0}}{Environment.NewLine}{{1}}",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingDependencies = new DiagnosticDescriptor(id: "AWSLambda0104",
            title: "Missing reference to a required dependency",
            messageFormat: "Your project has a missing required package dependency. Please add a reference to the following package: {0}.",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MainMethodExists = new DiagnosticDescriptor(id: "AWSLambda0104",
            title: "static Main method exists",
            messageFormat: "Failed to generate Main method for LambdaGenerateMainAttribute because project already contains Main method. Existing Main methods must be removed when using LambdaGenerateMainAttribute attribute.",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor HttpResultsOnNonApiFunction = new DiagnosticDescriptor(id: "AWSLambda0105",
            title: $"Invalid return type {nameof(IHttpResult)}",
            messageFormat: $"{nameof(IHttpResult)} is not a valid return type for LambdaFunctions without {nameof(HttpApiAttribute)} or {nameof(RestApiAttribute)} attributes",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly  DiagnosticDescriptor InvalidResourceName = new DiagnosticDescriptor(id: "AWSLambda0106",
            title: $"Invalid CloudFormation resource name",
            messageFormat: "The specified CloudFormation resource name is not valid. It must only contain alphanumeric characters.",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CodeGenerationFailed = new DiagnosticDescriptor(id: "AWSLambda0107",
            title: "Failed Code Generation",
            messageFormat: "{0}",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingLambdaSerializer = new DiagnosticDescriptor(id: "AWSLambda0108",
            title: "Failed Code Generation",
            messageFormat: "Assembly attribute Amazon.Lambda.Core.LambdaSerializerAttribute is missing. Add serialization package like " + 
                "Amazon.Lambda.Serialization.SystemTextJson and add the assembly attribute to register the JSON serializer for Lambda events.",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedMethodParamaterType = new DiagnosticDescriptor(id: "AWSLambda0109",
            title: "Unsupported Method Paramater Type",
            messageFormat: "Unsupported query paramter '{0}' of type '{1}' encountered. Only primitive .NET types and their corresponding enumerables can be used as query parameters.",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidParameterAttributeName = new DiagnosticDescriptor(id: "AWSLambda0110",
           title: "Invalid Parameter Attribute Name",
           messageFormat: "Invalid parameter attribute name '{0}' for method parameter '{1}' encountered. Valid values can only contain uppercase and lowercase alphanumeric characters, periods (.), hyphens (-), underscores (_) and dollar signs ($).",
           category: "AWSLambdaCSharpGenerator",
           DiagnosticSeverity.Error,
           isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor SetOutputTypeExecutable = new DiagnosticDescriptor(id: "AWSLambda0111",
            title: "Output Type is not an executable",
            messageFormat: "AssemblyAttribute Amazon.Lambda.Annotations.LambdaGlobalPropertiesAttribute is configured to generate a static main method " + 
                           "but the assembly itself is not configured to output an executable. Set the 'OutputType' property in the .csproj file to be 'exe'.",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidRuntimeSelection = new DiagnosticDescriptor(id: "AWSLambda0112",
            title: "Invalid runtime selection",
            messageFormat: "The runtime selected in the Amazon.Lambda.Annotations.LambdaGlobalPropertiesAttribute is not a supported value " + 
                           "It should be set to either 'dotnet6' or 'provided.al2'.",
            category: "AWSLambdaCSharpGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}