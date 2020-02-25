using System.Collections.Generic;

namespace Amazon.Lambda.TestTool.WebTester.Models
{
    public class ConfigFileSummary
    {
        public string AWSProfile { get; set; }
        public string AWSRegion { get; set; }
        public List<FunctionSummary> Functions { get; set; }
    }
}