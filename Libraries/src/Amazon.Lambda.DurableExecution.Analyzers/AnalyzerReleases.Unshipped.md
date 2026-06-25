; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|----------------------------------------------------------------------
DE001   | AWSLambdaDurableExecution | Warning | Non-deterministic call outside a step. https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.DurableExecution/docs/analyzers.md#de001
DE002   | AWSLambdaDurableExecution | Warning | Nested durable operation inside a step body. https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.DurableExecution/docs/analyzers.md#de002
DE003   | AWSLambdaDurableExecution | Warning | Mutable variable captured and modified inside a durable operation. https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.DurableExecution/docs/analyzers.md#de003
DE004   | AWSLambdaDurableExecution | Info | Task.WhenAll/WhenAny over durable tasks; prefer ParallelAsync/MapAsync. https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.DurableExecution/docs/analyzers.md#de004
