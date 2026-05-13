namespace BlueprintBaseName._1.Controllers


open Microsoft.AspNetCore.Mvc


[<Route("api/[controller]")>]
type ValuesController() =
    inherit ControllerBase()

    [<HttpGet>]
    member this.Get() =
        [|"value1"; "value2"|]

    [<HttpGet("{id}")>]
    member this.Get(id: int) =
        "value"

    [<HttpPost>]
    member this.Post([<FromBody>] value: string) =
        ()

    [<HttpPut("{id}")>]
    member this.Put(id: int, [<FromBody>] value: string) =
        ()

    [<HttpDelete("{id}")>]
    member this.Delete(id: int) =
        ()
