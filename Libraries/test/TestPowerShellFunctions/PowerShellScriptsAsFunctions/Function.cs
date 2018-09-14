using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;



namespace PowerShellScriptsAsFunctions
{
    public class Function : Amazon.Lambda.PowerShellHost.PowerShellFunctionHost
    {

        public Function(string script) : base(script)
        {

        }

    }
}
