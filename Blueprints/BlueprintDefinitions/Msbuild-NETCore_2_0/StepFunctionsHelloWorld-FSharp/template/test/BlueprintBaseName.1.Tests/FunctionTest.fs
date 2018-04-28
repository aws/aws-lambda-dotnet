namespace BlueprintBaseName._1.Tests

open Xunit
open Amazon.Lambda.Core
open Amazon.Lambda.TestUtilities

open BlueprintBaseName._1

module FunctionTest =    

    [<Fact>]
    let ``Test Greeting Function``() =
        let context = new TestLambdaContext()
    
        let mutable state = new StepFunctionTasks.State(Name = "MyStepFunctions")

        state <- StepFunctionTasks.Greeting state context

        Assert.Equal(5, state.WaitInSeconds)
        Assert.Equal("Hello MyStepFunctions", state.Message)
    
    [<EntryPoint>]
    let main argv = 0