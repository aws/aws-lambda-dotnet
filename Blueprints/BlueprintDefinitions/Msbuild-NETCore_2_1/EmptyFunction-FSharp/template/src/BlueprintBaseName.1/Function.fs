namespace BlueprintBaseName._1

open Amazon.Lambda.Core

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()


type Function() =
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member _this.FunctionHandler (input: string) (_: ILambdaContext) =
        match input with
        | null -> ""
        | v -> v.ToUpper()
