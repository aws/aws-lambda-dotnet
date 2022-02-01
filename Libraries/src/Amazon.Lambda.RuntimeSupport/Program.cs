using System;
using System.IO;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                throw new ArgumentException("The function handler was not provided via command line arguments.", nameof(args));
;           }

            var handler = args[0];

            RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer(handler);
            await runtimeSupportInitializer.RunLambdaBootstrap();
        }
    }
}
