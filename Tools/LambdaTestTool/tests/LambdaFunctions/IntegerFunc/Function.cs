using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;


namespace IntegerFunc
{
    public class Function
    {
        
        /// <summary>
        /// A simple function that takes an integer and returns a string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public static string FunctionHandler(int input, ILambdaContext context)
        {
            context.Logger.LogLine($"Executing function with input: {input}");
            Console.WriteLine("Testing Console Logging");
            
            return $"Hello {input}";
        }
    }
}
    