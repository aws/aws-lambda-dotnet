namespace BlueprintBaseName._1


open Amazon.Lambda.Core

open System


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()


type Function() =
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.FunctionHandler (input: string) (_: ILambdaContext) =
        match input with
        | null -> String.Empty
        | _ -> input.ToUpper()
