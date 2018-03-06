namespace TestFunctionFSharp

module FunctionFSharp =
    open Amazon.Lambda.Core

    type CheckNumber = CheckNumber of int
    type CardType = MasterCard | Visa
    type CardNumber = CardNumber of string
    type CreditCardInfo = { Type : CardType ; Number : CardNumber }

    /// PaymentMethod is cash, check or card
    [<NoComparisonAttribute>]
    type PaymentMethod = 
        /// Cash needs no extra information
        | Cash
        /// Check needs a CheckNumber
        | Check of CheckNumber 
        /// CreditCard needs a CardType and CardNumber
        | CreditCard of CreditCardInfo

    [<LambdaSerializer(typeof<FSharpJsonSerializer.FSharpJsonSerializer>)>]
    let toUpper (input : string) (_: ILambdaContext) =
        match isNull input with
        | true -> null
        | false -> input.ToUpper()

    [<LambdaSerializer(typeof<FSharpJsonSerializer.FSharpJsonSerializer>)>]
    let getPaymentMethod (input : PaymentMethod) (_ : ILambdaContext) =
        match box input with
        | null -> None
        | _ -> Some input

module Helper =
    open System.IO

    let serialize data =
        let serializer = new FSharpJsonSerializer.FSharpJsonSerializer()
        use m = new MemoryStream()
        serializer.Serialize(data, m)
        System.Text.Encoding.ASCII.GetString(m.ToArray());

module TestFunctionFSharp =
    open Xunit
    open Amazon.Lambda.TestUtilities
    open FunctionFSharp

    [<Fact>]
    let ``To upper case function should upper case input string``() =
        let context = new TestLambdaContext()
        let input = "hello world from F#"
        let actual = toUpper input context
        Assert.Equal(input.ToUpper(), actual)

    [<Fact>]
    let ``Get PaymentMethod should return Some Cash when given Cash``() =
        let context = new TestLambdaContext()
        let input = Cash
        let serializedInput = Helper.serialize input
        let actual = FunctionFSharp.getPaymentMethod input context
        Assert.Equal(Some input, actual)
        Assert.Equal("\"Cash\"", serializedInput)


    [<Fact>]
    let ``Get PaymentMethod should return Some Check when given Check``() =
        let context = new TestLambdaContext()
        let input =
            42
            |> CheckNumber 
            |> Check 
        let serializedInput = Helper.serialize input
        let actual = FunctionFSharp.getPaymentMethod input context
        Assert.Equal(Some input, actual)
        Assert.Equal("{\"Check\":{\"CheckNumber\":42}}", serializedInput)


    [<Fact>]
    let ``Get PaymentMethod should return Some CreditCard when given CreditCard``() =
        let context = new TestLambdaContext()
        let input = 
            {Type = Visa; Number = CardNumber "42-42" }
            |> CreditCard
        let serializedInput = Helper.serialize input
        let actual = FunctionFSharp.getPaymentMethod input context
        Assert.Equal(Some input, actual)
        Assert.Equal("{\"CreditCard\":{\"Type\":\"Visa\",\"Number\":{\"CardNumber\":\"42-42\"}}}", serializedInput)