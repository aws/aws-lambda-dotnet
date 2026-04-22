using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;

namespace TestWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
#pragma warning disable ASPDEPR008,ASPDEPR004
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();
#pragma warning restore ASPDEPR008,ASPDEPR004
            host.Run();
        }
    }
}
