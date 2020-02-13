using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BlueprintBaseName._1
{
    public class StepFunctionTasks
    {
        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public StepFunctionTasks()
        {
        }


        public State Greeting(State state, ILambdaContext context)
        {
            state.Message = "Hello";

            if(!string.IsNullOrEmpty(state.Name))
            {
                state.Message += " " + state.Name;
            }

            // Tell Step Function to wait 5 seconds before calling 
            state.WaitInSeconds = 5;

            return state;
        }

        public State Salutations(State state, ILambdaContext context)
        {
            state.Message += ", Goodbye";

            if (!string.IsNullOrEmpty(state.Name))
            {
                state.Message += " " + state.Name;
            }

            return state;
        }
    }
}
