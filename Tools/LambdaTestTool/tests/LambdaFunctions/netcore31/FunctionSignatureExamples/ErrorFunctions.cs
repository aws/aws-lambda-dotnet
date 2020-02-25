using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

namespace FunctionSignatureExamples
{
    public class ErrorFunctions
    {
        public void SyncThrowException()
        {
            throw new ApplicationException("Test Exception");
        }

        public Task AsyncNoResultThrowException()
        {
            return Task.Run(() => throw new ApplicationException("Test Exception"));
        }

        public Task<string> AsyncWithResultThrowException()
        {
            return Task<string>.Run((Func<string>)(() => throw new ApplicationException("Test Exception")));
        }
    }
}
