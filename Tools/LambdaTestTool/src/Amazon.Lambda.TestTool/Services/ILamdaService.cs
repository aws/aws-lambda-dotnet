using Amazon.Lambda.TestTool.Runtime;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Amazon.Lambda.TestTool.Services
{
    public interface ILamdaService
    {
        Task<ExecutionResponse> Execute(string functionName, HttpContext context, IDictionary<string, string> pathParameters);

    }
}
