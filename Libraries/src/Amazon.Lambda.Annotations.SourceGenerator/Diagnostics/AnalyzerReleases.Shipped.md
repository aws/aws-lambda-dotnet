; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.7.3
### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0119 | AWSLambdaCSharpGenerator | Error | Conflicting Service Configuration Methods Detected

## Release 1.5.1
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0118 | AWSLambdaCSharpGenerator | Error | Maximum Handler Length Exceeded

## Release 1.5.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0115 | AWSLambdaCSharpGenerator | Error | Invalid Usage of API Parameters
AWSLambda0116 | AWSLambdaCSharpGenerator | Error | Invalid SQSEventAttribute encountered
AWSLambda0117 | AWSLambdaCSharpGenerator | Error | Invalid Lambda Method Signature

## Release 1.1.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0111 | AWSLambdaCSharpGenerator | Error | If the GenerateMain global property is set to true but the project OutputType is not set to 'exe'
AWSLambda0112 | AWSLambdaCSharpGenerator | Error | An invalid runtime is selected in the LambdaGlobalProperties attribute
AWSLambda0113 | AWSLambdaCSharpGenerator | Error | The GenerateMain global property is set to true and the OutputType is set to 'exe', but no Lambda Function attributes are used
AWSLambda0114 | AWSLambdaCSharpGenerator | Error | The GenerateMain global property is set to true, but the project already contains a static Main method

## Release 1.0.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0110 | AWSLambdaCSharpGenerator | Error | Invalid Parameter Attribute Name

## Release 0.13.4.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0109 | AWSLambdaCSharpGenerator | Error | Unsupported Method Paramater Type

## Release 0.13.3.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0108 | AWSLambdaCSharpGenerator | Error | Assembly attribute Amazon.Lambda.Core.LambdaSerializerAttribute is missing

## Release 0.13.1.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0107 | AWSLambdaCSharpGenerator | Error | Unsupported error thrown during code generation

## Release 0.13.0.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0106 | AWSLambdaCSharpGenerator | Error | Invalid CloudFormation resource name


## Release 0.11.0.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0104 | AWSLambdaCSharpGenerator | Error | Missing reference to a required dependency
AWSLambda0105 | AWSLambdaCSharpGenerator | Error | Invalid return type IHttpResult

## Release 0.4.2.0
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0001 | AWSLambda | Error | Unhandled exception
AWSLambda0101 | AWSLambdaCSharpGenerator | Error | Multiple LambdaStartup classes not allowed
AWSLambda0102 | AWSLambdaCSharpGenerator | Error | Multiple events on Lambda function not supported
AWSLambda0103 | AWSLambdaCSharpGenerator | Info | Generated code