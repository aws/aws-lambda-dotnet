using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;


namespace ToUpperFunc
{
    public class Function
    {
        
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public static string FunctionHandler(string input, ILambdaContext context)
        {
            context.Logger.LogLine($"Executing function with input: {input}");
            Console.WriteLine("Testing Console Logging");
            
            if(string.Equals("error", input))
                throw new Exception("Forced Error");
            
            return input?.ToUpper();
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public static string ToLower(string input, ILambdaContext context)
        {
            return input?.ToLower();
        }
    }
}
    