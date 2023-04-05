; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.13.1.0
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0107 | AWSLambdaCSharpGenerator | Error | Unsupported error thrown during code generation

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0106 | AWSLambdaCSharpGenerator | Error | Invalid CloudFormation resource name

## Release 0.13.0.0

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0106 | AWSLambdaCSharpGenerator | Error | Invalid CloudFormation resource name


## Release 0.11.0.0

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