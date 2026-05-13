namespace BlueprintBaseName._1


open Amazon.Lambda.Core

open System


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()


module StepFunctionTasks =
    type State() =
        member val Name = null with get, set
        member val Message = null with get, set
        member val WaitInSeconds = 0 with get, set

    let Greeting (state: State) (context: ILambdaContext) =
        state.Message <-
            if not (String.IsNullOrEmpty state.Name) then
                sprintf "Hello %s" state.Name
            else "Hello"

        sprintf "Process Greeting message to be: \"%s\"" state.Message
        |> context.Logger.LogInformation

        state.WaitInSeconds <- 5
        state

    let Salutations (state: State) (context: ILambdaContext) =
        state.Message <-
            if not (String.IsNullOrEmpty state.Name) then
                sprintf "%s, Goodbye %s" state.Message state.Name
            else sprintf "%s, Goodbye" state.Message

        sprintf "Process Salutations message to be: \"%s\"" state.Message
        |> context.Logger.LogInformation

        state
