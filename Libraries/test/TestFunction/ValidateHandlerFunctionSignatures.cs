using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestFunction
{
    public class ValidateHandlerFunctionSignatures : BaseValidateHandlerFunctionSignatures
    {
        public string NoParameters()
        {
            return null;
        }

        public string OneStringParameters(string one)
        {
            return null;
        }

        public string TooManyParameters(string one, string two, string three)
        {
            return null;
        }
    }

    public class BaseValidateHandlerFunctionSignatures
    {
        public string InheritedMethod(string input)
        {
            return null;
        }
    }
}
