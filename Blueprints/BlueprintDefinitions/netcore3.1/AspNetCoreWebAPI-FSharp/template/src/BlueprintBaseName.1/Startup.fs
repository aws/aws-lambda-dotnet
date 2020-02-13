namespace BlueprintBaseName._1


open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection


type Startup(configuration: IConfiguration) =
    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =
        // To add AWS services to the ASP.NET Core dependency injection add
        // the AWSSDK.Extensions.NETCore.Setup NuGet package. Then
        // use the "AddAWSService" method to add AWS service clients.
        // services.AddAWSService<Amazon.S3.IAmazonS3>() |> ignore

        // Add framework services.
        services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1) |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IHostingEnvironment) =
        app.UseMvc() |> ignore
