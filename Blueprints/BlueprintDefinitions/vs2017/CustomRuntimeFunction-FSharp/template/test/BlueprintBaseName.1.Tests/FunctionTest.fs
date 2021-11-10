namespace BlueprintBaseName._1.Tests


open Xunit
open Amazon.Lambda.TestUtilities
open BlueprintBaseName._1


module FunctionTest =

    [<Fact>]
    let ``Invoke ToUpper Lambda Function``() =
        // Invoke the lambda function and confirm the string was upper cased.
        let context = TestLambdaContext()
        let upperCase = Function.functionHandler "hello world" context

        Assert.Equal("HELLO WORLD", upperCase)

    [<EntryPoint>]
    let main _ = 0
