namespace BlueprintBaseName._1

open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Hosting

module LocalEntryPoint =
    let exitCode = 0

    let CreateHostBuilder args =
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun webBuilder ->
                webBuilder.UseStartup<Startup>() |> ignore
            )

    [<EntryPoint>]
    let main args =
        CreateHostBuilder(args).Build().Run()
        exitCode
