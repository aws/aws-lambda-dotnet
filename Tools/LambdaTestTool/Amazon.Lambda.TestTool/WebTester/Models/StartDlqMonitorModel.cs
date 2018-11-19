namespace Amazon.Lambda.TestTool.WebTester.Models
{
    public class StartDlqMonitorModel
    {
        public string Profile { get; set; }
        public string Region { get; set; }
        public string ConfigFile { get; set; }
        public string Function { get; set; }
        public string QueueUrl { get; set; }
    }
}