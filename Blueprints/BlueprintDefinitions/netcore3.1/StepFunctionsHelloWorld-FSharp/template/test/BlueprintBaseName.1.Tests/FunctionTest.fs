namespace BlueprintBaseName._1.Tests


open Xunit
open Amazon.Lambda.TestUtilities

open BlueprintBaseName._1


module FunctionTest =
    [<Fact>]
    let ``Test Greeting Function``() =
        let context = TestLambdaContext()
        let state =
            StepFunctionTasks.Greeting
                (StepFunctionTasks.State(Name = "MyStepFunctions"))
                context

        Assert.Equal(5, state.WaitInSeconds)
        Assert.Equal("Hello MyStepFunctions", state.Message)

    [<EntryPoint>]
    let main _ = 0
