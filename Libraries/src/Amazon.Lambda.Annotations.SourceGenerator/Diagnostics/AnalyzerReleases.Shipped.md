; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.4.2.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0001 | AWSLambda | Error | Unhandled exception
AWSLambda0101 | AWSLambdaCSharpGenerator | Error | Multiple LambdaStartup classes not allowed
AWSLambda0102 | AWSLambdaCSharpGenerator | Error | Multiple events on Lambda function not supported
AWSLambda0103 | AWSLambdaCSharpGenerator | Info | Generated code
