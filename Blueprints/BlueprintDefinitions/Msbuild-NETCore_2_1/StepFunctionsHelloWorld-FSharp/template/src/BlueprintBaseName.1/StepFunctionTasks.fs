namespace BlueprintBaseName._1

open System

open Amazon.Lambda.Core

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

module StepFunctionTasks =

    type State() =        

        member val Name = null with get, set
        member val Message = null with get, set
        member val WaitInSeconds = 0 with get, set

    let Greeting (state : State) (context: ILambdaContext) =



        if not(String.IsNullOrEmpty(state.Name)) then 
            state.Message <- sprintf "Hello %s" state.Name
        else
            state.Message <- "Hello"

        context.Logger.LogLine(sprintf "Process Greeting message to be: \"%s\"" state.Message)
        state.WaitInSeconds <- 5
        state

    let Salutations (state : State) (context: ILambdaContext) =
        
        if not(String.IsNullOrEmpty(state.Name)) then 
            state.Message <- sprintf "%s, Goodbye %s" state.Message state.Name
        else
            state.Message <- sprintf "%s, Goodbye" state.Message
            
        context.Logger.LogLine(sprintf "Process Salutations message to be: \"%s\"" state.Message)
        state


