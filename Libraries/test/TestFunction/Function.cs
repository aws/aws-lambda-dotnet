using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestFunction
{
    
    public class Function
    {
        [Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public string ToUpper(string input)
        {
            return input?.ToUpper();
        }
    }
}
