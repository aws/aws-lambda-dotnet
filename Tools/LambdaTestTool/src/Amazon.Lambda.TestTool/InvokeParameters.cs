using System;
namespace Amazon.Lambda.TestTool
{
    public class InvokeParameters
    {
        public string Profile { get; set; }
        public string Region { get; set; }
        public string Payload { get; set; }
    }
}
