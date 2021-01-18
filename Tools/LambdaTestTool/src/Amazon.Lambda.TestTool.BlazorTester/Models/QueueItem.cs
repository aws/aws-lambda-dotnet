namespace Amazon.Lambda.TestTool.BlazorTester.Models
{
    public class QueueItem
    {
        public string QueueUrl { get; set; }
        public string QueueName => this.QueueUrl.Substring(this.QueueUrl.LastIndexOf('/') + 1);
    }
}