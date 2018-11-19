using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool.WebTester.Models
{
    public class QueueItem
    {
        public string QueueUrl { get; set; }
        public string QueueName => this.QueueUrl.Substring(this.QueueUrl.LastIndexOf('/') + 1);
    }
}
