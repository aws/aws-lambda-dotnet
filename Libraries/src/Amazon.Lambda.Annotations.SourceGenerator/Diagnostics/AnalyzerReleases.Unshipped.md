; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AWSLambda0111|AWSLambdaCSharpGenerator|Error|If the GenerateMain global property is set to true but the project OutputType is not set to 'exe'
AWSLambda0112|AWSLambdaCSharpGenerator|Error|An invalid runtime is selected in the LambdaGlobalProperties attribute
AWSLambda0113|AWSLambdaCSharpGenerator|Error|The GenerateMain global property is set to true and the OutputType is set to 'exe', but no Lambda Function attributes are used
AWSLambda0114|AWSLambdaCSharpGenerator|Error|The GenerateMain global property is set to true, but the project already contains a static Main method