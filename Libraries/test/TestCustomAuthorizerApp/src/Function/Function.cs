using System;

using Amazon.Lambda.Core;

namespace Function
{
    public class Function
    {
        public dynamic FunctionHandler(dynamic eventTrigger)
        {
            Console.WriteLine(eventTrigger);

            return new {};
        }
    }
}
