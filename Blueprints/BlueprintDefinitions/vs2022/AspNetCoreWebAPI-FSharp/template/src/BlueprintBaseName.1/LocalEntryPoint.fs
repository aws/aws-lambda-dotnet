namespace BlueprintBaseName._1


open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting


module LocalEntryPoint =
    let exitCode = 0

    let CreateWebHostBuilder args =
        WebHost
            .CreateDefaultBuilder(args)
            .UseStartup<Startup>()

    [<EntryPoint>]
    let main args =
        CreateWebHostBuilder(args).Build().Run()
        exitCode
