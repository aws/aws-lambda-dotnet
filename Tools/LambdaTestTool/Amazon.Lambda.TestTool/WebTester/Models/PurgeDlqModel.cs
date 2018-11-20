namespace Amazon.Lambda.TestTool.WebTester.Models
{
    public class PurgeDlqModel
    {
        public string Profile { get; set; }
        public string Region { get; set; }
        public string QueueUrl { get; set; }        
    }
}