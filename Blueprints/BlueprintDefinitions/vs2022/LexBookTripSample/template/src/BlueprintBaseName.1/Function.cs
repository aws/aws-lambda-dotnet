using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using Amazon.Lambda.LexEvents;

using System.IO;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1
{
    public class Function
    {

        /// <summary>
        /// Then entry point for the Lambda function that looks at the current intent and calls 
        /// the appropriate intent process.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public LexResponse FunctionHandler(LexEvent lexEvent, ILambdaContext context)
        {
            IIntentProcessor process;

            switch (lexEvent.CurrentIntent.Name)
            {
                case "BookHotel":
                    process = new BookHotelIntentProcessor();
                    break;
                case "BookCar":
                    process = new BookCarIntentProcessor();
                    break;
                default:
                    throw new Exception($"Intent with name {lexEvent.CurrentIntent.Name} not supported");
            }


            return process.Process(lexEvent, context);
        }

    }
}
