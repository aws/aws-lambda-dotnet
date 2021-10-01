using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations
{
    public class LambdaFunctionAttribute : Attribute
    {
        public string Name { get; set; }

        public int Timeout { get; set; }

        public int MemorySize { get; set; }

        public string Role { get; set; }

        public string Policies { get; set; }

        public LambdaFunctionAttribute(string name = null)
        {
            this.Name = name;
        }
    }
}
