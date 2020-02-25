
namespace Amazon.Lambda.TestTool.WebTester.Models
{
    public class FunctionSummary
    {
        public string FunctionName { get; set; }
        public string FunctionHandler { get; set; }
        
        public bool IsSuccess  => string.IsNullOrEmpty(ErrorMessage);
        public string ErrorMessage { get; set; }
    }
}