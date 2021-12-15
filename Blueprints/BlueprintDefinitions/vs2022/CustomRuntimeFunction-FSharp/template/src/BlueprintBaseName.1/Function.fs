namespace BlueprintBaseName._1

open Amazon.Lambda.Core
open Amazon.Lambda.RuntimeSupport
open Amazon.Lambda.Serialization.SystemTextJson

open System

module Function =


    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    ///
    /// To use this handler to respond to an AWS event, reference the appropriate package from 
    /// https://github.com/aws/aws-lambda-dotnet#events
    /// and change the string input parameter to the desired event type.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    let functionHandler (input: string) (_: ILambdaContext) =
        match input with
        | null -> String.Empty
        | _ -> input.ToUpper()


    [<EntryPoint>]
    let main _args =
    
        let handler = Func<string, ILambdaContext, string>(functionHandler)
        use bootstrap = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                            .Build()
   
        bootstrap.RunAsync().GetAwaiter().GetResult()
        0