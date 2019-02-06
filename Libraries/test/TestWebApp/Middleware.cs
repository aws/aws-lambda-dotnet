using Amazon.Lambda.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TestWebApp
{
    public class Middleware
    {
        private readonly RequestDelegate _next;

        public Middleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            LambdaLogger.Log("Middleware Invoked");

            context.Response.OnStarting(x =>
            {
                var lambdaContext = context.Items["LambdaContext"] as ILambdaContext;
                lambdaContext?.Logger.LogLine("OnStarting Called");
                return Task.FromResult(0);
            }, context);

            await _next(context);
        }
    }
}
