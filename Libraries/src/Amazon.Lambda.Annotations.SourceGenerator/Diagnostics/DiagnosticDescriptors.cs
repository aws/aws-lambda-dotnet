using System;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Diagnostics
{
    public static class DiagnosticDescriptors
    {
        /// Generic errors
        public static readonly DiagnosticDescriptor UnhandledException = new DiagnosticDescriptor(id: "AWSLambda0001",
            title: "Unhandled exception",
            messageFormat: $"This is a bug. Please run the build with detailed verbosity (dotnet build --verbosity detailed) and file a bug at https://github.com/aws/aws-lambda-dotnet with the build output and stack trace {{0}}.",
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
    }
}